using System.Reflection;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Core.Domain;
using Xunit;

namespace CatEcommerce.IntegrationTests.Unit;

/// <summary>
/// HO-XCT-001 U-B6 / ADR-316 -- every context that keeps a customer id on an aggregate has either registered an
/// <see cref="IPersonalDataEraser"/> or been exempted, with a reason, in <see cref="ErasureExemptions"/>. The check reflects over every
/// aggregate under <c>CatEcommerce.Domain.Aggregates</c> for a property named <c>*CustomerId</c>, <c>*CustomerAccountId</c> or <c>OwnerId</c>
/// and fails for a context that is neither. <b>Boarding and Subscriptions are skipped by the owner (2026-10-10)</b>: they have no aggregates,
/// so nothing here names them and nothing here invents their duties.
/// </summary>
public sealed class ErasureCoverageConventionTests : IClassFixture<XctApiFactory>
{
    private readonly XctApiFactory _factory;

    public ErasureCoverageConventionTests(XctApiFactory factory) => _factory = factory;

    /// <summary>
    /// The Domain folder of an aggregate to the context key its duty is registered or exempted under. An aggregate folder missing from this
    /// table fails the test below, so a NEW context cannot slip past U-B6 unnoticed (C-ALL-2).
    /// </summary>
    private static readonly Dictionary<string, string> ContextOfFolder = new(StringComparer.Ordinal)
    {
        ["Account"] = "account",
        ["Administration"] = "administration",
        ["Boarding"] = "boarding",
        ["Billing"] = "billing",
        ["Cat"] = "cat",
        ["Vaccination"] = "cat",
        ["Clinic"] = "appointments",
        ["Visit"] = "consultation",
        ["Prescription"] = "pharmacy",
        ["Inventory"] = "inventory",
        ["Notification"] = "notification",
        ["Order"] = "orders",
        ["Product"] = "shop",
        ["StoredFile"] = "files",
        ["Surgery"] = "surgery",
        // No customer personal data by construction: the audit log keeps actor ids (and is itself immutable, D-5); the erasure ledger IS the
        // customer id and nothing else; the Operations roots hold no customer reference.
        ["Audit"] = "exempt-by-design",
        ["Operations"] = "exempt-by-design",
        ["Validators"] = "exempt-by-design",
    };

    private static bool IsCustomerReference(string propertyName) =>
        propertyName.EndsWith("CustomerId", StringComparison.Ordinal) ||
        propertyName.EndsWith("CustomerAccountId", StringComparison.Ordinal) ||
        propertyName == "OwnerId";

    private static IEnumerable<(string Folder, Type Aggregate)> AggregatesWithACustomerReference() =>
        typeof(CatEcommerce.Domain.Aggregates.Account.CustomerAccountAggregate).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.Namespace is not null && t.Namespace.StartsWith("CatEcommerce.Domain.Aggregates.", StringComparison.Ordinal))
            .Where(t => typeof(AggregateRoot).IsAssignableFrom(t))
            .Where(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Any(p => IsCustomerReference(p.Name)))
            .Select(t => (t.Namespace!["CatEcommerce.Domain.Aggregates.".Length..].Split('.')[0], t));

    [Fact]
    public void U_B6_EveryAggregateFolderThatKeepsACustomerId_IsMappedToAContext()
    {
        var unmapped = AggregatesWithACustomerReference()
            .Where(x => !ContextOfFolder.ContainsKey(x.Folder))
            .Select(x => $"{x.Folder}/{x.Aggregate.Name}")
            .ToList();

        Assert.True(unmapped.Count == 0,
            "These aggregates keep a customer id but their folder is not in ContextOfFolder. Register an IPersonalDataEraser for the context or exempt it in ErasureExemptions.[Area].cs, then map the folder: " + string.Join(", ", unmapped));
    }

    [Fact]
    public void U_B6_EveryContextThatKeepsACustomerId_HasAnEraser_OrAnExemptionWithAReason()
    {
        using var scope = _factory.Services.CreateScope();
        var registered = scope.ServiceProvider.GetServices<IPersonalDataEraser>().Select(e => e.ContextKey).ToHashSet(StringComparer.Ordinal);

        var uncovered = AggregatesWithACustomerReference()
            .Where(x => ContextOfFolder.TryGetValue(x.Folder, out _))
            .Select(x => (x.Folder, Context: ContextOfFolder[x.Folder], x.Aggregate))
            .Where(x => x.Context != "exempt-by-design" && !registered.Contains(x.Context) && !ErasureExemptions.IsExempt(x.Context))
            .Select(x => $"{x.Context} ({x.Aggregate.Name})")
            .Distinct()
            .ToList();

        Assert.True(uncovered.Count == 0,
            "Contexts with a customer id and neither an IPersonalDataEraser (C-ALL-2) nor an ErasureExemptions entry: " + string.Join(", ", uncovered));
    }

    [Fact]
    public void EveryExemption_HasAReason_AndNoContextIsBothExemptAndRegistered()
    {
        using var scope = _factory.Services.CreateScope();
        var registered = scope.ServiceProvider.GetServices<IPersonalDataEraser>().Select(e => e.ContextKey).ToList();

        Assert.All(ErasureExemptions.All, e => Assert.False(string.IsNullOrWhiteSpace(e.Reason), $"{e.ContextKey} is exempt without a reason."));
        Assert.Empty(registered.Where(ErasureExemptions.IsExempt));
        Assert.Equal(registered.Count, registered.Distinct(StringComparer.Ordinal).Count());
    }
}
