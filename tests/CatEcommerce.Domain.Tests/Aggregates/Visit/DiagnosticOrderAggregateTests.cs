using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Domain.Events.Visit;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Tests.Aggregates.Visit;

/// <summary>HO-EMR-001 §15 U-D2..U-D5 -- the diagnostic order root.</summary>
public sealed class DiagnosticOrderAggregateTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 7);

    private static DiagnosticOrderAggregate Order(DiagnosticKind? kind = null, VisitStatus? visitStatus = null, string? description = null)
    {
        var order = new DiagnosticOrderAggregate(Guid.NewGuid());
        order.Order(new DiagnosticOrderDto(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), visitStatus ?? VisitStatus.Open, kind ?? DiagnosticKind.BloodWork, description,
            [new RequestedAnalyteDto(Guid.NewGuid(), "CREA", "Creatinine")], Guid.NewGuid(), Now, TestUserContext.Create(), Guid.NewGuid()));
        return order;
    }

    private static ResultValueDto Numeric(decimal value, decimal? low, decimal? high, string name = "Creatinine", string? unit = "mg/dL") =>
        new(Guid.NewGuid(), null, name, ResultValueKind.Numeric, value, null, unit, low, high, null);

    private static ResultValueDto Qualitative(string value, string? normal) =>
        new(Guid.NewGuid(), null, "FeLV", ResultValueKind.Qualitative, null, value, null, null, null, normal);

    private static DiagnosticResultSetDto Results(IReadOnlyList<ResultValueDto> values, Guid? supersedes = null, DateOnly? reportedOn = null) =>
        new(Guid.NewGuid(), reportedOn ?? Today, null, supersedes, values, Guid.NewGuid(), Today, Now, TestUserContext.Create(), Guid.NewGuid());

    [Fact]
    public void Order_IsPending_ListsTheAnalytes_AndRaisesTheEvent()
    {
        var order = Order();

        Assert.Equal(DiagnosticOrderStatus.Pending, order.Status);
        Assert.Single(order.RequestedAnalytes);
        Assert.Single(order.Events.OfType<DiagnosticOrderedEvent>());
    }

    [Fact]
    public void Order_OnAFinalisedVisit_IsRefused()
    {
        var violation = Assert.Throws<DomainRuleViolationException>(() => Order(visitStatus: VisitStatus.Finalised));
        Assert.Equal(BusinessViolationCodes.VisitFinalised, violation.ErrorCode);
    }

    [Theory]
    [InlineData("Imaging")]
    [InlineData("Other")]
    public void Order_ImagingAndOther_NeedADescription(string kind)
    {
        var violation = Assert.Throws<DomainRuleViolationException>(() => Order(Enumeration.FromDisplayName<DiagnosticKind>(kind)));
        Assert.Equal(BusinessViolationCodes.DiagnosticDescriptionRequired, violation.ErrorCode);

        Assert.Equal(DiagnosticKind.Imaging, Order(DiagnosticKind.Imaging, description: "Thoracic radiograph").Kind);
    }

    [Fact]
    public void RecordResults_FlagsEachValueAgainstTheRangeStoredOnIt_AndMarksTheOrderResulted()
    {
        var order = Order();

        order.RecordResults(Results([
            Numeric(4.9m, 5m, 10m),
            Numeric(10.0m, 5m, 10m),
            Numeric(10.1m, 5m, 10m),
            Numeric(7m, null, null),
            Qualitative("Positive", "Negative"),
        ]));

        var flags = order.ResultSets.Single().Values.Select(v => v.Flag).ToList();
        Assert.Equal([ResultFlag.Low, ResultFlag.Normal, ResultFlag.High, ResultFlag.NotAssessed, ResultFlag.Abnormal], flags);
        Assert.Equal(DiagnosticOrderStatus.Resulted, order.Status);
        Assert.True(Assert.Single(order.Events.OfType<DiagnosticResultsRecordedEvent>()).HasAbnormal);
    }

    [Fact]
    public void RecordResults_SupersedingASetOfAnotherOrder_IsResultSetNotFound()
    {
        var order = Order();

        var violation = Assert.Throws<DomainRuleViolationException>(() => order.RecordResults(Results([Numeric(1m, 0m, 2m)], supersedes: Guid.NewGuid())));

        Assert.Equal(BusinessViolationCodes.ResultSetNotFound, violation.ErrorCode);
    }

    [Fact]
    public void RecordResults_ACorrection_IsANewSetNamingTheOldOne_AndTheOldOneStays()
    {
        var order = Order();
        order.RecordResults(Results([Numeric(1m, 0m, 2m)]));
        var first = order.ResultSets.Single().Id;

        order.RecordResults(Results([Numeric(1.5m, 0m, 2m)], supersedes: first));

        Assert.Equal(2, order.ResultSets.Count);
        Assert.Equal(first, order.ResultSets[1].SupersedesResultSetId);
    }

    [Fact]
    public void RecordResults_ANumericValueWithNoUnit_OrNoValues_OrAFutureDate_IsRefused()
    {
        var order = Order();

        Assert.Equal(BusinessViolationCodes.InvalidDiagnosticResult,
            Assert.Throws<DomainRuleViolationException>(() => order.RecordResults(Results([Numeric(1m, 0m, 2m, unit: null)]))).ErrorCode);
        Assert.Equal(BusinessViolationCodes.InvalidDiagnosticResult,
            Assert.Throws<DomainRuleViolationException>(() => order.RecordResults(Results([]))).ErrorCode);
        Assert.Equal(BusinessViolationCodes.ClinicalDateInFuture,
            Assert.Throws<DomainRuleViolationException>(() => order.RecordResults(Results([Numeric(1m, 0m, 2m)], reportedOn: Today.AddDays(1)))).ErrorCode);
        Assert.Equal(BusinessViolationCodes.InvalidReferenceRange,
            Assert.Throws<DomainRuleViolationException>(() => order.RecordResults(Results([Numeric(1m, 5m, 2m)]))).ErrorCode);
    }

    [Fact]
    public void RecordResults_ElevenSets_IsRefused()
    {
        var order = Order();
        for (var i = 0; i < DiagnosticRules.MaxResultSets; i++) order.RecordResults(Results([Numeric(1m, 0m, 2m)]));

        Assert.Equal(BusinessViolationCodes.ClinicalLimitExceeded,
            Assert.Throws<DomainRuleViolationException>(() => order.RecordResults(Results([Numeric(1m, 0m, 2m)]))).ErrorCode);
    }

    [Fact]
    public void Cancel_ARepliedOrder_IsRefused_APendingOrderIsCancelled()
    {
        var withResults = Order();
        withResults.RecordResults(Results([Numeric(1m, 0m, 2m)]));
        var cancel = new DiagnosticCancelDto("Duplicate", Guid.NewGuid(), Now, TestUserContext.Create(), Guid.NewGuid());

        Assert.Equal(BusinessViolationCodes.DiagnosticOrderHasResults, Assert.Throws<DomainRuleViolationException>(() => withResults.Cancel(cancel)).ErrorCode);

        var pending = Order();
        pending.Cancel(cancel);
        Assert.Equal(DiagnosticOrderStatus.Cancelled, pending.Status);
        Assert.Equal(BusinessViolationCodes.DiagnosticOrderCancelled,
            Assert.Throws<DomainRuleViolationException>(() => pending.RecordResults(Results([Numeric(1m, 0m, 2m)]))).ErrorCode);
    }

    [Fact]
    public void Cancel_AnOrderWithAFile_IsRefused()
    {
        var order = Order();
        order.AttachFile(new DiagnosticAttachmentDto(Guid.NewGuid(), Guid.NewGuid(), Today, "report", Guid.NewGuid(), Today, Now, TestUserContext.Create(), Guid.NewGuid()));

        var cancel = new DiagnosticCancelDto("Duplicate", Guid.NewGuid(), Now, TestUserContext.Create(), Guid.NewGuid());
        Assert.Equal(BusinessViolationCodes.DiagnosticOrderHasResults, Assert.Throws<DomainRuleViolationException>(() => order.Cancel(cancel)).ErrorCode);
    }

    [Fact]
    public void AttachFile_TheTwentyFirst_AndAFutureResultDate_AreRefused()
    {
        var order = Order();
        DiagnosticAttachmentDto Attachment(DateOnly date) =>
            new(Guid.NewGuid(), Guid.NewGuid(), date, null, Guid.NewGuid(), Today, Now, TestUserContext.Create(), Guid.NewGuid());

        Assert.Equal(BusinessViolationCodes.ClinicalDateInFuture, Assert.Throws<DomainRuleViolationException>(() => order.AttachFile(Attachment(Today.AddDays(1)))).ErrorCode);

        for (var i = 0; i < DiagnosticRules.MaxAttachments; i++) order.AttachFile(Attachment(Today));

        Assert.Equal(BusinessViolationCodes.ClinicalLimitExceeded, Assert.Throws<DomainRuleViolationException>(() => order.AttachFile(Attachment(Today))).ErrorCode);
    }

    /// <summary>U-D5 -- results are accepted after the visit is finalised: the aggregate takes no visit
    /// status for them, because the lab report arrives days later (ADR-124).</summary>
    [Fact]
    public void RecordResults_TakesNoVisitStatus_SoItWorksAfterTheVisitIsFinalised()
    {
        var order = Order();
        // The order was placed while the visit was open; nothing about a later finalisation reaches it.
        order.RecordResults(Results([Numeric(1m, 0m, 2m)]));

        Assert.Equal(DiagnosticOrderStatus.Resulted, order.Status);
    }
}
