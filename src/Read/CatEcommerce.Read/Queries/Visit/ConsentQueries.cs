using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Visit;

// HO-EMR-001 §12.6 -- consent reads (Phase H).

/// <summary>The current wording of the consent form for a kind; <c>null</c> when it has not been published (consent is then refused).</summary>
public sealed class GetConsentFormQuery : Query<ConsentFormViewModel?>
{
    public required string Kind { get; init; }
}

/// <summary>A consent of the caller's active branch; another branch's is not found.</summary>
public sealed class GetConsentQuery : Query<ConsentViewModel?>
{
    public required Guid ConsentId { get; init; }
    public required Guid BranchId { get; init; }
}

public sealed class GetCatConsentsQuery : Query<IReadOnlyList<ConsentListItemViewModel>>
{
    public required Guid CatId { get; init; }
}

/// <summary><b>HO-SRG-001's contract</b> (US-CLN-21 AC2 / US-SRG-03), answered by <c>ConsentRules.IsValidFor</c>.</summary>
public sealed class GetConsentStatusQuery : Query<ConsentStatusViewModel>
{
    public required Guid CatId { get; init; }
    public required Guid AppointmentId { get; init; }
    public required string Kind { get; init; }
}

/// <summary>The signature as an SVG of polylines and nothing else; <c>null</c> when there is none or it is another branch's.</summary>
public sealed class GetConsentSignatureQuery : Query<string?>
{
    public required Guid ConsentId { get; init; }
    public required Guid BranchId { get; init; }
}
