using System.Globalization;

namespace UserQueries;

internal static class UserQueryTypeParsers
{
	public static readonly Dictionary<Type, Func<string, object>> Parsers = new()
	{
		[typeof(int)] = s => int.Parse(s, CultureInfo.InvariantCulture),
		[typeof(double)] = s => double.Parse(s, CultureInfo.InvariantCulture),
		[typeof(float)] = s => float.Parse(s, CultureInfo.InvariantCulture),
		[typeof(TimeSpan)] = s => TimeSpan.Parse(s, CultureInfo.InvariantCulture),
		[typeof(string)] = s => s,
		// add more types here
	};

	public static object Parse(Type type, string literal)
	{
		if (!Parsers.TryGetValue(type, out var parser))
			throw new InvalidUserQueryException($"Unsupported type: {type.Name}");

		try
		{
			if (type.IsEnum) return Enum.Parse(type, literal);
			return parser(literal);
		}
		catch (FormatException ex)
		{
			throw new InvalidUserQueryException($"Unable to parse literal \"{literal}\" as type {type.FullName}", ex);
		}
	}
}
