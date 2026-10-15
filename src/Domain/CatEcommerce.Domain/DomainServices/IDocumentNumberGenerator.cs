using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>HO-IMP-001 ADR-114 -- <c>{prefix}-{yyyy}-{seq:D6}</c> from a PostgreSQL sequence per <see cref="DocumentKind"/>.
/// Not gapless: a rolled-back command burns a number, which is fine for paperwork references (FR-BIL-1's gapless rule is for
/// invoices).</summary>
public interface IDocumentNumberGenerator
{
    Task<string> NextAsync(DocumentKind kind, CancellationToken ct);
}
