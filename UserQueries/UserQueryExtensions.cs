using System.Collections.Frozen;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace UserQueries;

/// <summary>
/// A collection of User Query utility methods
/// </summary>
public static class UserQueryExtensions
{
	/// <inheritdoc cref="UserQueryProvider{TModel}.EvaluateUserQuery(string)"/>
	/// <exception cref="UserQueryableMisconfigurationException"></exception>
	public static IQueryable<TModel> EvaluateUserQuery<TModel>(this IQueryable<TModel> baseQueryable, string userQuery)
	{
		var provider = new UserQueryProvider<TModel>(baseQueryable);
		return provider.EvaluateUserQuery(userQuery);
	}

	/// <summary>
	/// Construct a GET request URL to return a page of contents from a REST API utilizing User Queries
	/// </summary>
	public static string ConstructRequest(string baseURL, string userFilter, string? orderBy, bool descending, int pageSize, int currentPage)
	{
		var builder = new StringBuilder(baseURL);

		if (!baseURL.EndsWith('/'))
			builder.Append('/');

		builder.Append("?query=");
		builder.Append(Uri.EscapeDataString(userFilter));

		if (orderBy != null)
		{
			builder.Append(Uri.EscapeDataString(descending ? " orderbydescending " : " orderby "));
			builder.Append(orderBy);
		}

		builder.Append("&pageSize=");
		builder.Append(pageSize);
		builder.Append("&currentPage=");
		builder.Append(currentPage);

		return builder.ToString();
	}

	/// <summary>
	/// Returns an ordered array of queryable properties for <paramref name="modelType"/>, including their associated query names.
	/// </summary>
	/// <exception cref="UserQueryableMisconfigurationException"></exception>
	public static IReadOnlyList<UserQueryProperty> GetQueryableProperties(Type modelType, out ParameterExpression modelExpression)
	{
		var modelExpressionLocal = Expression.Parameter(modelType, "x");
		modelExpression = modelExpressionLocal;

		var userQueryableAttrs = modelType
			.GetProperties(BindingFlags.Instance | BindingFlags.Public)
			.SelectMany(p => p.GetCustomAttributes<UserQueryableAttribute>(), (p, a) =>
			{
				Expression exp = Expression.Property(modelExpressionLocal, p);
				return (a.QueryName, a.Order, Property: p, Expression: exp);
			});

		var embeddedQueryableAttrs = modelType
			.GetProperties(BindingFlags.Instance | BindingFlags.Public)
			.SelectMany(p => p.GetCustomAttributes<EmbeddedUserQueryableAttribute>(), (p, a) => (p, a))
			.Select(tuple => 
			{
				var (rootProperty, attr) = tuple;

				var parts = attr.Path
					.Split('.')
					.Select(part => part.Trim())
					.ToArray();

				if (parts.Length == 0 || parts.Any(string.IsNullOrWhiteSpace))
					throw new UserQueryableMisconfigurationException(
						$"Embedded path '{attr.Path}' on property '{rootProperty.Name}' is invalid.");

				var (property, exp) = BuildEmbeddedPropertyAccess(modelExpressionLocal, rootProperty, parts);
				return (attr.QueryName, attr.Order, Property: property, Expression: exp);
			});

		var list = userQueryableAttrs
			.Concat(embeddedQueryableAttrs)
			.OrderBy(a => a.Order)
			.Select(a => new UserQueryProperty(a.QueryName, a.Property, a.Expression))
			.ToList();

		// Ensure all query names are valid and unique
		var queryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var item in list) 
		{
			var name = item.QueryName;
			var prop = item.Property;

			if (GetReservedWords().Contains(name, StringComparer.OrdinalIgnoreCase))
				throw new UserQueryableMisconfigurationException($"Query name '{name}' on type '{modelType.Name}' is a reserved word and cannot be used.");
			if (!IsValidQueryablePropertyName(name))
				throw new UserQueryableMisconfigurationException($"Invalid query name '{name}' on type '{modelType.Name}'. Query names must start with a letter and can only contain letters, digits, or underscores.");
			if (!queryNames.Add(name))
				throw new UserQueryableMisconfigurationException($"Duplicate query name '{name}' on type '{modelType.Name}'. Query names must be unique.");
			if (!prop.CanRead)
				throw new UserQueryableMisconfigurationException($"Queryable property '{name}' on type '{modelType.Name}' must be readable.");
		}

