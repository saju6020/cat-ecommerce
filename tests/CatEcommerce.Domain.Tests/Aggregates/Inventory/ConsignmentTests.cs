using System.Reflection;
using CatEcommerce.Domain.Aggregates.Inventory;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Tests.Aggregates.Inventory;

/// <summary>HO-IMP-001 §15 phase C -- U-C1 ... U-C5: consignment transitions, HS codes, documents (never removed) and the stale-allocation flag.</summary>
public sealed class ConsignmentTests
{
    private static InventoryActor Actor() => new(TestUserContext.Create(), Guid.NewGuid());

    private static ConsignmentDetailsDto Details(
        string? permit = "IP-1", string? certificate = "HC-1", string? declaration = "CD-1", IReadOnlyList<HsCodeDto>? hs = null, string country = "DE") =>
        new(country, "BL-123456", [Guid.NewGuid()], hs ?? [new HsCodeDto("2309.10", "Cat food")], permit, certificate, declaration, new DateOnly(2026, 11, 1), Actor());

    private static ConsignmentAggregate Planned(ConsignmentDetailsDto? details = null)
    {
        var consignment = new ConsignmentAggregate(Guid.NewGuid());
        consignment.Plan(new ConsignmentPlanDto("CN-2026-000001", Guid.NewGuid(), DateTimeOffset.UtcNow, details ?? Details()));
        return consignment;
    }

    private static ConsignmentAggregate In(ConsignmentStatus status, ConsignmentDetailsDto? details = null)
    {
        var consignment = Planned(details);
        var path = new[] { ConsignmentStatus.InTransit, ConsignmentStatus.AtCustoms, ConsignmentStatus.Cleared };
        foreach (var step in path)
        {
            if (consignment.Status == status) break;
            if (status == ConsignmentStatus.Cancelled) { consignment.Cancel("Cancelled by the test.", DateTimeOffset.UtcNow, Actor()); break; }
            consignment.AdvanceStatus(step, new DateOnly(2026, 10, 7), DateTimeOffset.UtcNow, Actor());
        }

        return consignment;
    }

    // ── U-C1 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    public static IEnumerable<object[]> StatusPairs() =>
        from source in Enumeration.GetAll<ConsignmentStatus>()
        from target in Enumeration.GetAll<ConsignmentStatus>()
        where target != ConsignmentStatus.Cancelled
        select new object[] { source, target };

    [Theory]
    [MemberData(nameof(StatusPairs))]
    public void U_C1_AdvanceStatus_OnlyThePlannedInTransitAtCustomsClearedSequenceIsAllowed(ConsignmentStatus source, ConsignmentStatus to)
    {
        var consignment = In(source);
        var allowed =
            (source == ConsignmentStatus.Planned && to == ConsignmentStatus.InTransit) ||
            (source == ConsignmentStatus.InTransit && to == ConsignmentStatus.AtCustoms) ||
            (source == ConsignmentStatus.AtCustoms && to == ConsignmentStatus.Cleared);

        void Act() => consignment.AdvanceStatus(to, new DateOnly(2026, 10, 8), DateTimeOffset.UtcNow, Actor());

        if (allowed) { Act(); Assert.Equal(to, consignment.Status); }
        else Assert.Equal(BusinessViolationCodes.InvalidConsignmentTransition, Assert.Throws<DomainRuleViolationException>(Act).ErrorCode);
    }

    [Theory]
    [InlineData("Planned", true)]
    [InlineData("InTransit", true)]
    [InlineData("AtCustoms", true)]
    [InlineData("Cleared", false)]
    [InlineData("Cancelled", false)]
    public void U_C1_AnyNonClearedConsignmentCanBeCancelled_OnceWithAReason(string statusName, bool allowed)
    {
        var consignment = In(Enumeration.FromDisplayName<ConsignmentStatus>(statusName));

        void Act() => consignment.Cancel("Shipment withdrawn.", DateTimeOffset.UtcNow, Actor());

        if (allowed) { Act(); Assert.Equal(ConsignmentStatus.Cancelled, consignment.Status); }
        else Assert.Equal(BusinessViolationCodes.InvalidConsignmentTransition, Assert.Throws<DomainRuleViolationException>(Act).ErrorCode);
    }

