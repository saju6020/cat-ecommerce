// Portal module -- HO-POR-001.
//
// The portal is a window, not a module (ADR-257): no aggregate, table, event or event handler, so nothing but query-side
// registrations lives here. Program.cs already calls AddPortalModule(...), so this file is the only one that changes per phase
// (ADR-341).
//
// Count against HO-POR-001 §12.5 -- built so far (Phases A, B, C, D orders reader only):
//   Q1 handler .................. 1   (GetPortalDashboardQueryHandler)
//   PortalSectionComposer ....... 1
//   Section readers ............. 7   (cats, appointments, profile; visits (EMR); balances (BIL); prescriptions (PHM); orders (ORD))
// Not registered, by design: re-order (C1 + validator + handler) -- RETIRED 2026-10-10 (ADR-263); the customer uses Orders' own
// POST /api/customers/me/orders/{id}/reorder, so Orders' eligibility applies by construction. Not built yet: stays (HO-BRD-001) and subscriptions (HO-SUB-001) -- not built.
// Q2 (stay diary) is retired (README C-6).
//
// ValidateScopes/ValidateOnBuild stay on: every upstream IQueryHandlerAsync<,> a reader takes by constructor is therefore proven
// registered at startup (I-A1), not on a customer's request.

using CatEcommerce.Read.Queries.Portal;
using CatEcommerce.Read.QueryHandlers.Portal;
using CatEcommerce.Read.QueryHandlers.Portal.Sections;
using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Api.Modules;

public static class PortalModule
{
    public static IServiceCollection AddPortalModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IQueryHandlerAsync<GetPortalDashboardQuery, PortalDashboardViewModel>, GetPortalDashboardQueryHandler>();
        services.AddScoped<PortalSectionComposer>();

        services.AddScoped<IPortalSectionReader<PortalCatsViewModel>, CatsSectionReader>();
        services.AddScoped<IPortalSectionReader<PortalAppointmentsViewModel>, AppointmentsSectionReader>();
        services.AddScoped<IPortalSectionReader<PortalCustomerViewModel>, ProfileReader>();
        services.AddScoped<IPortalSectionReader<PortalVisitsViewModel>, VisitsSectionReader>();
        services.AddScoped<IPortalSectionReader<PortalBalancesViewModel>, BalancesSectionReader>();
        services.AddScoped<IPortalSectionReader<PortalPrescriptionsViewModel>, PrescriptionsSectionReader>();
        services.AddScoped<IPortalSectionReader<PortalOrdersViewModel>, OrdersSectionReader>();

        return services;
    }
}
