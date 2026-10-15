using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Surgery;

/// <summary>Every campaign, newest first, with its prices (service names) and how many cases are booked under it. Practice configuration, not branch-scoped.</summary>
public sealed class GetSurgeryCampaignsQuery : Query<PagedResult<SurgeryCampaignViewModel>>
{
    public bool ActiveOnly { get; init; }
}

/// <summary>US-SRG-11 -- the campaigns that would apply to a booking of this procedure in this branch on this local date: active, dated, covering the branch, priced for the service
/// and not at their cap. Each with its price and the cases left.</summary>
public sealed class GetApplicableCampaignsQuery : Query<IReadOnlyList<ApplicableCampaignViewModel>>
{
    public required Guid BranchId { get; init; }
    public required Guid ServiceId { get; init; }
    public required DateOnly Date { get; init; }
}

/// <summary>US-SRG-11 "reportable separately" -- the surgeries under a campaign at the caller's branch, in a local date range, with totals and the standard price each would have
/// cost (HO-ADM-001's price effective when the surgery was booked). Null when the campaign does not exist.</summary>
public sealed class GetCampaignCasesQuery : Query<CampaignCasesViewModel?>
{
    public required Guid CampaignId { get; init; }
    public required Guid BranchId { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
}
