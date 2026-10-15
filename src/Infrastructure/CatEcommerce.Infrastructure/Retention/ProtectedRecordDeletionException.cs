using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Infrastructure.Retention;

/// <summary>
/// HO-XCT-001 §8.12. Raised by <see cref="HardDeleteGuardInterceptor"/> before any SQL is sent. It is a programming
/// error -- some code path tried to remove a record the classification says must be kept -- so it is logged at Error
/// with the table name and mapped to <c>409 RecordIsRetained</c> (§12.5). The message carries the table name for the
/// log only; the HTTP response uses the plain-language <c>OperationsMessages.RecordIsRetained</c>.
/// </summary>
public sealed class ProtectedRecordDeletionException(string schema, string table, HardDeletePolicy policy)
    : InvalidOperationException($"Hard delete of {schema}.{table} is not permitted ({policy.DisplayName}).")
{
    public string Schema { get; } = schema;
    public string Table { get; } = table;
    public HardDeletePolicy Policy { get; } = policy;
}
