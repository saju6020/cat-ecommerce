// Surgery module -- HO-SRG-001 (not yet implemented).
//
// Empty stub so Program.cs never needs another edit when this area is built (ADR-341): the
// integrator adds one line calling this method once, and the area that implements HO-SRG-001 fills
// this method's body with its own command handlers, validators, query handlers and domain event
// handlers -- never touching another area's module file.

namespace CatEcommerce.Api.Modules;

public static class SurgeryModule
{
    public static IServiceCollection AddSurgeryModule(this IServiceCollection services, IConfiguration configuration)
    {
        // HO-SRG-001: no registrations yet.
        return services;
    }
}
