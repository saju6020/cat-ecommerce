using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Portal;

/// <summary>HO-POR-001 Q1 -- GET /api/portal/dashboard. Deliberately has NO customer-id property: the caller is
/// <c>UserContext.UserId</c> of a customer session and nothing a client sends can change it (ADR-261).</summary>
public sealed class GetPortalDashboardQuery : Query<PortalDashboardViewModel>
{
}
