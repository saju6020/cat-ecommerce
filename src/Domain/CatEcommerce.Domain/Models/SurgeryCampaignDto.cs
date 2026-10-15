using CatEcommerce.Domain.ValueObjects;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

/// <summary>One campaign price. <paramref name="IsSterilisation"/> is the service's own flag, read by the handler: a campaign prices sterilisations only (ADR-171).</summary>
public sealed record CampaignPriceInput(Guid ServiceId, bool IsSterilisation, Money Price);

/// <summary>HO-SRG-001 section 11.3 -- the input of <c>SurgeryCampaignAggregate.Create</c> and <c>Update</c>.</summary>
public sealed record SurgeryCampaignDto(
    string Code, string Name, string Description, DateOnly ValidFrom, DateOnly ValidTo, IReadOnlyList<Guid> BranchIds, IReadOnlyList<CampaignPriceInput> Prices, int? MaxCases,
    DateTimeOffset Now, UserContext UserContext, Guid CorrelationId);

/// <summary>What the campaign said about a booking: the price it gives, or the reason it does not apply.</summary>
public sealed record CampaignApplication(Money? Price, string? RefusalReason)
{
    public bool Applies => Price is not null;
}
