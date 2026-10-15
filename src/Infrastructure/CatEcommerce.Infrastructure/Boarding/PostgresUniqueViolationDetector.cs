using CatEcommerce.Domain.DomainServices;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CatEcommerce.Infrastructure.Boarding;

/// <summary>Postgres error classification anywhere in the exception chain.</summary>
public sealed class PostgresUniqueViolationDetector : IUniqueViolationDetector
{
    private const string DaycareSlotIndex = "ix_daycare_slot";

    public bool IsUniqueViolation(Exception exception, string constraintName) =>
        Chain(exception).OfType<PostgresException>().Any(p =>
            p.SqlState == PostgresErrorCodes.UniqueViolation && string.Equals(p.ConstraintName, constraintName, StringComparison.Ordinal));

    public bool IsOccupancyClaimLost(Exception exception) =>
        Chain(exception).OfType<PostgresException>().Any(p =>
            p.SqlState is PostgresErrorCodes.ExclusionViolation or PostgresErrorCodes.DeadlockDetected ||
            (p.SqlState == PostgresErrorCodes.UniqueViolation && string.Equals(p.ConstraintName, DaycareSlotIndex, StringComparison.Ordinal)));

    public bool IsConcurrencyConflict(Exception exception) => Chain(exception).Any(e => e is DbUpdateConcurrencyException);

    private static IEnumerable<Exception> Chain(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException) yield return current;
    }
}
