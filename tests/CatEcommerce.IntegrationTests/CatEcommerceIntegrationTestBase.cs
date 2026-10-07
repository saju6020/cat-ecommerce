using System.Text.Json;
using Xunit;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// Shared base for every area's real-HTTP integration test base class. Extracted after wave A
/// (HO-CAT-001's <c>CatIntegrationTestBase</c>, HO-ACC-001's <c>AccountsIntegrationTestBase</c>),
/// which had each independently declared the shared JSON options and the factory-exposing pattern.
/// See tests/CatEcommerce.IntegrationTests/README.md for how a new area adds its own.
/// </summary>
public abstract class CatEcommerceIntegrationTestBase<TFactory>(TFactory factory) : IClassFixture<TFactory>
    where TFactory : CatEcommerceApiFactory
{
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Exposed so derived test classes (and this class's own body, and any area-specific
    /// base class in between) need not capture their own copy of the factory (avoids CS9107 -- the
    /// primary constructor parameter is captured once, here).</summary>
    protected TFactory Factory => factory;

    /// <summary>Default client -- override for an area that needs non-default
    /// <c>WebApplicationFactoryClientOptions</c> (e.g. cookie handling).</summary>
    protected virtual HttpClient CreateClient() => Factory.CreateClient();
}
