namespace CatEcommerce.Domain.Models;

/// <summary>
/// The result wrapper every <c>IFileStore</c> method returns (ADR-318). Shape not given verbatim by
/// ADR-318's code block (which only shows <c>Task&lt;FileStoreResult&lt;T&gt;&gt;</c>); modelled on
/// the sibling <see cref="StockResult{T}"/> shape used elsewhere in the same "port never throws for
/// a business rule" style (HO-IMP-001 §8.8), since HO-FIL-001 §10.2's violation codes
/// (<c>FileObjectMissing</c>, <c>FileTooLarge</c>, ...) need somewhere to surface without a throw.
/// Flagged as inferred in docs/handover/CONTRACTS.md.
/// </summary>
public sealed record FileStoreResult<T>(bool IsSuccess, T? Value, string? ViolationCode, string? Message)
{
    public static FileStoreResult<T> Success(T value) => new(true, value, null, null);

    public static FileStoreResult<T> Failure(string violationCode, string message) =>
        new(false, default, violationCode, message);
}
