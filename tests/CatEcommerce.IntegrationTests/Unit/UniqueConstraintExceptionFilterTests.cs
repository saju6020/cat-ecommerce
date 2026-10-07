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
using Xunit;

namespace CatEcommerce.IntegrationTests.Unit;

/// <summary>
/// ARCHITECTURE gate second pass, HIGH B (owner decision, 2026-09-28): a Postgres deadlock
/// (<c>40P01</c>) maps to <c>409 SlotNoLongerAvailable</c> only on an action carrying
/// <see cref="SlotClaimEndpointAttribute"/>; everywhere else it is left unhandled (the logged
/// <c>500</c> it always was). A pure unit test, no TestServer/HTTP -- constructs the
/// <see cref="ExceptionContext"/> and the <see cref="Endpoint"/> metadata directly, the same
/// approach <c>EndpointAccessConventionTests</c> uses for this project's other Api-layer
/// convention tests.
/// </summary>
public sealed class UniqueConstraintExceptionFilterTests
{
    [Fact]
    public void OnException_DeadlockOnMarkedEndpoint_MapsToSlotNoLongerAvailable()
    {
        var context = BuildContext(BuildDeadlockException(), marked: true);

        new UniqueConstraintExceptionFilter().OnException(context);

        Assert.True(context.ExceptionHandled);
        var result = Assert.IsType<ConflictObjectResult>(context.Result);
        var validation = Assert.IsType<ValidationResponse>(result.Value);
        Assert.Contains(validation.Errors, e => e.ErrorCode == BusinessViolationCodes.SlotNoLongerAvailable);
    }

    [Fact]
    public void OnException_DeadlockOnUnmarkedEndpoint_IsNotHandled()
    {
        var context = BuildContext(BuildDeadlockException(), marked: false);

        new UniqueConstraintExceptionFilter().OnException(context);

        Assert.False(context.ExceptionHandled);
        Assert.Null(context.Result);
    }

    /// <summary>The exact shape the flaky-test root cause found: EF's base <c>ExecutionStrategy</c>
    /// wraps a transient-classified exception in an outer <see cref="InvalidOperationException"/>.
    /// Proves the unwrap still finds the deadlock through that extra layer.</summary>
    [Fact]
    public void OnException_DeadlockWrappedInTransientFailureException_OnMarkedEndpoint_IsStillMapped()
    {
        var wrapped = new InvalidOperationException("An exception has been raised that is likely due to a transient failure.", BuildDeadlockException());
        var context = BuildContext(wrapped, marked: true);

        new UniqueConstraintExceptionFilter().OnException(context);

        Assert.True(context.ExceptionHandled);
        var result = Assert.IsType<ConflictObjectResult>(context.Result);
        var validation = Assert.IsType<ValidationResponse>(result.Value);
        Assert.Contains(validation.Errors, e => e.ErrorCode == BusinessViolationCodes.SlotNoLongerAvailable);
    }

    private static DbUpdateException BuildDeadlockException()
    {
        var postgresException = new PostgresException("deadlock detected", "ERROR", "ERROR", PostgresErrorCodes.DeadlockDetected);
        return new DbUpdateException("An error occurred while saving the entity changes.", postgresException);
    }

    private static ExceptionContext BuildContext(Exception exception, bool marked)
    {
        var httpContext = new DefaultHttpContext();
        var metadata = marked
            ? new EndpointMetadataCollection(new SlotClaimEndpointAttribute())
            : EndpointMetadataCollection.Empty;
        httpContext.SetEndpoint(new Endpoint(null, metadata, "test"));

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new ExceptionContext(actionContext, new List<IFilterMetadata>()) { Exception = exception };
    }
}
