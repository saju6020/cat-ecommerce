using CatEcommerce.Application.CommandHandlers.Operations;
using CatEcommerce.Application.CommandHandlers.Validators;
using CatEcommerce.Application.Commands.Operations;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Operations;
using CatEcommerce.Read.QueryHandlers.Operations;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Time.Testing;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Application.Tests.CommandHandlers.Operations;

/// <summary>HO-XCT-001 U-E1, U-E2, U-E3.</summary>
public sealed class WebVitalTests
{
    private static WebVitalSampleInput Sample(string route = "/shop", string metric = "LCP", decimal value = 1800m, string nav = "navigate") =>
        new(metric, value, route, nav, "4g", "Wide");

    private static RecordWebVitalsCommand Command(params WebVitalSampleInput[] samples) => new()
    {
        BatchId = Guid.NewGuid(), BuildId = "build-1.2", Samples = samples, UserContext = new UserContext(Guid.NewGuid(), Guid.NewGuid()), CorrelationId = Guid.NewGuid()
    };

    // U-E1
    [Theory]
    [InlineData("/shop?q=1")]
    [InlineData("/a@b")]
    [InlineData("/a#b")]
    [InlineData("shop")]
    public void U_E1_ARouteWithAQueryAnAtSignAFragmentOrNoLeadingSlash_IsInvalidWebVitalsPayload(string route) =>
        AssertInvalid(Command(Sample(route)));

    [Fact]
    public void U_E1_TooLongARoute_TooManySamples_AnUnknownMetric_AndANonNavigationType_AreInvalid()
    {
        AssertInvalid(Command(Sample("/" + new string('a', 200))));
        AssertInvalid(Command(Enumerable.Range(0, 11).Select(_ => Sample()).ToArray()));
        AssertInvalid(Command());
        AssertInvalid(Command(Sample(metric: "FID")));
        AssertInvalid(Command(Sample(nav: "teleport")));
        AssertInvalid(Command(Sample(value: -1m)));
    }

    [Fact]
    public void U_E1_AValidBatch_Passes() => Assert.True(new RecordWebVitalsCommandValidator().Validate(Command(Sample(), Sample("/shop/[slug]", "CLS", 0.02m))).IsValid);

    private static void AssertInvalid(RecordWebVitalsCommand command)
    {
        var result = new RecordWebVitalsCommandValidator().Validate(command);
        Assert.False(result.IsValid);
        Assert.All(result.Errors, e => Assert.Equal(BusinessViolationCodes.InvalidWebVitalsPayload, e.ErrorCode));
    }

    // U-E2
    [Fact]
    public async Task U_E2_ASignedInCallersContext_NeverReachesTheRow()
    {
        var batches = new FakeAggregateRootRepository<WebVitalBatchAggregate>();
        var handler = new RecordWebVitalsCommandHandler(batches, new FakeTimeProvider(DateTimeOffset.UtcNow));
        var command = Command(Sample());                       // carries a real customer context

        var response = await handler.HandleAsync(command);

        Assert.True(response.IsSuccess);
        var saved = Assert.Single(batches.Saved);
        Assert.Equal(Guid.Empty, saved.CreatedBy);
        Assert.Empty(saved.Events);
    }

    [Fact]
    public async Task AnOutOfRangeValue_IsReturnedAsAViolation_NotThrown()
    {
        var handler = new RecordWebVitalsCommandHandler(new FakeAggregateRootRepository<WebVitalBatchAggregate>(), new FakeTimeProvider(DateTimeOffset.UtcNow));

        var response = await handler.HandleAsync(Command(Sample(value: 999_999m)));

        Assert.False(response.IsSuccess);
        Assert.Contains(response.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.InvalidWebVitalsPayload);
    }

    // U-E3
    [Theory]
    [InlineData(49, 2400, "InsufficientData")]
    [InlineData(50, 2400, "Pass")]
    [InlineData(50, 2500, "Pass")]
    [InlineData(50, 2600, "Fail")]
    public void U_E3_TheLcpVerdict_NeedsFiftySamples_AndPassesAtOrUnderTheBudget(int samples, int p75, string expected) =>
        Assert.Equal(expected, GetWebVitalsSummaryQueryHandler.Verdict(WebVitalMetric.LCP, samples, p75));

    [Fact]
    public void TheInpAndClsBudgetsApply() 
    {
        Assert.Equal("Fail", GetWebVitalsSummaryQueryHandler.Verdict(WebVitalMetric.INP, 60, 250));
        Assert.Equal("Pass", GetWebVitalsSummaryQueryHandler.Verdict(WebVitalMetric.CLS, 60, 0.1m));
        Assert.Equal("Fail", GetWebVitalsSummaryQueryHandler.Verdict(WebVitalMetric.CLS, 60, 0.11m));
    }
}

/// <summary>HO-XCT-001 U-F7.</summary>
public sealed class RestoreTestDueDateTests
{
    [Fact]
    public void U_F7_TheDueDate_IsTheLastPassedTestPlusTheInterval()
    {
        var due = GetBackupStatusQueryHandler.NextDueOn(new DateOnly(2026, 7, 1), 92, new DateOnly(2026, 9, 1));

        Assert.Equal(new DateOnly(2026, 10, 1), due);
    }

    [Fact]
    public void NoPassedTestEver_IsDueToday() =>
        Assert.Equal(new DateOnly(2026, 10, 12), GetBackupStatusQueryHandler.NextDueOn(null, 92, new DateOnly(2026, 10, 12)));
}
