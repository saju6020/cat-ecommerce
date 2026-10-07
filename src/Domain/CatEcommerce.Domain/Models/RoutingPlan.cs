using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Models;

/// <summary>HO-NTF-001 §8.5 -- the output of <c>NotificationRoutingPolicy.Plan</c>.</summary>
public sealed record RoutingPlan(NotificationChannel? PrimaryChannel, bool InboxCopy, NotificationSuppressionReason? SuppressionReason);
