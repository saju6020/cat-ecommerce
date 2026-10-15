using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Surgery;

/// <summary>
/// HO-SRG-001 section 8.11 (ADR-171) -- the price a surgery is booked at. No campaign asked for: the standard price at now. A campaign asked for: the campaign decides
/// (<c>SurgeryCampaignAggregate.Evaluate</c> -- active, dates, branch, price, cap, eligibility note), and each refusal is a <c>CampaignNotApplicable</c> with the reason. Read-only.
/// The cap is judged against a count taken at booking; two bookings racing for the last case can both pass (recorded in docs/backlog/SRG.md).
/// </summary>
public sealed class SurgeryCampaignPricing(CatEcommerceDbContext db) : ISurgeryCampaignPricing
{
    public async Task<SurgeryPricingResult> ResolveAsync(SurgeryPricingRequest request, CancellationToken ct = default)
    {
        if (request.CampaignId is not { } campaignId)
            return new SurgeryPricingResult(new SurgeryPriceSnapshot(request.StandardPrice, SurgeryPricingBasis.Standard, null, null), null);

        var campaign = await db.SurgeryCampaigns.AsNoTracking().FirstOrDefaultAsync(c => c.Id == campaignId && !c.IsMarkedToDelete, ct);
        if (campaign is null)
            return Refuse("That campaign does not exist.");

        var booked = await db.Surgeries.AsNoTracking()
            .CountAsync(s => !s.IsMarkedToDelete && s.PriceSnapshot.CampaignId == campaignId && s.Status != SurgeryStatus.Cancelled, ct);

        var application = campaign.Evaluate(request.BranchId, request.ServiceId, request.LocalDate, request.CampaignEligibilityNote, booked);
        if (!application.Applies)
            return Refuse(application.RefusalReason!);

        return new SurgeryPricingResult(
            new SurgeryPriceSnapshot(application.Price!, SurgeryPricingBasis.Campaign, campaign.Id, request.CampaignEligibilityNote!.Trim()), null);
    }

    private static SurgeryPricingResult Refuse(string reason) =>
        new(null, new SurgeryClash(BusinessViolationCodes.CampaignNotApplicable, "campaignId", reason));
}
