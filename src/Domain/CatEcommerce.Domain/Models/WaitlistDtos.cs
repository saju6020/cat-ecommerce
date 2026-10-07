using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

/// <summary>HO-APT-001 §8.11/§11.3 -- inputs to <c>WaitlistEntryAggregate</c>'s mutation methods.</summary>
public sealed record WaitlistJoinDto(
    Guid BranchId, Guid CustomerId, Guid CatId, Guid ServiceId, DateOnly LocalDate, Guid? PreferredPerformerId,
    DateTimeOffset Now, UserContext UserContext, Guid CorrelationId);

public sealed record WaitlistOfferDto(Guid AppointmentId, DateTimeOffset OfferExpiresOn, UserContext UserContext, Guid CorrelationId);

public sealed record WaitlistClaimDto(UserContext UserContext, Guid CorrelationId);

public sealed record WaitlistLapseDto(UserContext UserContext, Guid CorrelationId);

public sealed record WaitlistWithdrawDto(UserContext UserContext, Guid CorrelationId);

public sealed record WaitlistCloseDto(UserContext UserContext, Guid CorrelationId);
