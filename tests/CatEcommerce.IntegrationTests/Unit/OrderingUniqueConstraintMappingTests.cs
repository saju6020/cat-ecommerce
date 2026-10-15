using CatEcommerce.Api.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.IntegrationTests.Unit;

/// <summary>HO-ORD-001 §12.5 (ARCHITECTURE M4 fix, 2026-10-09) -- the Ordering unique-index mappings of <see cref="UniqueConstraintExceptionFilter"/>, as pure unit tests.</summary>
public sealed class OrderingUniqueConstraintMappingTests
{
    private static ExceptionContext Context(PostgresException pg) =>
        new(new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()), new List<IFilterMetadata>())
        {
            Exception = new DbUpdateException("An error occurred while saving the entity changes.", pg),
        };

    [Fact]
    public void TheCustomerIdempotencyIndex_IsA409_DuplicateSubmission_NotA500()
    {
        var context = Context(new PostgresException("duplicate key value violates unique constraint", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation,
            constraintName: "ix_sales_order_customer_idempotency", tableName: "sales_order"));

        new UniqueConstraintExceptionFilter().OnException(context);

        Assert.True(context.ExceptionHandled);
        var result = Assert.IsType<ConflictObjectResult>(context.Result);
        var validation = Assert.IsType<ValidationResponse>(result.Value);
        Assert.Contains(validation.Errors, e => e.ErrorCode == "DuplicateSubmission");
    }
}
