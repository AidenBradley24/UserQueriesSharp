using System.Collections.Frozen;
using System.Linq.Expressions;
using System.Reflection;
using static UserQueries.Tokenizer;

namespace UserQueries;

/// <inheritdoc cref="IUserQueryProvider{TModel}"/>
public sealed class UserQueryProvider<TModel> : IUserQueryProvider<TModel>
{
	private readonly IQueryable<TModel> root;
	private readonly FrozenDictionary<string, UserQueryProperty> queryableProperties;
	private readonly Token? defaultTarget;

	/// <summary>
	/// Initialize a new provider for <typeparamref name="TModel"/>
	/// </summary>
	/// <param name="root">The base <see cref="IQueryable"/> to evaluate queries off of.<br/>
	/// To restrict access to certain entities, provide those restictions to this root.</param>
	/// <exception cref="UserQueryableMisconfigurationException"></exception>
	public UserQueryProvider(IQueryable<TModel> root)
	{
		this.root = root;

		try 
		{
			queryableProperties = root
				.GetQueryableProperties(out var modelExpression)
				.ToFrozenDictionary(prop => prop.QueryName, prop => prop);

			ModelExpression = modelExpression;

			if (queryableProperties.TryGetValue("default", out var defaultProp)) 
			{
				defaultTarget = new Token(defaultProp.QueryName, false);
			}
		}
		catch (UserQueryableMisconfigurationException ex) 
		{
			throw new UserQueryableMisconfigurationException("Unable to initialize UserQueryProvider: " + ex.Message);
		}
	}

	private ParameterExpression ModelExpression { get; }

