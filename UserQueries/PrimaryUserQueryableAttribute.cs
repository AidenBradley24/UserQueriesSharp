namespace UserQueries;

/// <summary>
/// When there is no specified property target, assume this property is the target.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class PrimaryUserQueryableAttribute(string propertyName) : Attribute
{
	public string PropertyName { get; } = propertyName;
}
