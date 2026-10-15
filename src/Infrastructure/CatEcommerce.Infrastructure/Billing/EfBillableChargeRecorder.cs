using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Infrastructure.Billing;

/// <summary>
/// HO-BIL-001 §8.4 (ADR-178) -- the push path. Adds a <see cref="ChargeAggregate"/> to the caller's unit of
/// work <b>without saving</b> (the same shape as <c>INotificationOutbox</c>, F23): the producer's own repository
/// save commits it with the business change. Idempotent by source key -- a key already tracked in this context,
/// or already in the table, returns the existing charge id and adds nothing. The race where two requests record
/// one key between the pre-check and the save surfaces as <c>23505 ix_charge_source</c> -> <c>409 ChargeAlreadyRecorded</c>.
/// Charges raise no domain event (F31(c)). Never calls <c>SaveChangesAsync</c>.
/// </summary>
public sealed class EfBillableChargeRecorder(
    CatEcommerceDbContext db, IBusinessSettings businessSettings, IUserContextProvider userContextProvider, TimeProvider timeProvider)
    : IBillableChargeRecorder
{
    public async Task<Guid> RecordAsync(ChargeRequestDto request, CancellationToken ct = default)
    {
        var key = new ChargeSourceKey(request.SourceKind, request.SourceId, Normalise(request.SourceLineRef));

        var tracked = FindTracked(key);
        if (tracked is not null) return tracked.Id;

        var existingId = await db.Charges.AsNoTracking()
            .Where(c => c.SourceKind == key.SourceKind && c.SourceId == key.SourceId && c.SourceLineRef == key.SourceLineRef)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(ct);
        if (existingId is { } id) return id;

        var userContext = userContextProvider.GetUserContext();
        var charge = new ChargeAggregate(Guid.NewGuid());
        charge.Record(request, new ChargeRecordContext(
            businessSettings.GetString(SettingKeys.CommerceCurrencyCode), timeProvider.GetUtcNow(), new BillingActor(userContext, Guid.NewGuid())));

        db.Charges.Add(charge);
        return charge.Id;
    }

    public async Task VoidAsync(ChargeSourceKey key, string reason, CancellationToken ct = default)
    {
        var normalised = key with { SourceLineRef = Normalise(key.SourceLineRef) };

        var charge = FindTracked(normalised)
            ?? await db.Charges.FirstOrDefaultAsync(
                c => c.SourceKind == normalised.SourceKind && c.SourceId == normalised.SourceId && c.SourceLineRef == normalised.SourceLineRef, ct);
        if (charge is null) return;

        if (await ChargeBilling.IsBilledAsync(db, charge.Id, ct))
            throw new DomainRuleViolationException(BusinessViolationCodes.ChargeAlreadyInvoiced, BillingMessages.ChargeAlreadyInvoiced, nameof(key));

        charge.Void(reason, timeProvider.GetUtcNow(), new BillingActor(userContextProvider.GetUserContext(), Guid.NewGuid()));
    }

    private ChargeAggregate? FindTracked(ChargeSourceKey key) =>
        db.ChangeTracker.Entries<ChargeAggregate>()
            .Select(e => e.Entity)
            .FirstOrDefault(c => c.SourceKind == key.SourceKind && c.SourceId == key.SourceId && c.SourceLineRef == key.SourceLineRef);

    private static string Normalise(string? sourceLineRef) =>
        string.IsNullOrWhiteSpace(sourceLineRef) ? ChargeRules.DefaultSourceLineRef : sourceLineRef.Trim();
}