    [Fact]
    public void U_C1_ClearingWithoutAPermitNumberIsRefused_AndEachMissingNumberIsEnough()
    {
        foreach (var details in new[] { Details(permit: null), Details(certificate: " "), Details(declaration: null) })
        {
            var consignment = In(ConsignmentStatus.AtCustoms, details);

            var ex = Assert.Throws<DomainRuleViolationException>(() =>
                consignment.AdvanceStatus(ConsignmentStatus.Cleared, new DateOnly(2026, 10, 8), DateTimeOffset.UtcNow, Actor()));

            Assert.Equal(BusinessViolationCodes.ClearanceDocumentsMissing, ex.ErrorCode);
            Assert.Equal(ConsignmentStatus.AtCustoms, consignment.Status);
        }
    }

    [Fact]
    public void U_C1_EveryTransitionIsRecordedInTheStatusHistory()
    {
        var consignment = In(ConsignmentStatus.Cleared);

        Assert.Equal(3, consignment.StatusHistory.Count);
        Assert.Equal([1, 2, 3], consignment.StatusHistory.Select(h => h.Sequence));
        Assert.Equal(new DateOnly(2026, 10, 7), consignment.ClearedOn);
    }

    [Fact]
    public void U_C1_APlannedConsignmentKeepsEveryFrImp3Field()
    {
        var consignment = Planned();

        Assert.Equal("DE", consignment.OriginCountryCode);
        Assert.Equal("BL-123456", consignment.ShipmentReference);
        Assert.Equal("IP-1", consignment.ImportPermitNumber);
        Assert.Equal("HC-1", consignment.HealthCertificateNumber);
        Assert.Equal("CD-1", consignment.CustomsDeclarationNumber);
        Assert.Single(consignment.HsCodes);
        Assert.Single(consignment.PurchaseOrderIds);
    }

    // ── U-C2 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("2309.10", true)]
    [InlineData("230910", true)]
    [InlineData("23.09", false)]
    [InlineData("ABCD", false)]
    [InlineData("2309", true)]
    [InlineData("2309.10.90.11", true)]
    [InlineData("2309.10.90.11.22", false)]
    public void U_C2_HsCodePattern(string code, bool valid)
    {
        Assert.Equal(valid, InventoryRules.IsValidHsCode(code));

        void Act() => Planned(Details(hs: [new HsCodeDto(code, null)]));
        if (valid) Act();
        else Assert.Equal(BusinessViolationCodes.InvalidHsCode, Assert.Throws<DomainRuleViolationException>(Act).ErrorCode);
    }

    // ── U-C3 / U-C4 ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