		if (modelType.GetCustomAttributes<PrimaryUserQueryableAttribute>().FirstOrDefault() is PrimaryUserQueryableAttribute primaryAttr)
		{
			var prop = modelType.GetProperty(primaryAttr.PropertyName, BindingFlags.Instance | BindingFlags.Public)
				?? throw new UserQueryableMisconfigurationException($"PrimaryUserQueryable property '{primaryAttr.PropertyName}' not found on type '{modelType.Name}'.");
			if (!list.Any(item => item.Property == prop))
				throw new UserQueryableMisconfigurationException($"PrimaryUserQueryable property '{primaryAttr.PropertyName}' on type '{modelType.Name}' is not a valid queryable property.");

			list.Add(new UserQueryProperty("default", prop, Expression.Property(modelExpressionLocal, prop)));
		}

		return list;
	}

	private static (PropertyInfo Property, Expression Expression) BuildEmbeddedPropertyAccess(
		ParameterExpression modelExpression,
		PropertyInfo rootProperty,
		string[] parts)
	{
		PropertyInfo currentProperty = rootProperty;
		Expression currentExpression = Expression.Property(modelExpression, rootProperty);
		Expression? nullGuard = null;

		if (parts.Length > 0 && CanBeNull(currentExpression.Type))
		{
			nullGuard = IsNull(currentExpression);
		}

		for (int i = 0; i < parts.Length; i++)
		{
			var sourceType = GetMemberSourceType(currentProperty.PropertyType);
			var sourceExpression = UnwrapNullable(currentExpression);

			var nextProperty = sourceType.GetProperty(parts[i], BindingFlags.Instance | BindingFlags.Public)
				?? throw new UserQueryableMisconfigurationException(
					$"Embedded property '{parts[i]}' not found on type '{sourceType.Name}'.");

			currentExpression = Expression.Property(sourceExpression, nextProperty);
			currentProperty = nextProperty;

			if (i < parts.Length - 1 && CanBeNull(currentExpression.Type))
			{
				nullGuard = nullGuard == null
					? IsNull(currentExpression)
					: Expression.OrElse(nullGuard, IsNull(currentExpression));
			}
		}

		if (currentProperty.PropertyType == typeof(string))
		{
			currentExpression = Expression.Coalesce(
				currentExpression,
				Expression.Constant(string.Empty, typeof(string)));
		}

		if (nullGuard != null)
		{
			currentExpression = Expression.Condition(
				nullGuard,
				GetFallbackExpression(currentProperty.PropertyType),
				currentExpression);
		}

		return (currentProperty, currentExpression);
	}

	private static bool CanBeNull(Type type)
	{
		return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
	}

	private static Expression IsNull(Expression expression)
	{
		return Expression.Equal(expression, Expression.Constant(null, expression.Type));
	}

	private static Type GetMemberSourceType(Type type)
	{
		return Nullable.GetUnderlyingType(type) ?? type;
	}

	private static Expression UnwrapNullable(Expression expression)
	{
		var underlyingType = Nullable.GetUnderlyingType(expression.Type);
		return underlyingType == null
			? expression
			: Expression.Property(expression, nameof(Nullable<int>.Value));
	}

	private static Expression GetFallbackExpression(Type type)
	{
		if (type == typeof(string))
			return Expression.Constant(string.Empty, typeof(string));

		return Expression.Default(type);
	}

	/// <summary>
	/// Returns an ordered array of queryable properties for the type of <typeparamref name="T"/>, including their associated query names.
	/// </summary>
	/// <exception cref="UserQueryableMisconfigurationException"></exception>
	public static IReadOnlyList<UserQueryProperty> GetQueryableProperties<T>(this IEnumerable<T> collection, out ParameterExpression modelExpression) 
	{ 
		return GetQueryableProperties(typeof(T), out modelExpression);
	}

	/// <summary>
	/// Words that are reserved for query syntax and cannot be used as queryable property names.
	/// </summary>
	public static string[] GetReservedWords() => ["default", "orderby", "orderbydescending"];

	/// <summary>
	/// Returns true if <paramref name="propertyName"/> is a valid user queryable name.
	/// </summary>
	public static bool IsValidQueryablePropertyName(string propertyName)
	{
		if (string.IsNullOrEmpty(propertyName)) return false;
		if (!char.IsLetter(propertyName[0])) return false;
		foreach (char c in propertyName.AsSpan()[1..])
		{
			if (!char.IsLetterOrDigit(c) && c != '_')
				return false;
		}
		if (GetReservedWords().Contains(propertyName, StringComparer.OrdinalIgnoreCase))
			return false;
		return true;
	}
}
