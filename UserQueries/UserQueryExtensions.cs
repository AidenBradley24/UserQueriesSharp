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
				var exp = Expression.Property(modelExpressionLocal, p);
				return (a.QueryName, a.Order, Property: p, Expression: exp);
			});

		var embeddedQueryableAttrs = modelType
			.GetProperties(BindingFlags.Instance | BindingFlags.Public)
			.SelectMany(p => p.GetCustomAttributes<EmbeddedUserQueryableAttribute>(), (p, a) => (p, a))
			.Select(tuple => 
			{
				var (p, a) = tuple;
				var embeddedProperty = p.PropertyType.GetProperty(a.EmbeddedPropertyName)
					?? throw new UserQueryableMisconfigurationException($"Embedded property '{a.EmbeddedPropertyName}' not found on type '{p.PropertyType.Name}'.");
				var exp = Expression.Property(Expression.Property(modelExpressionLocal, p), embeddedProperty);
				return (a.QueryName, a.Order, Property: embeddedProperty, Expression: exp);
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
