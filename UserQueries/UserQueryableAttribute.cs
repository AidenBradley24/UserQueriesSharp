namespace UserQueries;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class UserQueryableAttribute(string queryName) : Attribute
{
	public string QueryName { get; } = queryName;
	public int Order { get; set; } = 0;
}
