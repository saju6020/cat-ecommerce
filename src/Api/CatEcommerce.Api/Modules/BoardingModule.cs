// Boarding module -- HO-BRD-001 (not yet implemented).
//
// Empty stub so Program.cs never needs another edit when this area is built (ADR-341): the
// integrator adds one line calling this method once, and the area that implements HO-BRD-001 fills
// this method's body with its own command handlers, validators, query handlers and domain event
// handlers -- never touching another area's module file.

using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Fakes.Boarding;

namespace CatEcommerce.Api.Modules;

public static class BoardingModule
{
    public static IServiceCollection AddBoardingModule(this IServiceCollection services, IConfiguration configuration)
    {
        // FAKE until HO-BRD-001 lands -- replace this registration (docs/handover/CONTRACTS.md).
        services.AddScoped<IBoardingBillableSource, InMemoryBoardingBillableSource>();
        services.AddScoped<IBoardingVetSpendingPolicy, InMemoryBoardingVetSpendingPolicy>();
        services.AddScoped<IBoardingStayReferenceReader, InMemoryBoardingStayReferenceReader>();

        return services;
    }
}
