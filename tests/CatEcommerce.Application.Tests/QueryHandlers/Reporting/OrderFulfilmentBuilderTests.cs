using CatEcommerce.Infrastructure.Persistence.ReportingViews;
using CatEcommerce.Read.QueryHandlers.Reporting.Sources;
using CatEcommerce.Shared.Common.Enums;
using Xunit;

namespace CatEcommerce.Application.Tests.QueryHandlers.Reporting;

/// <summary>HO-RPT-001 section 15 H -- U-H3: the order fulfilment age and the dispatch service level (24 hours). Pure.</summary>
public sealed class OrderFulfilmentBuilderTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-14T12:00:00Z");

    private static RvOrderRow Order(OrderStatus status, double confirmedHoursAgo, double? dispatchedAfterHours = null) => new()
    {
        OrderNumber = "PC-1", Status = status.Value, PlacedOn = Now.AddHours(-confirmedHoursAgo), ConfirmedOn = Now.AddHours(-confirmedHoursAgo),
        DispatchedOn = dispatchedAfterHours is { } h ? Now.AddHours(-confirmedHoursAgo + h) : null, Channel = OrderChannel.Online.Value,
    };

    [Theory]
    [InlineData(23, false)]
    [InlineData(24, false)]
    [InlineData(25, true)]
    public void U_H3_AnOpenOrder_BreachesTheServiceLevelAfter24HoursFromConfirmation(double hours, bool breached) =>
        Assert.Equal(breached, OrderFulfilmentBuilder.Breached(Order(OrderStatus.Confirmed, hours), Now, 24));

    [Fact]
    public void U_H3_ADispatchedOrder_IsJudgedAtItsDispatch_NotAtNow()
    {
        Assert.False(OrderFulfilmentBuilder.Breached(Order(OrderStatus.Dispatched, 100, dispatchedAfterHours: 20), Now, 24));
        Assert.True(OrderFulfilmentBuilder.Breached(Order(OrderStatus.Dispatched, 100, dispatchedAfterHours: 30), Now, 24));
    }

    [Fact]
    public void U_H3_ACancelledOrder_AndOneStillAwaitingItsTransfer_NeverBreach_AndTheAgeIsReported()
    {
        var cancelled = Order(OrderStatus.Cancelled, 80);
        var awaiting = Order(OrderStatus.PendingPayment, 80);
        awaiting.ConfirmedOn = null;

        Assert.False(OrderFulfilmentBuilder.Breached(cancelled, Now, 24));
        Assert.False(OrderFulfilmentBuilder.Breached(awaiting, Now, 24));

        var rows = OrderFulfilmentBuilder.Build([awaiting], Now, 24);
        Assert.Equal(80L, rows.Single()["ageHours"]);
        Assert.Equal("PendingPayment", rows.Single()["status"]);
    }
}
