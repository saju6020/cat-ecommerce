using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CatEcommerce.Api.Filters;

/// <summary>
/// HO-EMR-001 §12.5 / ADR-118 -- the third layer of immutability. A <c>PostgresException</c> with
/// <c>SQLSTATE CR001</c> means a database trigger refused to change a frozen clinical row. The aggregate
/// guard (<c>EnsureOpen</c>) and the API both stand in front of it, so <b>reaching this filter means one of
/// them was bypassed -- a defect</b>: it is logged at <c>Error</c> (no record content) and answered with a
/// <c>409</c>.
///
/// Registered globally by <c>ConsultationModule</c> alongside <see cref="ConcurrencyExceptionFilter"/>.
/// </summary>
public sealed class ClinicalRecordFrozenFilter(ILogger<ClinicalRecordFrozenFilter> logger) : IExceptionFilter
{
    /// <summary>The custom SQLSTATE the immutability triggers raise (<c>AddClinicalRecordImmutability</c>).</summary>
    public const string FrozenSqlState = "CR001";

    public const string Message = "This record is finalised and cannot be changed.";

    public void OnException(ExceptionContext context)
    {
        if (FindFrozen(context.Exception) is not { } pg) return;

        logger.LogError(
            "A clinical immutability trigger refused a write on {Table} ({Constraint}); the aggregate guard was bypassed. Path: {Path}",
            pg.TableName, pg.ConstraintName, context.HttpContext.Request.Path.Value);

        context.Result = new ConflictObjectResult(new { error = Message });
        context.ExceptionHandled = true;
    }

    private static PostgresException? FindFrozen(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException { SqlState: FrozenSqlState } pg) return pg;
        }

        return null;
    }
}
