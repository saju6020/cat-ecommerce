using System.Reflection;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Product;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Tests.Common;

/// <summary>HO-SHP-001 §15 -- U-G3 (the back-in-stock template) and the cross-cutting U-X1 / U-X4.</summary>
public sealed class ShopCrossCuttingTests
{
    // ── U-G3 ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void U_G3_BackInStockDefinition_PassesTemplateRules_IsMarketing_AndRequiresTheLinks()
    {
        var definition = NotificationTemplateCatalogue.Find(NotificationTemplateCatalogue.ShopBackInStock);

        Assert.NotNull(definition);
        Assert.Equal(NotificationCategory.Marketing, definition!.Category);
        Assert.Equal(NotificationUrgency.Normal, definition.Urgency);

        var contents = definition.Defaults
            .Select(d => new TemplateContentInput(d.Channel, d.Subject, d.Title, d.Body, d.LinkPath)).ToList();
        Assert.Null(TemplateRules.Validate(definition, contents));

        // productLink is required on every channel; unsubscribeLink (the marketing standard variable)
        // appears on every channel's wording, as TemplateRules rule 4 requires.
        var productLink = definition.Variables.Single(v => v.Name == "productLink");
        Assert.Equal(definition.Channels.OrderBy(c => c.Value), productLink.RequiredOn.OrderBy(c => c.Value));
        foreach (var content in contents)
            Assert.Contains("{{unsubscribeLink}}", content.Body);
        Assert.All(contents.Where(c => c.Channel != NotificationChannel.InApp), c => Assert.Contains("{{productLink}}", c.Body));
        Assert.Equal("{{productLink}}", contents.Single(c => c.Channel == NotificationChannel.InApp).LinkPath);
    }

    [Fact]
    public void U_G3_BackInStock_IsInTheShippedCatalogue()
    {
        Assert.Contains(NotificationTemplateCatalogue.Definitions, d => d.Key == "shop.back-in-stock");
    }

    // ── U-X1 ────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Sanctioned exceptions, all short codes and never free text. <c>ProductTranslationChangedEvent.Locale</c> is a
    /// locale code (<c>^[a-z]{2}(-[A-Z]{2})?$</c>, at most 10 characters), never free text -- §11.5 itself
    /// lists <c>(ProductId, Locale)</c> as the event's shape.</summary>
    private static readonly HashSet<(string Type, string Property)> AllowList =
    [
        ("ProductTranslationChangedEvent", "Locale"),
        // An ISO-3166-1 alpha-2 key (two letters), the origin record's identity -- not free text.
        ("SourcingOriginCreatedEvent", "CountryCode"),
        ("SourcingOriginUpdatedEvent", "CountryCode"),
    ];

    [Fact]
    public void U_X1_NoShopEventCarriesAStringProperty()
    {
        var eventTypes = typeof(ProductCreatedEvent).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(ProductCreatedEvent).Namespace && typeof(Event).IsAssignableFrom(t) && !t.IsAbstract)
            .ToList();

        Assert.NotEmpty(eventTypes);

        var violations = eventTypes
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(p => p.PropertyType == typeof(string) && !AllowList.Contains((t.Name, p.Name)))
                .Select(p => $"{t.Name}.{p.Name}"))
            .ToList();

        Assert.True(violations.Count == 0, "Shop events must carry ids, instants, enums, numbers and booleans only (no names, descriptions, ingredients, review text or notes): " + string.Join(", ", violations));
    }

    // ── U-X4 ────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Products.View", new[] { "Reception", "Inventory", "ShopManager", "Administrator" })]
    [InlineData("Products.Manage", new[] { "ShopManager", "Administrator" })]
    [InlineData("Products.ManagePrices", new[] { "ShopManager", "Administrator" })]
    [InlineData("SourcingOrigins.Manage", new[] { "ShopManager", "Administrator" })]
    [InlineData("Reviews.Moderate", new[] { "ShopManager", "Administrator" })]
    public void U_X4_ShopActionRoles_MatchAppendixDProductCatalogue(string action, string[] expectedRoles)
    {
        Assert.True(ActionRoles.TryGetRoles(action, out var roles));
        Assert.Equal(expectedRoles.OrderBy(r => r), roles.OrderBy(r => r));

        // Vet, Nurse, Caretaker and Accountant have no catalogue access (Appendix D).
        foreach (var excluded in new[] { "Vet", "Nurse", "Caretaker", "Accountant" })
            Assert.DoesNotContain(excluded, roles);
    }

    [Fact]
    public void U_X4_ShopRoleIds_AreTheHO_ACC_001Ones()
    {
        var all = new[] { "Reception", "Nurse", "Vet", "Caretaker", "Inventory", "ShopManager", "Accountant", "Administrator" };
        foreach (var action in new[] { "Products.View", "Products.Manage", "Products.ManagePrices", "SourcingOrigins.Manage", "Reviews.Moderate" })
        {
            Assert.True(ActionRoles.TryGetRoles(action, out var roles));
            Assert.All(roles, r => Assert.Contains(r, all));
        }
    }
}
