using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace CatEcommerce.Infrastructure.Tests.Files;

/// <summary>Minimal hand-written <see cref="IHostEnvironment"/> for constructor-guard tests --
/// no mocking library is in this slice's allowed scope, matching the rest of the solution's test
/// doubles (e.g. <c>FakeAggregateRootRepository</c>).</summary>
internal sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = environmentName;
    public string ApplicationName { get; set; } = "CatEcommerce.Infrastructure.Tests";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