	/// <inheritdoc/>
	public IQueryable<TModel> EvaluateUserQuery(string queryText)
	{
		IWideEnumerator<Token> tokens = Tokenize(queryText).GetWideEnumerator(historyDepth: 1, foresightDepth: 2);

		try
		{
			EvaluationMode mode = EvaluationMode.InitialMode;
			Stack<Expression> terms = [];
			Expression? currentTerm = null;

			Token? reference = null;
			Token? sortProperty = null;
			bool descending = false;

			while (tokens.MoveNext())
			{
				switch (mode)
				{
					case EvaluationMode.InitialMode:
						if (!tokens.Current.IsLiteral)
						{
							if (tokens.Current.Value == "orderby")
							{
								if (!tokens.MoveNext()) throw new InvalidUserQueryException($"Incomplete: include property to sort by: {queryText} ...");
								sortProperty = tokens.Current;
								descending = false;
								break;
							}
							else if (tokens.Current.Value == "orderbydescending")
							{
								if (!tokens.MoveNext()) throw new InvalidUserQueryException($"Incomplete: include property to sort by: {queryText} ...");
								sortProperty = tokens.Current;
								descending = true;
								break;
							}
							else if (!queryableProperties.ContainsKey(tokens.Current.Value))
							{
								// CONSIDER THIS ENTIRE QUERY A LITERAL (except anything following orderby)
								// this allows simple searches on the default
								ArgumentNullException.ThrowIfNull(defaultTarget);

								int orderbyIndex = queryText.IndexOf("orderby", StringComparison.OrdinalIgnoreCase);
								string bigToken;
								IEnumerator<Token>? extraTokens = null;
								if (orderbyIndex != -1)
								{
									bigToken = queryText[..orderbyIndex].Trim();
									extraTokens = Tokenize(queryText[orderbyIndex..]).GetEnumerator();
								}
								else
								{
									bigToken = queryText;
								}

								var term = EvaluateComparison(defaultTarget, new Token(bigToken.ToString(), true), null);
								var lambda = Expression.Lambda<Func<TModel, bool>>(term, ModelExpression);
								var exp = root.Where(lambda);

								if (extraTokens != null)
								{
									if (tokens.Current.Value == "orderby")
									{
										if (!extraTokens.MoveNext()) throw new InvalidUserQueryException($"Incomplete: include property to sort by: {queryText} ...");
										sortProperty = tokens.Current;
										var orderLambda = CreateOrderByExpression(sortProperty);
										return exp.OrderBy(orderLambda);
									}
									else if (tokens.Current.Value == "orderbydescending")
									{
										if (!extraTokens.MoveNext()) throw new InvalidUserQueryException($"Incomplete: include property to sort by: {queryText} ...");
										sortProperty = tokens.Current;
										var orderLambda = CreateOrderByExpression(sortProperty);
										return exp.OrderByDescending(orderLambda);
									}
								}

								return exp;
							}
						}
						mode = EvaluationMode.ReadyMode;
						goto case EvaluationMode.ReadyMode;
					case EvaluationMode.ReadyMode:
						// start of a phrase
						Expression newTerm;
						if (reference != null && tokens.Foresight.Count >= 2 && tokens.Foresight[0].Value == "-")
						{
							newTerm = Expression.AndAlso(
								EvaluateComparison(reference, tokens.Current, new Token(">=", false)),
								EvaluateComparison(reference, tokens.Foresight[1], new Token("<=", false))
								);
							tokens.MoveBy(2);
						}
						else if (reference != null && IsOperator(tokens.Current.Value))
						{
							Token op = tokens.Current;
							if (!tokens.MoveNext()) throw new InvalidUserQueryException($"Incomplete: include property or literal to compare to: {queryText} ...");
							newTerm = EvaluateComparison(reference, tokens.Current, op);
						}
						else if (tokens.Current.IsLiteral)
						{
							Token target = reference ?? defaultTarget ?? throw new InvalidUserQueryException("A default target is not specified.");
							newTerm = EvaluateComparison(target, tokens.Current, null);
						}
						else
						{
							Token target = tokens.Current;
							if (!tokens.MoveNext()) throw new InvalidUserQueryException($"Incomplete: include operator: {queryText} ...");
							Token op = tokens.Current;

							if (op.Value == ":")
							{
								// refering to a term
								reference = target;
								break;
							}

							// term operator term
							if (!tokens.MoveNext()) throw new InvalidUserQueryException($"Incomplete: include property or literal to compare to: {queryText} ...");
							newTerm = EvaluateComparison(target, tokens.Current, op);
						}
						// if current term exists logical AND with existing
						currentTerm = currentTerm == null ? newTerm : Expression.AndAlso(currentTerm, newTerm);
						mode = EvaluationMode.FinishMode;
						break;
					case EvaluationMode.FinishMode:
						// end of a phrase and options to continue or exit
						if (tokens.Current.IsLiteral)
							throw new InvalidUserQueryException($"Literal must be seperated with a comma or &: {tokens.Current.Value}");
						if (tokens.Current.Value == ",")
						{
							if (currentTerm != null) terms.Push(currentTerm);
							currentTerm = null;
							mode = EvaluationMode.ReadyMode;
						}
						else if (tokens.Current.Value == "&")
						{
							mode = EvaluationMode.ReadyMode;
						}
						else if (tokens.Current.Value == "orderby")
						{
							if (!tokens.MoveNext()) throw new InvalidUserQueryException($"Incomplete: include property to sort by: {queryText} ...");
							sortProperty = tokens.Current;
							descending = false;
							break;
						}
						else if (tokens.Current.Value == "orderbydescending")
						{
							if (!tokens.MoveNext()) throw new InvalidUserQueryException($"Incomplete: include property to sort by: {queryText} ...");
							sortProperty = tokens.Current;
							descending = true;
							break;
						}
						else
						{
							throw new InvalidUserQueryException($"Invalid operator: {tokens.Current.Value}");
						}
						break;
				}
			}

			if (currentTerm != null)
				terms.Push(currentTerm);

			// Logical OR together terms
			IQueryable<TModel> returnValue;
			if (terms.TryPop(out currentTerm))
			{
				while (terms.TryPop(out var ex))
				{
					currentTerm = Expression.OrElse(currentTerm, ex);
				}
				var lambda = Expression.Lambda<Func<TModel, bool>>(currentTerm, ModelExpression);
				returnValue = root.Where(lambda);
			}
			else
			{
				returnValue = root;
			}

			if (sortProperty != null)
			{
				var lambda = CreateOrderByExpression(sortProperty);
				return descending ? returnValue.OrderByDescending(lambda) : returnValue.OrderBy(lambda);
			}
			else
			{
				return returnValue;
			}
		}
		catch (ArgumentException ex)
		{
			throw new InvalidUserQueryException(ex.Message, ex);
		}
	}