    private static ConsignmentDocumentDto Document(string contentType = "application/pdf", long size = 1024, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), ImportDocumentKind.ImportPermit, "IP-1", new DateOnly(2026, 9, 1), "permit.pdf", "docs/" + Guid.NewGuid(), contentType, size, DateTimeOffset.UtcNow, Actor());

    [Fact]
    public void U_C3_The51stDocumentIsRefused()
    {
        var consignment = Planned();
        for (var i = 0; i < InventoryRules.MaxDocuments; i++) consignment.AddDocument(Document());

        var ex = Assert.Throws<DomainRuleViolationException>(() => consignment.AddDocument(Document()));

        Assert.Equal(BusinessViolationCodes.DocumentLimitExceeded, ex.ErrorCode);
        Assert.Equal(InventoryRules.MaxDocuments, consignment.Documents.Count);
    }

    [Fact]
    public void U_C3_AHtmlDocumentAndA21MbDocumentAreRefused_AndExactly20MbIsAccepted()
    {
        var consignment = Planned();

        Assert.Equal(BusinessViolationCodes.InvalidDocumentContentType,
            Assert.Throws<DomainRuleViolationException>(() => consignment.AddDocument(Document(contentType: "text/html"))).ErrorCode);
        Assert.Equal(BusinessViolationCodes.DocumentTooLarge,
            Assert.Throws<DomainRuleViolationException>(() => consignment.AddDocument(Document(size: 21L * 1024 * 1024))).ErrorCode);

        consignment.AddDocument(Document(size: InventoryRules.MaxDocumentBytes));
        consignment.AddDocument(Document(contentType: "image/png"));
        consignment.AddDocument(Document(contentType: "image/jpeg"));
        Assert.Equal(3, consignment.Documents.Count);
    }

    [Fact]
    public void U_C4_SupersedingADocumentKeepsTheOldOne_AndThereIsNoRemoveMethod()
    {
        var consignment = Planned();
        var first = Document();
        consignment.AddDocument(first);

        var replacement = Document();
        consignment.SupersedeDocument(first.DocumentId, replacement);

        Assert.Equal(2, consignment.Documents.Count);
        var old = consignment.Documents.Single(d => d.Id == first.DocumentId);
        Assert.NotNull(old.SupersededOn);
        Assert.Null(consignment.Documents.Single(d => d.Id == replacement.DocumentId).SupersededOn);

        var methods = typeof(ConsignmentAggregate).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Select(m => m.Name);
        Assert.DoesNotContain(methods, n => n.Contains("Remove", StringComparison.OrdinalIgnoreCase) || n.Contains("Delete", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(BusinessViolationCodes.ConsignmentDocumentNotFound,
            Assert.Throws<DomainRuleViolationException>(() => consignment.SupersedeDocument(first.DocumentId, Document())).ErrorCode);
    }

    [Fact]
    public void U_C4_ACancelledConsignmentIsKeptWithItsDocuments_AndAcceptsNoMore()
    {
        var consignment = Planned();
        consignment.AddDocument(Document());
        consignment.Cancel("Withdrawn.", DateTimeOffset.UtcNow, Actor());

        Assert.Single(consignment.Documents);
        Assert.Throws<DomainRuleViolationException>(() => consignment.AddDocument(Document()));
    }

    // ── U-C5 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    private static ConsignmentCostsDto Costs(decimal freight = 1000m) =>
        new([new CostComponentDto(CostComponentKind.Freight, freight, "BDT", null)], [new ExchangeRateDto("USD", 118.5m, new DateOnly(2026, 10, 7))], Actor());

    [Fact]
    public void U_C5_ReplacingCostsAfterAnAllocationMarksItStale_AndReallocatingClearsIt()
    {
        var consignment = Planned();
        consignment.ReplaceCostsAndRates(Costs());
        Assert.False(consignment.CostsChangedSinceAllocation);

        consignment.RecordLandedCostAllocation(new LandedCostAllocationDto([new LandedCostLineDto(Guid.NewGuid(), 10m, 100m, 1000m, 110m)], DateTimeOffset.UtcNow, Actor()));
        Assert.False(consignment.CostsChangedSinceAllocation);
        Assert.NotNull(consignment.AllocatedOn);

        consignment.ReplaceCostsAndRates(Costs(freight: 1200m));
        Assert.True(consignment.CostsChangedSinceAllocation);

        consignment.RecordLandedCostAllocation(new LandedCostAllocationDto([new LandedCostLineDto(Guid.NewGuid(), 10m, 100m, 1200m, 130m)], DateTimeOffset.UtcNow, Actor()));
        Assert.False(consignment.CostsChangedSinceAllocation);
        Assert.Single(consignment.LandedCost);
    }

    [Fact]
    public void U_C5_OneExchangeRatePerCurrency_AndARateMustBePositiveWithSixDecimals()
    {
        var consignment = Planned();

        Assert.Equal(BusinessViolationCodes.InvalidExchangeRate, Assert.Throws<DomainRuleViolationException>(() => consignment.ReplaceCostsAndRates(
            new ConsignmentCostsDto([], [new ExchangeRateDto("USD", 1m, new DateOnly(2026, 10, 7)), new ExchangeRateDto("USD", 2m, new DateOnly(2026, 10, 7))], Actor()))).ErrorCode);
        Assert.Equal(BusinessViolationCodes.InvalidExchangeRate, Assert.Throws<DomainRuleViolationException>(() => consignment.ReplaceCostsAndRates(
            new ConsignmentCostsDto([], [new ExchangeRateDto("USD", 0m, new DateOnly(2026, 10, 7))], Actor()))).ErrorCode);
        Assert.Equal(BusinessViolationCodes.InvalidExchangeRate, Assert.Throws<DomainRuleViolationException>(() => consignment.ReplaceCostsAndRates(
            new ConsignmentCostsDto([], [new ExchangeRateDto("USD", 1.1234567m, new DateOnly(2026, 10, 7))], Actor()))).ErrorCode);
    }

    [Fact]
    public void U_C1_ASupplierMayDifferButAnInvalidOriginCountryIsRefused()
    {
        Assert.Equal(BusinessViolationCodes.InvalidCountryCode, Assert.Throws<DomainRuleViolationException>(() => Planned(Details(country: "ZZ"))).ErrorCode);
    }
}
