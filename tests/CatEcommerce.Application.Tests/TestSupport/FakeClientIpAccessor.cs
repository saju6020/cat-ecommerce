using System.Net;
using CatEcommerce.Domain.DomainServices;

namespace CatEcommerce.Application.Tests.TestSupport;

public sealed class FakeClientIpAccessor(IPAddress? address = null) : IClientIpAccessor
{
    public IPAddress? GetClientIpAddress() => address;
}
