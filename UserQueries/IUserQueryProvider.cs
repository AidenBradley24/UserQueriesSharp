namespace UserQueries;

/// <summary>
/// A safe way of allowing users to specify a string to query a database.
/// </summary>
/// <remarks>
/// Use <see cref="UserQueryableAttribute"/> to specify queryable public properties in the model class. <br/>
/// Use <see cref="PrimaryUserQueryableAttribute"/> to specify the primary property to compare on the model class.
/// </remarks>
/// <typeparam name="TModel">The type of queryable model</typeparam>
public interface IUserQueryProvider<TModel>
{
	/// <summary>
	/// Parse a user defined query of the <typeparamref name="TModel"/> type.
	/// </summary>
	/// <remarks>
	/// This is a non-exhaustive explaination of user queries. For a full explaination see UserQueries.md.
	/// <list type="table">
	///   <listheader>
	///     <term>Operator</term>
	///     <description>Description</description>
	///   </listheader>
	///   <item><term><c>=</c></term><description>equals</description></item>
	///   <item><term><c>!=</c></term><description>not equals</description></item>
	///   <item><term><c>^</c></term><description>starts with (string only)</description></item>
	///   <item><term><c>!^</c></term><description>does not start with (string only)</description></item>
	///   <item><term><c>$</c></term><description>ends with (string only)</description></item>
	///   <item><term><c>!$</c></term><description>does not end with (string only)</description></item>
	///   <item><term><c>*</c></term><description>contains (string only)</description></item>
	///   <item><term><c>!*</c></term><description>does not contain (string only)</description></item>
	///   <item><term><c>&lt;</c></term><description>less than (int only)</description></item>
	///   <item><term><c>&gt;</c></term><description>greater than (int only)</description></item>
	///   <item><term><c>&lt;=</c></term><description>less than or equal to (int only)</description></item>
	///   <item><term><c>&gt;=</c></term><description>greater than or equal to (int only)</description></item>
	/// </list>
	///
	/// <para><b>Notes:</b></para>
	/// <list type="bullet">
	///   <item>Commas (<c>,</c>) separate sections that are ORed together.</item>
	///   <item>Quotes inside values are parsed as string literals. Escape characters: <c>\"</c> and <c>\\</c>.</item>
	///   <item>All comparisons are case-insensitive.</item>
	///   <item>Whitespace outside of quotes is ignored.</item>
	/// </list>
	/// </remarks>
	/// <exception cref="InvalidUserQueryException"></exception>
	public IQueryable<TModel> EvaluateUserQuery(string queryText);
}
