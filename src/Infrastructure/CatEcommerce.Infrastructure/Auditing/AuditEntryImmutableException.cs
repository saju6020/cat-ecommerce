namespace CatEcommerce.Infrastructure.Auditing;

/// <summary>HO-ACC-001 §11.9. A programming error, not a business rule violation -- an
/// <see cref="Domain.Aggregates.Audit.AuditEntryAggregate"/> must never be modified or deleted.</summary>
public sealed class AuditEntryImmutableException()
    : InvalidOperationException("Audit entries are immutable and cannot be modified or deleted.");
