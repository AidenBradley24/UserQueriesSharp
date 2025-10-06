#pragma warning disable CS1591
namespace UserQueries;

/// <summary>
/// Occurs when something is wrong with the entity / model object's configuration.
/// </summary>
public class UserQueryableMisconfigurationException(string? message) : Exception(message) { }

/// <summary>
/// Occurs when the user writes an invalid user query string.
/// </summary>
public class InvalidUserQueryException : Exception
{
	public InvalidUserQueryException(string message) : base(message) { }

	public InvalidUserQueryException() { }

	public InvalidUserQueryException(string? message, Exception? innerException) : base(message, innerException) { }
}
