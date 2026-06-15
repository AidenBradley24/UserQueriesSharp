namespace UserQueries;

/// <summary>
/// Specifies that a property has a specified nested property that can be queried.
/// </summary>
/// <param name="queryName">The name used in the query.</param>
/// <param name="embeddedPropertyName">The property that should be queried.</param>
[AttributeUsage(AttributeTargets.Property)]
public sealed class EmbeddedUserQueryableAttribute(string queryName, string embeddedPropertyName) : Attribute
{
	/// <summary>
	/// Defines the name used in the query
	/// </summary>
	public string QueryName { get; } = queryName;

	/// <summary>
	/// Defines the property that should be queried. This allows for nested properties to be queried directly from the parent object.
	/// </summary>
	public string EmbeddedPropertyName { get; } = embeddedPropertyName;

	/// <summary>
	/// Specifies the order of precedence when mutiple properties are involved
	/// </summary>
	public int Order { get; set; } = 0;
}
