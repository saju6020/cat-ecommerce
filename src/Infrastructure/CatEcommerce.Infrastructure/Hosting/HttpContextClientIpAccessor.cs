using System.Net;
using CatEcommerce.Domain.DomainServices;
using Microsoft.AspNetCore.Http;

namespace CatEcommerce.Infrastructure.Hosting;

/// <summary>
/// HO-ACC-001 §12.2 / §20.12. Reads <c>HttpContext.Connection.RemoteIpAddress</c> -- **after**
/// <c>ForwardedHeadersMiddleware</c> has run, so this reflects the trusted-proxy-resolved peer,
/// never a client-supplied header value directly. Never trusts <c>X-Forwarded-For</c> itself.
/// </summary>
public sealed class HttpContextClientIpAccessor(IHttpContextAccessor httpContextAccessor) : IClientIpAccessor
{
    public IPAddress? GetClientIpAddress()
    {
        var remote = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress;
        if (remote is null) return null;

        return remote.IsIPv4MappedToIPv6 ? remote.MapToIPv4() : remote;
    }
}
