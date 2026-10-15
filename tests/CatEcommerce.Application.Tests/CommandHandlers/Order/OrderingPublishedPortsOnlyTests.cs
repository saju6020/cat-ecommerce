using System.Reflection;
using CatEcommerce.Application.CommandHandlers.Order;
using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.DomainServices;

namespace CatEcommerce.Application.Tests.CommandHandlers.Order;

/// <summary>
/// HO-ORD-001 ARCHITECTURE gate (2026-10-09, owner decisions 2 to 5) as a rule that fails the build: Ordering's handlers talk to Inventory and Billing only through their
/// published ports. Not Inventory's internal position reader or clock, not Billing's finder, not Billing's aggregates through a repository, and no numbering lock of its own.
/// </summary>
public sealed class OrderingPublishedPortsOnlyTests
{
    private static readonly HashSet<Type> Forbidden =
    [
        typeof(IStockPositionReader), typeof(IInventoryClock), typeof(IBillingFinder), typeof(IDocumentNumbering),
        typeof(InvoiceAggregate), typeof(PaymentAggregate), typeof(CreditNoteAggregate), typeof(DocumentNumberSeriesAggregate),
    ];

    private static IEnumerable<Type> Mentioned(Type type)
    {
        yield return type;
        if (type.IsGenericType)
            foreach (var argument in type.GetGenericArguments())
                foreach (var inner in Mentioned(argument))
                    yield return inner;
    }

    private static IEnumerable<Type> OrderingHandlerTypes() =>
        typeof(PlaceOrderCommandHandler).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(PlaceOrderCommandHandler).Namespace && !t.IsNested && !t.IsInterface);

    [Fact]
    public void NoOrderingHandlerOrHelper_TakesAnInventoryOrBillingInternal_AsAConstructorDependency()
    {
        var offenders = new List<string>();
        foreach (var type in OrderingHandlerTypes())
            foreach (var constructor in type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                foreach (var parameter in constructor.GetParameters())
                    foreach (var mentioned in Mentioned(parameter.ParameterType))
                        if (Forbidden.Contains(mentioned)) offenders.Add($"{type.Name}({parameter.ParameterType.Name} {parameter.Name})");

        Assert.True(offenders.Count == 0, "Ordering reaches into another area's internals: " + string.Join("; ", offenders.Distinct()));
    }

    [Fact]
    public void NoOrderingStaticHelper_TakesAnInventoryOrBillingInternal_AsAParameter()
    {
        var offenders = new List<string>();
        foreach (var type in OrderingHandlerTypes().Where(t => t.IsAbstract && t.IsSealed))
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                foreach (var parameter in method.GetParameters())
                    foreach (var mentioned in Mentioned(parameter.ParameterType))
                        if (Forbidden.Contains(mentioned)) offenders.Add($"{type.Name}.{method.Name}({parameter.ParameterType.Name})");

        Assert.True(offenders.Count == 0, "Ordering reaches into another area's internals: " + string.Join("; ", offenders.Distinct()));
    }

    [Fact]
    public void OrderingsTransactionalScope_IsBillingsPublishedScope_PlusItsOwnStockRaceTranslation_AndNothingMore()
    {
        var members = typeof(ITransactionalScope).GetMethods().Select(m => m.Name).OrderBy(n => n).ToArray();

        Assert.Equal(["FlushAsync", "RunAsync"], members);      // no lock, no series name: Billing owns the numbering lock (ADR-179 amended, ADR-215)
    }
}
