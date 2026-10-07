using CatEcommerce.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace CatEcommerce.Infrastructure.Tests.Configuration;

/// <summary>HO-SHP-001 §12.2 -- <c>LocalisationOptions</c>: at least one locale, each matching the
/// locale pattern, distinct; validated on start. The default is the first entry.</summary>
public sealed class LocalisationOptionsValidatorTests
{
    private static ValidateOptionsResult Validate(params string[] locales) =>
        new LocalisationOptionsValidator().Validate(null, new LocalisationOptions { ContentLocales = [.. locales] });

    [Fact]
    public void TheShippedDefault_IsJustEnglish_AndValid_WhenBoundFromConfiguration()
    {
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Localisation:ContentLocales:0"] = "en" })
            .Build();

        var options = new LocalisationOptions();
        configuration.GetSection(LocalisationOptions.SectionName).Bind(options);

        // The binder appends to an initialised list, so the options type starts empty (see its remarks).
        Assert.Equal(["en"], options.ContentLocales);
        Assert.True(new LocalisationOptionsValidator().Validate(null, options).Succeeded);
    }

    [Fact]
    public void NoLocaleConfigured_FailsLoudly() =>
        Assert.True(new LocalisationOptionsValidator().Validate(null, new LocalisationOptions()).Failed);

    [Fact]
    public void ASecondLocale_IsValid_AndTheFirstIsTheDefault()
    {
        Assert.True(Validate("en", "bn", "pt-BR").Succeeded);

        var settings = new ConfiguredContentLocales(Options.Create(new LocalisationOptions { ContentLocales = ["en", "bn"] }));
        Assert.Equal("en", settings.DefaultLocale);
        Assert.Equal(["en", "bn"], settings.EnabledLocales);
    }

    public static TheoryData<string[]> BadConfigurations => new()
    {
        Array.Empty<string>(),
        new[] { "english" },
        new[] { "EN" },
        new[] { "en", "en" },
        new[] { "en", "" },
        new[] { "en", "bn", "fr", "de", "es", "it" },
    };

    [Theory]
    [MemberData(nameof(BadConfigurations))]
    public void BadConfiguration_FailsValidation(string[] locales) => Assert.True(Validate(locales).Failed);
}
