using CatEcommerce.Api.Filters;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.IntegrationTests.Unit;

/// <summary>HO-BIL-001 §12.5 -- the Billing unique-index mappings of <see cref="UniqueConstraintExceptionFilter"/>, as pure
/// unit tests. The numbering indexes are deliberately NOT mapped (a numbering bug must be loud, never papered over), and
/// neither is a trigger violation (<c>P0001</c>: a code defect that surfaces as a 500).</summary>
public sealed class BillingUniqueConstraintMappingTests
{
    private static ExceptionContext Context(PostgresException pg)
    {
        var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
        return new ExceptionContext(actionContext, new List<IFilterMetadata>())
        {
            Exception = new DbUpdateException("An error occurred while saving the entity changes.", pg),
        };
    }

    private static PostgresException Unique(string constraint, string table = "charge") =>
        new("duplicate key value violates unique constraint", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation, constraintName: constraint, tableName: table);

    [Theory]
    [InlineData("ix_charge_source", typeof(ConflictObjectResult), "ChargeAlreadyRecorded")]
    [InlineData("ix_invoice_line_charge", typeof(ConflictObjectResult), "ChargeAlreadyInvoiced")]
    [InlineData("ix_bank_match_line", typeof(ConflictObjectResult), "BankLineAlreadyMatched")]
    [InlineData("ix_bank_match_payment", typeof(ConflictObjectResult), "PaymentAlreadyMatched")]
    [InlineData("ix_bank_statement_source", typeof(BadRequestObjectResult), "StatementDuplicate")]
    public void TheBillingIndexes_MapToTheSameShapeTheHandlerPreChecksReturn(string constraint, Type expectedResult, string expectedCode)
    {
        var context = Context(Unique(constraint));

        new UniqueConstraintExceptionFilter().OnException(context);

        Assert.True(context.ExceptionHandled);
        Assert.IsType(expectedResult, context.Result);
        var validation = Assert.IsType<ValidationResponse>(((ObjectResult)context.Result!).Value);
        Assert.Contains(validation.Errors, e => e.ErrorCode == expectedCode);
    }

    [Theory]
    [InlineData("ix_invoice_number")]
    [InlineData("ix_invoice_sequence")]
    [InlineData("ix_credit_note_number")]
    [InlineData("ix_document_number_series")]
    public void TheNumberingIndexes_AreNeverMapped_ABugMustBeLoud(string constraint)
    {
        var context = Context(Unique(constraint, "invoice"));

        new UniqueConstraintExceptionFilter().OnException(context);

        Assert.False(context.ExceptionHandled);
        Assert.Null(context.Result);
    }

    [Fact]
    public void ATriggerViolation_IsNotMapped_ItIsACodeDefect()
    {
        var context = Context(new PostgresException("Issued invoices are immutable", "ERROR", "ERROR", "P0001"));

        new UniqueConstraintExceptionFilter().OnException(context);

        Assert.False(context.ExceptionHandled);
    }

    [Fact]
    public void AFirstWriteRaceOnABillingProfile_Is409()
    {
        var context = Context(Unique("PK_billing_profile", table: "billing_profile"));

        new UniqueConstraintExceptionFilter().OnException(context);

        Assert.True(context.ExceptionHandled);
        Assert.IsType<ConflictObjectResult>(context.Result);
    }

    [Fact]
    public void TheMessages_AreTheOnesBillingMessagesSharesWithTheHandlers()
    {
        var context = Context(Unique("ix_charge_source"));

        new UniqueConstraintExceptionFilter().OnException(context);

        var validation = Assert.IsType<ValidationResponse>(((ObjectResult)context.Result!).Value);
        Assert.Equal(BillingMessages.ChargeAlreadyRecorded, validation.Errors.Single().ErrorMessage);
    }
}
