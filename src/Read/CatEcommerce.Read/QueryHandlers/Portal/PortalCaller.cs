using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Read.QueryHandlers.Portal;

/// <summary>HO-POR-001 §8.3 -- who is asking, taken from the portal query's <see cref="UserContext"/> and nowhere else (ADR-261).
/// Every upstream query a reader builds carries all three members.</summary>
public sealed record PortalCaller(Guid CustomerId, UserContext UserContext, Guid CorrelationId);
