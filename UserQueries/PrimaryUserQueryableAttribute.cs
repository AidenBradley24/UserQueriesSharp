namespace UserQueries;

/// <summary>
/// Specifies a property within a class to be used as the default property.
/// </summary>
/// <remarks>
/// <code>
/// [PrimaryUserQueryable(nameof(Property))]
/// public class Entity 
/// {
///		[UserQueryable("propertyName")]
///		public string Property { get; set; }
/// }
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class PrimaryUserQueryableAttribute(string propertyName) : Attribute
{
	/// <summary>
	/// The name of the property.
	/// </summary>
	public string PropertyName { get; } = propertyName;
}
