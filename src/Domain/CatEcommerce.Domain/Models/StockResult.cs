namespace CatEcommerce.Domain.Models;

/// <summary>
/// HO-IMP-001 §8.8: "the caller converts a failure into its <c>CommandResponse</c>; the port never
/// throws for a business rule." Helper factories keep every consumer and fake constructing this the
/// same way.
/// </summary>
public sealed record StockResult<T>(bool IsSuccess, T? Value, string? ViolationCode, string? Message, string? PropertyName)
{
    public static StockResult<T> Success(T value) => new(true, value, null, null, null);

    public static StockResult<T> Failure(string violationCode, string message, string? propertyName = null) =>
        new(false, default, violationCode, message, propertyName);
}
