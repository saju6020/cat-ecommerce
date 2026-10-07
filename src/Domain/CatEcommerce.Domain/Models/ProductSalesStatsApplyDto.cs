using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

/// <summary>HO-SHP-001 section 8.7 / section 11.1 C10. Pushed by HO-ORD-001; ADR-082 narrowed to
/// this copy alone. Monotonic by <see cref="AsOf"/> -- a stale push (older or equal AsOf) is
/// ignored, never overwriting a fresher figure. <see cref="Now"/> is the caller's clock: an AsOf beyond it
/// (plus the skew tolerance) is refused, since it would freeze every later push (row 35).</summary>
public sealed record ProductSalesStatsApplyDto(int UnitsSold90d, DateTimeOffset AsOf, DateTimeOffset Now, UserContext UserContext, Guid CorrelationId);
