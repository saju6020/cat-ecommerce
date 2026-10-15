namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// Lets a command handler recognise a lost race decided by the database without referencing the database provider (the Application layer holds no Npgsql or EF type). Implemented in
/// Infrastructure, looking through the whole exception chain.
/// </summary>
public interface IUniqueViolationDetector
{
    /// <summary>A Postgres 23505 on the given constraint.</summary>
    bool IsUniqueViolation(Exception exception, string constraintName);

    /// <summary>The claim on a unit or daycare slot was lost: an exclusion violation (23P01), a deadlock (40P01) or a 23505 on <c>ix_daycare_slot</c>.</summary>
    bool IsOccupancyClaimLost(Exception exception);

    /// <summary>A save on a stale row version (EF DbUpdateConcurrencyException).</summary>
    bool IsConcurrencyConflict(Exception exception);
}
