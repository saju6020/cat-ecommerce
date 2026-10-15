using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Tests.Aggregates.Billing;

/// <summary>HO-BIL-001 §15 group B (U-B1, U-B2) -- a charge validates its fields, defaults its business line
/// from its source, and is immutable except <c>Void</c> (ADR-178).</summary>
public sealed class ChargeAggregateTests
{
    private static ChargeRequestDto Request(
        decimal quantity = 1m, decimal price = 800m, decimal discount = 0m, string description = "Consultation",
        ChargeSourceKind? source = null, BusinessLine? line = null, string currency = "BDT", string sourceLineRef = "service") =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), line ?? ChargeRules.DefaultBusinessLine(source ?? ChargeSourceKind.Appointment), ItemKind.Service,
            source ?? ChargeSourceKind.Appointment, Guid.NewGuid(), sourceLineRef, null, null, description, quantity,
            new Money(price, currency), new Money(discount, currency), Guid.NewGuid(), new DateOnly(2026, 10, 7));

    private static ChargeRecordContext Context(string currency = "BDT") => new(currency, DateTimeOffset.UtcNow, BillingTestData.Actor());

    private static ChargeAggregate Record(ChargeRequestDto request, string currency = "BDT")
    {
        var charge = new ChargeAggregate(Guid.NewGuid());
        charge.Record(request, Context(currency));
        return charge;
    }

    private static string ErrorOf(Action act) => Assert.Throws<DomainRuleViolationException>(act).ErrorCode;

    [Fact]
    public void U_B1_ValidCharge_IsRecordedActive_WithItsSnapshot()
    {
        var request = Request(quantity: 2m, price: 400m, discount: 100m);
        var charge = Record(request);

        Assert.Equal(ChargeStatus.Active, charge.Status);
        Assert.Equal(2m, charge.Quantity);
        Assert.Equal(new Money(400m, "BDT"), charge.UnitPrice);
        Assert.Equal(new Money(100m, "BDT"), charge.LineDiscount);
        Assert.Equal(request.ServiceDate, charge.ServiceDate);
        Assert.Empty(charge.Events); // charges raise no events (F31(c))
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void U_B1_QuantityMustBePositive(int quantity)
    {
        Assert.Equal(BusinessViolationCodes.InvalidQuantity, ErrorOf(() => Record(Request(quantity: quantity))));
    }

    [Fact]
    public void U_B1_QuantityHasAtMostThreeDecimals()
    {
        Assert.Equal(BusinessViolationCodes.InvalidQuantity, ErrorOf(() => Record(Request(quantity: 1.0001m))));
        Record(Request(quantity: 0.125m));
    }

    [Fact]
    public void U_B1_NegativePrice_IsRefused()
    {
        Assert.Equal(BusinessViolationCodes.InvalidAmount, ErrorOf(() => Record(Request(price: -1m))));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void U_B1_BlankDescription_IsRefused(string description)
    {
        Assert.Equal(BusinessViolationCodes.DescriptionRequired, ErrorOf(() => Record(Request(description: description))));
    }

    [Fact]
    public void DiscountAboveTheLineAmount_IsRefused()
    {
        Assert.Equal(BusinessViolationCodes.DiscountExceedsAmount, ErrorOf(() => Record(Request(quantity: 2m, price: 100m, discount: 200.01m))));
    }

    [Fact]
    public void ACurrencyOtherThanTheConfiguredOne_IsRefused()
    {
        Assert.Equal(BusinessViolationCodes.CurrencyMismatch, ErrorOf(() => Record(Request(currency: "USD"), currency: "BDT")));
    }

    [Theory]
    [InlineData("Appointment", "Clinic")]
    [InlineData("Visit", "Clinic")]
    [InlineData("Dispense", "Clinic")]
    [InlineData("Surgery", "Clinic")]
    [InlineData("Order", "Shop")]
    [InlineData("BoardingStay", "Boarding")]
    public void U_B2_DefaultBusinessLinePerSource(string source, string expected)
    {
        Assert.Equal(expected, ChargeRules.DefaultBusinessLine(Enumeration.FromDisplayName<ChargeSourceKind>(source)).DisplayName);
    }

    [Fact]
    public void U_B2_OnlyAManualChargeMayOverrideTheDefaultLine()
    {
        Assert.Equal(BusinessViolationCodes.InvalidEnumValue, ErrorOf(() => Record(Request(source: ChargeSourceKind.Order, line: BusinessLine.Clinic))));
        Record(Request(source: ChargeSourceKind.Manual, line: BusinessLine.Shop));
    }

    [Fact]
    public void ASourceLineRefDefaultsToMain()
    {
        Assert.Equal("main", Record(Request(sourceLineRef: " ")).SourceLineRef);
    }

    [Fact]
    public void Void_OnlyAnActiveCharge_AndNeedsAReason()
    {
        var charge = Record(Request());
        Assert.Equal(BusinessViolationCodes.ReasonRequired, ErrorOf(() => charge.Void("  ", DateTimeOffset.UtcNow, BillingTestData.Actor())));

        charge.Void("Entered by mistake", DateTimeOffset.UtcNow, BillingTestData.Actor());
        Assert.Equal(ChargeStatus.Voided, charge.Status);
        Assert.Equal("Entered by mistake", charge.VoidReason);

        Assert.Equal(BusinessViolationCodes.ChargeNotActive, ErrorOf(() => charge.Void("again", DateTimeOffset.UtcNow, BillingTestData.Actor())));
    }

    [Fact]
    public void ReassignOwner_MovesOnlyAnActiveCharge()
    {
        var charge = Record(Request());
        var survivor = Guid.NewGuid();
        charge.ReassignOwner(survivor);
        Assert.Equal(survivor, charge.CustomerId);

        charge.Void("x", DateTimeOffset.UtcNow, BillingTestData.Actor());
        charge.ReassignOwner(Guid.NewGuid());
        Assert.Equal(survivor, charge.CustomerId);
    }
}
