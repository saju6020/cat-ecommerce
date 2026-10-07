namespace CatEcommerce.Domain.Exceptions;

/// <summary>
/// Raised by an aggregate when a mutation would violate one of its own invariants.
/// Command handlers catch this and translate it to a CommandResponse (400), never a 500 —
/// HO-CAT-001 §8.6.
/// </summary>
public sealed class DomainRuleViolationException(string errorCode, string message, string propertyName)
    : Exception(message)
{
    public string ErrorCode { get; } = errorCode;
    public string PropertyName { get; } = propertyName;
}
