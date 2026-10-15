using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Visit;
using CatEcommerce.Read.QueryHandlers.StoredFile;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Visit;

/// <summary>§12.4 <c>GET /api/staff/consent-forms/{kind}</c> -- the wording currently in force for a consent kind. It is the
/// HO-ADM-001 policy document's current version, read as that version's body (<c>LimitedMarkup</c>, never HTML).</summary>
public sealed class GetConsentFormQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetConsentFormQuery, ConsentFormViewModel?>
{
    public async Task<QueryResponse<ConsentFormViewModel?>> HandleAsync(GetConsentFormQuery query)
    {
        if (!EnumerationParsing.TryParse<ConsentKind>(query.Kind, out var kind) || kind is null)
            return new QueryResponse<ConsentFormViewModel?> { Result = null };

        var key = ConsentForms.KeyFor(kind);
        var form = await (
            from d in db.Set<PolicyDocumentAggregate>().AsNoTracking()
            where d.Key == key && d.CurrentVersionNumber != null && !d.IsMarkedToDelete
            join v in db.Set<PolicyVersionAggregate>().AsNoTracking() on new { Key = d.Key, Number = d.CurrentVersionNumber!.Value } equals new { Key = v.PolicyKey, Number = v.VersionNumber }
            select new ConsentFormViewModel(d.Key, v.VersionNumber, d.Title, v.Body))
            .FirstOrDefaultAsync();

        return new QueryResponse<ConsentFormViewModel?> { Result = form };
    }
}

public sealed class GetConsentQueryHandler(CatEcommerceDbContext db, IStoredFileSigningBatcher signer)
    : IQueryHandlerAsync<GetConsentQuery, ConsentViewModel?>
{
    public async Task<QueryResponse<ConsentViewModel?>> HandleAsync(GetConsentQuery query)
    {
        var consent = await db.Set<ConsentRecordAggregate>().AsNoTracking()
            .Where(c => c.Id == query.ConsentId && c.BranchId == query.BranchId && !c.IsMarkedToDelete)
            .Select(c => new
            {
                c.Id, c.Version, c.BranchId, c.CatId, c.AppointmentId, c.Kind, c.ProcedureDescription, c.Method, c.SignedByName, c.SignerRelationship,
                c.SignedByCustomerId, c.SignedOn, c.PolicyKey, c.PolicyVersionNumber, c.CapturedBy, c.WitnessStaffId, c.Status,
                c.WithdrawnOn, c.WithdrawnBy, c.WithdrawalReason, c.FileId, HasSignature = c.Signature != null
            })
            .FirstOrDefaultAsync();
        if (consent is null) return new QueryResponse<ConsentViewModel?> { Result = null };

        var timeZoneId = await db.Set<BranchAggregate>().AsNoTracking().Where(b => b.Id == consent.BranchId).Select(b => b.TimeZoneId).FirstOrDefaultAsync() ?? "UTC";
        var names = await StaffNames.LookupAsync(db, [consent.CapturedBy, consent.WitnessStaffId, consent.WithdrawnBy]);
        var file = consent.FileId is { } fileId
            ? (await signer.SignManyAsync([fileId], "inline")).GetValueOrDefault(fileId)
            : null;

        return new QueryResponse<ConsentViewModel?>
        {
            Result = new ConsentViewModel(
                consent.Id, consent.Version, consent.CatId, consent.AppointmentId, consent.Kind.DisplayName, consent.ProcedureDescription, consent.Method.DisplayName,
                consent.SignedByName, consent.SignerRelationship.DisplayName, consent.SignedByCustomerId, consent.SignedOn,
                consent.PolicyKey, consent.PolicyVersionNumber, StaffNames.Of(names, consent.CapturedBy),
                consent.WitnessStaffId is null ? null : StaffNames.Of(names, consent.WitnessStaffId),
                consent.Status.DisplayName, consent.WithdrawnOn, consent.WithdrawnBy is null ? null : StaffNames.Of(names, consent.WithdrawnBy), consent.WithdrawalReason,
                timeZoneId,
                // §12.4: "signatureUrl to the SVG or the file's signed URL".
                consent.HasSignature ? $"/api/staff/consents/{consent.Id}/signature.svg" : file?.Url,
                file)
        };
    }
}

public sealed class GetCatConsentsQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetCatConsentsQuery, IReadOnlyList<ConsentListItemViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<ConsentListItemViewModel>>> HandleAsync(GetCatConsentsQuery query)
    {
        var rows = await db.Set<ConsentRecordAggregate>().AsNoTracking()
            .Where(c => c.CatId == query.CatId && !c.IsMarkedToDelete)
            .OrderByDescending(c => c.SignedOn).ThenBy(c => c.Id)
            .Select(c => new { c.Id, c.AppointmentId, c.Kind, c.Method, c.Status, c.SignedOn, c.PolicyVersionNumber, c.SignedByName })
            .Take(200)
            .ToListAsync();

        IReadOnlyList<ConsentListItemViewModel> items = rows
            .Select(c => new ConsentListItemViewModel(c.Id, c.AppointmentId, c.Kind.DisplayName, c.Method.DisplayName, c.Status.DisplayName, c.SignedOn, c.PolicyVersionNumber, c.SignedByName))
            .ToList();

        return new QueryResponse<IReadOnlyList<ConsentListItemViewModel>> { Result = items };
    }
}

/// <summary>The status contract (US-CLN-21 AC2): the pure <see cref="ConsentRules.IsValidFor"/> applied to this cat's recorded
/// consents for the procedure booking. A withdrawn consent, another appointment or another kind never counts.</summary>
public sealed class GetConsentStatusQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetConsentStatusQuery, ConsentStatusViewModel>
{
    public async Task<QueryResponse<ConsentStatusViewModel>> HandleAsync(GetConsentStatusQuery query)
    {
        if (!EnumerationParsing.TryParse<ConsentKind>(query.Kind, out var kind) || kind is null)
            return new QueryResponse<ConsentStatusViewModel> { Result = new ConsentStatusViewModel(false, null, null) };

        // Served by ix_consent_record_cat_appointment_kind_status. The predicate is ConsentRules.IsValidFor written as a query: the
        // same cat, appointment and kind, and the consent still Recorded (a withdrawn one never counts).
        var valid = await db.Set<ConsentRecordAggregate>().AsNoTracking()
            .Where(c => c.CatId == query.CatId && c.AppointmentId == query.AppointmentId && c.Kind == kind
                && c.Status == ConsentStatus.Recorded && !c.IsMarkedToDelete)
            .OrderByDescending(c => c.SignedOn).ThenBy(c => c.Id)
            .Select(c => new { c.Id, c.SignedOn })
            .FirstOrDefaultAsync();

        return new QueryResponse<ConsentStatusViewModel>
        {
            Result = valid is null ? new ConsentStatusViewModel(false, null, null) : new ConsentStatusViewModel(true, valid.Id, valid.SignedOn)
        };
    }
}

public sealed class GetConsentSignatureQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetConsentSignatureQuery, string?>
{
    public async Task<QueryResponse<string?>> HandleAsync(GetConsentSignatureQuery query)
    {
        var signature = await db.Set<ConsentRecordAggregate>().AsNoTracking()
            .Where(c => c.Id == query.ConsentId && c.BranchId == query.BranchId && !c.IsMarkedToDelete)
            .Select(c => c.Signature)
            .FirstOrDefaultAsync();

        return new QueryResponse<string?> { Result = signature?.ToSvg() };
    }
}
