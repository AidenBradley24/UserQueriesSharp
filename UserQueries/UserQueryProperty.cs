using System.Linq.Expressions;
using System.Reflection;

namespace UserQueries
{
	/// <summary>
	/// Represents a property of a user queryable model that can be used in user queries, along with its associated query name and expression.
	/// </summary>
	public class UserQueryProperty(string queryName, PropertyInfo property, Expression expression)
	{
		/// <summary>
		/// The name used by users to reference this property in queries.
		/// </summary>
		public string QueryName { get; } = queryName;

		/// <summary>
		/// The <see cref="PropertyInfo"/> of the property that can be queried. This is the actual property on the model that will be accessed when this query name is used.
		/// </summary>
		public PropertyInfo Property { get; } = property;

		/// <summary>
		/// An <see cref="Expression"/> representing how to access this property on the model. This is used internally to build query expressions based on user queries.
		/// </summary>
		public Expression Expression { get; } = expression;
	}
}
