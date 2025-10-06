using System.Reflection;
using System.Text;

namespace UserQueries;

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
	/// Returns the queryable property names of a record in a set order.
	/// </summary>
	public static string[] GetQueryableProperties(Type modelType)
	{
		return [.. modelType.GetCustomAttributes<UserQueryableAttribute>().OrderBy(a => a.Order).Select(a => a.QueryName)];
	}

	/// <summary>
	/// Returns the values of a record in the same order as <see cref="GetQueryableProperties(Type)"/>
	/// </summary>
	public static string[] GetQueryablePropertiesRecord<TModel>(TModel record)
	{
		if (record == null) throw new ArgumentNullException(nameof(record));
		var type = typeof(TModel);
		var props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
		var queryableAttrs = type.GetCustomAttributes<UserQueryableAttribute>().OrderBy(a => a.Order).ToArray();
		if (queryableAttrs.Length == 0)
			return [];

		var result = new string[queryableAttrs.Length];
		for (int i = 0; i < queryableAttrs.Length; i++)
		{
			var attr = queryableAttrs[i];
			var prop = props.FirstOrDefault(p => p.GetCustomAttribute<UserQueryableAttribute>()?.QueryName == attr.QueryName)
				?? throw new UserQueryableMisconfigurationException($"Property with QueryName '{attr.QueryName}' not found on type '{type.Name}'.");
			var value = prop.GetValue(record);
			result[i] = value?.ToString() ?? "";
		}
		return result;
	}

	public static bool IsValidQueryablePropertyName(string propertyName)
	{
		if (string.IsNullOrEmpty(propertyName)) return false;
		if (!char.IsLetter(propertyName[0])) return false;
		foreach (char c in propertyName.AsSpan()[1..])
		{
			if (!char.IsLetterOrDigit(c) && c != '_')
				return false;
		}
		return true;
	}
}
