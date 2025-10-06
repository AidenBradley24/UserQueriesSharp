namespace UserQueries;

/// <summary>
/// Specifies that a property can be queried by users and provides metadata for the query.
/// </summary>
/// <remarks>This attribute is intended to mark properties that are exposed for user-defined queries, such as in
/// search or filtering scenarios.
/// <code>
/// [PrimaryUserQueryable(nameof(Property))]
/// public class Entity 
/// {
///		[UserQueryable("propertyName")]
///		public string Property { get; set; }
/// }
/// </code>
/// </remarks>
/// <param name="queryName">The name to be used within a query. Same naming rules as C-Sharp</param>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class UserQueryableAttribute(string queryName) : Attribute
{
	/// <summary>
	/// Defines the name used in the query
	/// </summary>
	public string QueryName { get; } = queryName;

	/// <summary>
	/// Specifies the order of precedence when mutiple properties are involved
	/// </summary>
	public int Order { get; set; } = 0;
}
