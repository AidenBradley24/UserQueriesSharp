using System.Collections.Frozen;

namespace UserQueries;

internal static class UserQueryOperatorCapabilities
{
	private static readonly FrozenSet<Type> ComparableTypes =
	[
		typeof(int), typeof(double), typeof(float), typeof(TimeSpan)
	];

	private static readonly FrozenSet<Type> StringCompatibleOperators = [typeof(string)];

	public static bool SupportsComparison(Type type)
	{
		return ComparableTypes.Contains(type);
	}

	public static bool SupportsStringMatch(Type type)
	{
		return StringCompatibleOperators.Contains(type);
	}
}
