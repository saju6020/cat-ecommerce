using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CatEcommerce.Infrastructure.Billing;

/// <summary>Adds the match and saves it alone; a unique violation on the active-line or active-payment index means someone else got there
/// first, so the failed entry is detached and the caller is told <c>false</c>. Any other failure is a real error and rethrows.</summary>
public sealed class EfBankMatchRecorder(CatEcommerceDbContext db) : IBankMatchRecorder
{
    private const string LineIndex = "ix_bank_match_line";
    private const string PaymentIndex = "ix_bank_match_payment";

    public async Task<bool> TryRecordAsync(BankMatchAggregate match, CancellationToken ct = default)
    {
        db.Set<BankMatchAggregate>().Add(match);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: LineIndex or PaymentIndex })
        {
            db.Entry(match).State = EntityState.Detached;
            return false;
        }
    }
}
