using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Surgery;

/// <summary>US-SRG-10 -- surgeries in Recovery at the branch, polled every 30 s: at most TWO SQL commands whatever the number of entries (I-F2).</summary>
public sealed class GetRecoveryBoardQuery : Query<RecoveryBoardViewModel>
{
    public required Guid BranchId { get; init; }
}

/// <summary>US-SRG-07 -- the protocol-filled discharge form: the instructions and the follow-ups with their windows. Null when the surgery is not the branch's.</summary>
public sealed class GetDischargeDraftQuery : Query<DischargeDraftViewModel?>
{
    public required Guid SurgeryId { get; init; }
    public required Guid BranchId { get; init; }
}

/// <summary>The portal list: the caller's own surgeries (<c>upcoming</c> or <c>past</c>).</summary>
public sealed class GetCustomerSurgeriesQuery : Query<PagedResult<CustomerSurgerySummaryViewModel>>
{
    public required Guid CustomerId { get; init; }
    public string Scope { get; init; } = "upcoming";
}

/// <summary>The portal detail. Another customer's surgery is "not found" (null).</summary>
public sealed class GetCustomerSurgeryDetailQuery : Query<CustomerSurgeryDetailViewModel?>
{
    public required Guid CustomerId { get; init; }
    public required Guid SurgeryId { get; init; }
}

/// <summary>HO-EMR-001 history contract (US-CLN-13) -- the surgeries on a cat's clinical history, newest first. No endpoint: EMR consumes the query.</summary>
public sealed class GetCatSurgeriesQuery : Query<PagedResult<CatSurgeryHistoryViewModel>>
{
    public required Guid CatId { get; init; }
}

/// <summary>HO-BIL-001 contract (Phase J, deferred there) -- the price snapshot plus the drug entries with SKU and quantity, effective after corrections. No endpoint.</summary>
public sealed class GetSurgeryChargeableItemsQuery : Query<SurgeryChargeableItemsViewModel?>
{
    public required Guid SurgeryId { get; init; }
}
