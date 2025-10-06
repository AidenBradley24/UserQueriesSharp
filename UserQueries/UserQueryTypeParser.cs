using System.Globalization;
using System.Reflection;

namespace UserQueries;

internal static class UserQueryTypeParser
{
	public static MethodInfo? GetParseMethod(Type type)
	{
		var interfaceType = type.GetInterface("IParsable`1");
		if (interfaceType == null) return null;

		return type.GetMethod(
			"Parse",
			BindingFlags.Public | BindingFlags.Static,
			null,
			[typeof(string), typeof(IFormatProvider)],
			null
		);
	}

	public static object Parse(Type type, string literal)
	{
		if (type == typeof(string)) return literal;
		if (type.IsEnum)
		{
			try
			{
				return Enum.Parse(type, literal);
			}
			catch (Exception ex)
			{
				throw new InvalidUserQueryException($"Unable to parse literal \"{literal}\" as enum type {type.Name}", ex);
			}
		}

		MethodInfo? parseMethod = GetParseMethod(type)
			?? throw new InvalidUserQueryException($"Unsupported type: {type.Name}");

		try
		{
			return parseMethod.Invoke(null, [literal, CultureInfo.InvariantCulture])!;
		}
		catch (TargetInvocationException ex)
		{
			throw new InvalidUserQueryException($"Unable to parse literal \"{literal}\" as type {type.FullName}", ex.InnerException);
		}
	}
}
