using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Inventory;

/// <summary>
/// HO-IMP-001 ADR-114 -- <c>{prefix}-{yyyy}-{seq:D6}</c> from one PostgreSQL sequence per <see cref="DocumentKind"/>. The sequence names
/// are a fixed table (never caller input), so the raw <c>nextval</c> is safe. Not gapless: a rolled-back command burns a number.
/// </summary>
public sealed class SequenceDocumentNumberGenerator(CatEcommerceDbContext db, TimeProvider timeProvider) : IDocumentNumberGenerator
{
    /// <summary>The prefix and sequence of every kind -- the migrations create exactly these sequences.</summary>
    public static readonly IReadOnlyDictionary<DocumentKind, (string Prefix, string Sequence)> Sequences =
        new Dictionary<DocumentKind, (string, string)>
        {
            [DocumentKind.PurchaseOrder] = ("PO", "po_number_seq"),
            [DocumentKind.Consignment] = ("CN", "consignment_number_seq"),
            [DocumentKind.GoodsReceipt] = ("GRN", "grn_number_seq"),
            [DocumentKind.StockTransfer] = ("TRF", "transfer_number_seq"),
            [DocumentKind.StockAdjustment] = ("ADJ", "adjustment_number_seq"),
            [DocumentKind.StockCount] = ("CNT", "count_number_seq"),
            [DocumentKind.BatchRecall] = ("RCL", "recall_number_seq"),
        };

    /// <summary>The statement for one of the fixed sequence names above (never caller input).</summary>
    private static string NextValueSql(string sequence) => "SELECT nextval('inventory." + sequence + "') AS \"Value\"";

    public async Task<string> NextAsync(DocumentKind kind, CancellationToken ct)
    {
        var (prefix, sequence) = Sequences[kind];
        var sql = NextValueSql(sequence);
        var next = await db.Database.SqlQueryRaw<long>(sql).SingleAsync(ct);

        return $"{prefix}-{timeProvider.GetUtcNow().Year}-{next:D6}";
    }
}
