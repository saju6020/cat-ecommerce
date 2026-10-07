// Billing module -- HO-BIL-001 (not yet implemented).
//
// Empty stub so Program.cs never needs another edit when this area is built (ADR-341): the
// integrator adds one line calling this method once, and the area that implements HO-BIL-001 fills
// this method's body with its own command handlers, validators, query handlers and domain event
// handlers -- never touching another area's module file.

using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Fakes.Billing;

namespace CatEcommerce.Api.Modules;

public static class BillingModule
{
    public static IServiceCollection AddBillingModule(this IServiceCollection services, IConfiguration configuration)
    {
        // FAKE until HO-BIL-001 lands -- replace this registration (docs/handover/CONTRACTS.md).
        // Scoped to match the real EfBillableChargeRecorder's destined lifetime (.ai/di-registration-guide.md §4).
        services.AddScoped<IBillableChargeRecorder, InMemoryBillableChargeRecorder>();

        return services;
    }
}
