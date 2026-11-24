namespace Domain.Rules;

/// <summary>
/// A business rule refused the action; the message is shown to the user as is.
/// Derives from InvalidOperationException so the API keeps answering 400 with the message (same as before the rules moved here).
/// </summary>
public sealed class BusinessRuleException(string message) : InvalidOperationException(message);
