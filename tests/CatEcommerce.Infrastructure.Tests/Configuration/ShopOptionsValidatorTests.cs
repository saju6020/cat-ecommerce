using CatEcommerce.Infrastructure.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace CatEcommerce.Infrastructure.Tests.Configuration;

/// <summary>HO-SHP-001 backlog row 13 -- <c>Shop:InterimAvailabilityFromPrice</c> (a price-derived stand-in
/// for stock) is refused at startup outside Development/Testing.</summary>
public sealed class ShopOptionsValidatorTests
{
    private sealed class Environment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static Microsoft.Extensions.Options.ValidateOptionsResult Validate(string environment, bool interim) =>
        new ShopOptionsValidator(new Environment(environment)).Validate(null, new ShopOptions { InterimAvailabilityFromPrice = interim });

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("")]
    public void InterimAvailabilityOn_OutsideDevelopmentAndTesting_IsRefused(string environment)
    {
        var result = Validate(environment, interim: true);

        Assert.True(result.Failed);
        Assert.Contains("Shop:InterimAvailabilityFromPrice", result.FailureMessage);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public void InterimAvailabilityOn_InDevelopmentAndTesting_IsAllowed(string environment) =>
        Assert.True(Validate(environment, interim: true).Succeeded);

    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public void InterimAvailabilityOff_IsAlwaysAllowed(string environment) =>
        Assert.True(Validate(environment, interim: false).Succeeded);

    [Fact]
    public void TheDefault_IsOff() => Assert.False(new ShopOptions().InterimAvailabilityFromPrice);
}
