using System.Net;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-ACC-001 §8.10 / §12.2. A port so audit writers and command handlers (Application, Domain)
/// never take a direct dependency on <c>HttpContext</c> (ADR-025). Implementation reads the
/// connection's remote address <b>after</b> <c>ForwardedHeadersMiddleware</c> has run, honouring
/// only configured trusted proxies (ADR-040, §20.12).
/// </summary>
public interface IClientIpAccessor
{
    IPAddress? GetClientIpAddress();
}
