namespace UserQueries;

/// <summary>
/// Specifies that a navigation property has one embedded property that should be included for use in user queries. <br/>
/// Stack multiple on one navigation property to define multiple from the same class. <br/>
/// Supports multiple navigation properties deep with the <paramref name="path"/>.
/// </summary>
/// <param name="queryName">The name used in the query.</param>
/// <param name="path">The path to the nested property that should be queried starting from the class of the navigation property.</param>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public sealed class EmbeddedUserQueryableAttribute(string queryName, string path) : Attribute
{
	/// <summary>
	/// Defines the name used in the query
	/// </summary>
	public string QueryName { get; } = queryName;

	/// <summary>
	/// The path to the nested property that should be queried starting from the class of the navigation property.
	/// </summary>
	public string Path { get; } = path;

	/// <summary>
	/// Specifies the order of precedence when mutiple properties are involved
	/// </summary>
	public int Order { get; set; } = 0;
}
