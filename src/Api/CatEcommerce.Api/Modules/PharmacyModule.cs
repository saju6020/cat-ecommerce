// Pharmacy module -- HO-PHM-001 (not yet implemented).
//
// Empty stub so Program.cs never needs another edit when this area is built (ADR-341): the
// integrator adds one line calling this method once, and the area that implements HO-PHM-001 fills
// this method's body with its own command handlers, validators, query handlers and domain event
// handlers -- never touching another area's module file.

using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Fakes.Pharmacy;

namespace CatEcommerce.Api.Modules;

public static class PharmacyModule
{
    public static IServiceCollection AddPharmacyModule(this IServiceCollection services, IConfiguration configuration)
    {
        // FAKE until HO-PHM-001 lands -- replace this registration (docs/handover/CONTRACTS.md).
        // Fail-closed (README C-4, ADR-084): denies every diet-authorisation check.
        services.AddScoped<IDietAuthorisationGate, DenyAllDietAuthorisationGate>();

        return services;
    }
}
