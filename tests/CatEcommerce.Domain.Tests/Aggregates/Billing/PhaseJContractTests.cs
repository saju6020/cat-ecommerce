using System.Reflection;
using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Tests.Aggregates.Billing;

/// <summary>
/// HO-BIL-001 Phase J (owner decisions and architecture call, 2026-10-14) -- the pure contracts: EMR's line kinds map to Billing's item kinds explicitly and
/// exhaustively, every diagnostic kind has an admin price setting defaulting to 0, and an order freezes the price it was made at.
/// </summary>
public sealed class PhaseJContractTests
{
    private static IEnumerable<T> AllOf<T>() where T : Enumeration =>
        typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.FieldType == typeof(T)).Select(f => (T)f.GetValue(null)!);

    // ── the explicit kind mapping (backlog EMR 43: Procedure/Medicine/Consumable -> Service/Medicine/Product) ──

    [Fact]
    public void EmrKinds_MapToBillingKinds_ExplicitlyAsTheOwnerSpecified()
    {
        Assert.Equal(ItemKind.Service, VisitBillableLineKindMap.ItemKindFor(VisitBillableLineKind.Procedure));
        Assert.Equal(ItemKind.Medicine, VisitBillableLineKindMap.ItemKindFor(VisitBillableLineKind.Medicine));
        Assert.Equal(ItemKind.Product, VisitBillableLineKindMap.ItemKindFor(VisitBillableLineKind.Consumable));
        Assert.Equal(ItemKind.Service, VisitBillableLineKindMap.ItemKindFor(VisitBillableLineKind.Diagnostic));
    }

    [Fact]
    public void EveryVisitLineKind_HasABillingKind_SoANewOneCannotBeBilledByAccident()
    {
        var kinds = AllOf<VisitBillableLineKind>().ToList();
        Assert.Equal(4, kinds.Count);
        Assert.All(kinds, kind => Assert.NotNull(VisitBillableLineKindMap.ItemKindFor(kind)));
    }

    [Fact]
    public void EveryTreatmentKind_HasAVisitLineKind_OfTheSameName()
    {
        foreach (var treatment in AllOf<TreatmentKind>())
            Assert.Equal(treatment.DisplayName, VisitBillableLineKind.Of(treatment).DisplayName);
    }

    [Fact]
    public void AKindWithNoCase_Throws_RatherThanBeingBilledUnderAGuess()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => VisitBillableLineKindMap.ItemKindFor(new VisitBillableLineKind(99, "Unknown")));
        Assert.Throws<ArgumentOutOfRangeException>(() => VisitBillableLineKind.Of(new TreatmentKind(99, "Unknown")));
    }

    // ── diagnostic prices: admin-set, default 0 ──────────────────────────────────────────────────

    [Fact]
    public void EveryDiagnosticKind_HasItsOwnRegisteredPriceSetting_DefaultZero_AdminEditable()
    {
        var keys = new List<string>();
        foreach (var kind in AllOf<DiagnosticKind>())
        {
            var key = DiagnosticPricing.SettingKeyFor(kind);
            var definition = SettingDefinitions.Find(key);

            Assert.NotNull(definition);
            Assert.Equal(SettingType.Decimal, definition!.Type);
            Assert.Equal("0.00", definition.Default);
            Assert.Equal("0.00", definition.Min);
            Assert.False(definition.BranchOverridable);
            Assert.Equal("Clinical", definition.Group);
            keys.Add(key);
        }

        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(5, keys.Count);
    }

    // ── an order freezes the price it was made at ────────────────────────────────────────────────

    private static DiagnosticOrderDto Dto(Money? price, Guid? taxClassId) => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), VisitStatus.Open, DiagnosticKind.BloodWork, null, [], Guid.NewGuid(),
        DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid(), price, taxClassId);

    [Fact]
    public void AnOrder_RecordsThePriceAndTaxClass_ItWasMadeAt()
    {
        var taxClass = Guid.NewGuid();
        var order = new DiagnosticOrderAggregate(Guid.NewGuid());

        order.Order(Dto(new Money(350m, "BDT"), taxClass));

        Assert.Equal(new Money(350m, "BDT"), order.UnitPrice);
        Assert.Equal(taxClass, order.TaxClassId);
    }

    [Fact]
    public void AnOrderMadeWithoutAPrice_HasNone_AndNoTaxClass_NothingIsInvented()
    {
        var order = new DiagnosticOrderAggregate(Guid.NewGuid());

        order.Order(Dto(null, Guid.NewGuid()));

        Assert.Null(order.UnitPrice);
        Assert.Null(order.TaxClassId);
    }
}