	private UserQueryProperty GetProperty(Token token)
	{
		if (token.Value == "orderby" || token.Value == "orderbydescending") throw new InvalidUserQueryException("Incomplete: include property to sort by");
		bool exists = queryableProperties.TryGetValue(token.Value.ToLowerInvariant(), out var property);
		if (!exists) throw new InvalidUserQueryException($"Property \"{token.Value}\" not in type {typeof(TModel).FullName}\n" +
			$"Use {nameof(UserQueryableAttribute)} to specify properties as queryable.");
		return property!;
	}

	private Expression EvaluateComparison(Token target, Token compared, Token? op)
	{
		UserQueryProperty targetProp = GetProperty(target);
		bool supportsString = typeof(string).IsAssignableFrom(targetProp.Property.PropertyType);
		bool supportsComparision = typeof(IComparable).IsAssignableFrom(targetProp.Property.PropertyType) && targetProp.Property.PropertyType != typeof(string);
		op ??= new Token(supportsString ? "*" : "=", false);

		Expression left = targetProp.Expression;
		Expression right;

		if (compared.IsLiteral)
		{
			object? parsed = UserQueryTypeParser.Parse(targetProp.Property.PropertyType, compared.Value);
			right = Expression.Constant(parsed, targetProp.Property.PropertyType);
		}
		else
		{
			UserQueryProperty comparedProp = GetProperty(compared);
			right = comparedProp.Expression;
		}

		Expression body = op.Value switch
		{
			"=" => Expression.Equal(left, right),
			"!=" => Expression.NotEqual(left, right),

			"<" when supportsComparision => Expression.LessThan(left, right),
			"<=" when supportsComparision => Expression.LessThanOrEqual(left, right),
			">" when supportsComparision => Expression.GreaterThan(left, right),
			">=" when supportsComparision => Expression.GreaterThanOrEqual(left, right),

			"^" when supportsString => CallInsensitive(left, right, nameof(string.StartsWith)),
			"!^" when supportsString => Expression.Not(CallInsensitive(left, right, nameof(string.StartsWith))),
			"$" when supportsString => CallInsensitive(left, right, nameof(string.EndsWith)),
			"!$" when supportsString => Expression.Not(CallInsensitive(left, right, nameof(string.EndsWith))),
			"*" when supportsString => CallInsensitive(left, right, nameof(string.Contains)),
			"!*" when supportsString => Expression.Not(CallInsensitive(left, right, nameof(string.Contains))),

			_ => throw new InvalidUserQueryException($"Unsupported or type-mismatched operator \"{op.Value}\" for property \"{target.Value}\"")
		};

		return body;
	}

	private Expression<Func<TModel, object>> CreateOrderByExpression(Token sortProperty)
	{
		var property = GetProperty(sortProperty).Expression;

		Expression conversion = property.Type.IsValueType
			? Expression.Convert(property, typeof(object))
			: property;

		return Expression.Lambda<Func<TModel, object>>(conversion, ModelExpression);
	}

	private static MethodCallExpression CallInsensitive(Expression left, Expression right, string methodName)
	{
		// Convert both expressions to lowercase: left.ToLower().method(right.ToLower())
		var toLowerMethod = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;

		var leftToLower = Expression.Call(left, toLowerMethod);
		var rightToLower = Expression.Call(right, toLowerMethod);

		var method = typeof(string).GetMethod(methodName, [typeof(string)])!;
		return Expression.Call(leftToLower, method, rightToLower);
	}

	private static bool IsOperator(string value) => value is
	"=" or
	"!=" or
	"^" or
	"!^" or
	"$" or
	"!$" or
	"*" or
	"!*" or
	"<" or
	"<=" or
	">" or
	">=";

	enum EvaluationMode
	{
		ReadyMode,
		FinishMode,
		InitialMode
	}
}
