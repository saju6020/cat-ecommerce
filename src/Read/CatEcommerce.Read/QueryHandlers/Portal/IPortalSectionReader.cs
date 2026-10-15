namespace CatEcommerce.Read.QueryHandlers.Portal;

/// <summary>
/// HO-POR-001 §8.3, ADR-259 -- the anti-corruption adapter for ONE upstream module. The reader is the only file that knows that
/// module's ViewModel shape; it maps member by member into the portal's own section type and never passes an upstream type
/// through. It takes upstream <c>IQueryHandlerAsync&lt;,&gt;</c> instances by constructor -- never a DbContext, never the dispatcher
/// (ADR-258). It may throw: the composer turns that into an <c>Unavailable</c> section (ADR-260).
/// </summary>
public interface IPortalSectionReader<TSection> where TSection : class
{
    Task<TSection> ReadAsync(PortalCaller caller);
}
