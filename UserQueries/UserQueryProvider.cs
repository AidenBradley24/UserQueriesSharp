using System.Collections.Frozen;
using System.Linq.Expressions;
using System.Reflection;
using static UserQueries.Tokenizer;

namespace UserQueries;

/// <inheritdoc cref="IUserQueryProvider{TModel}"/>
public sealed class UserQueryProvider<TModel> : IUserQueryProvider<TModel>
{
	private readonly IQueryable<TModel> root;
	private readonly FrozenDictionary<string, PropertyInfo> queryableProperties;
	private readonly Token? defaultTarget;

	private static readonly FrozenSet<string> operators =
	[
		"=",     // equals
		"!=",    // not equals
		"^",     // starts with (string only)
		"!^",    // does not start with (string only)
		"$",     // ends with (string only)
		"!$",    // does not end with (string only)
		"*",     // contains (string only)
		"!*",    // does not contain (string only)
		"<",     // less than
		"<=",    // less than or equal to
		">",     // greater than
		">="     // greater than or equal to
	];

	/// <summary>
	/// Initialize a new provider for <typeparamref name="TModel"/>
	/// </summary>
	/// <param name="root">The base <see cref="IQueryable"/> to evaluate queries off of.<br/>
	/// To restrict access to certain entities, provide those restictions to this root.</param>
	/// <exception cref="UserQueryableMisconfigurationException"></exception>
	public UserQueryProvider(IQueryable<TModel> root)
	{
		this.root = root;
		queryableProperties = typeof(TModel).GetProperties()
			.Where(p => p.GetCustomAttributes<UserQueryableAttribute>(true).Any())
			.Select(p =>
			{
				if (!p.CanRead) throw new UserQueryableMisconfigurationException($"Type {typeof(TModel).FullName} {p.Name} is not readable.");
				return new KeyValuePair<string, PropertyInfo>(p.GetCustomAttributes<UserQueryableAttribute>(true).First().QueryName, p);
			}).ToFrozenDictionary();
		if (queryableProperties.ContainsKey("orderby") || queryableProperties.ContainsKey("orderbydescending"))
			throw new UserQueryableMisconfigurationException("'orderby' and 'orderbydescending' are reserved words. They cannot be used as property names.");
		string? defaultPropName = typeof(TModel).GetCustomAttribute<PrimaryUserQueryableAttribute>()?.PropertyName;
		var defaultProperty = defaultPropName != null ? typeof(TModel).GetProperty(defaultPropName) : null;
		if (defaultProperty == null && defaultPropName != null)
			throw new UserQueryableMisconfigurationException($"Type {typeof(TModel).FullName} {defaultPropName} not found.");
		if (defaultProperty != null && !defaultProperty.CanRead)
			throw new UserQueryableMisconfigurationException($"Type {typeof(TModel).FullName} {defaultProperty.Name} is not readable.");
		if (defaultProperty != null)
		{
			var att = defaultProperty.GetCustomAttribute<UserQueryableAttribute>()
				?? throw new UserQueryableMisconfigurationException($"Type {typeof(TModel).FullName} {defaultProperty.Name} is not user queryable.");
			defaultTarget = new Token(att.QueryName, false);
		}
	}

	private readonly ParameterExpression model = Expression.Parameter(typeof(TModel), "x");

	/// <inheritdoc/>
	public IQueryable<TModel> EvaluateUserQuery(string queryText)
	{
		IWideEnumerator<Token> tokens = Tokenize(queryText).GetWideEnumerator(1, 2);

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
								var lambda = Expression.Lambda<Func<TModel, bool>>(term, model);
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
						else if (reference != null && operators.Contains(tokens.Current.Value))
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
				var lambda = Expression.Lambda<Func<TModel, bool>>(currentTerm, model);
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

	private PropertyInfo GetProperty(Token token)
	{
		if (token.Value == "orderby" || token.Value == "orderbydescending") throw new InvalidUserQueryException("Incomplete: include property to sort by");
		bool exists = queryableProperties.TryGetValue(token.Value.ToLowerInvariant(), out var property);
		if (!exists) throw new InvalidUserQueryException($"Property \"{token.Value}\" not in type {typeof(TModel).FullName}\n" +
			$"Use {nameof(UserQueryableAttribute)} to specify properties as queryable.");
		return property!;
	}

	private Expression EvaluateComparison(Token target, Token compared, Token? op)
	{
		PropertyInfo targetProp = GetProperty(target);
		bool supportsString = typeof(string).IsAssignableFrom(targetProp.PropertyType);
		bool supportsComparision = typeof(IComparable).IsAssignableFrom(targetProp.PropertyType) && targetProp.PropertyType != typeof(string);
		op ??= new Token(supportsString ? "*" : "=", false);

		Expression left = Expression.Property(model, targetProp);
		Expression right;

		if (compared.IsLiteral)
		{
			object? parsed = UserQueryTypeParser.Parse(targetProp.PropertyType, compared.Value);
			right = Expression.Constant(parsed, targetProp.PropertyType);
		}
		else
		{
			PropertyInfo comparedProp = GetProperty(compared);
			right = Expression.Property(model, comparedProp);
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
		var property = Expression.Property(model, GetProperty(sortProperty));

		Expression conversion = property.Type.IsValueType
			? Expression.Convert(property, typeof(object))
			: property;

		return Expression.Lambda<Func<TModel, object>>(conversion, model);
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

	enum EvaluationMode
	{
		ReadyMode,
		FinishMode,
		InitialMode
	}
}
