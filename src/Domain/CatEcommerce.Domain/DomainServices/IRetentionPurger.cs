namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-XCT-001 section 8.9 / ADR-304 -- the ONLY hard deletes the application performs, and only of <c>Purgeable</c> classes. There is a named method per
/// table; no caller passes a table name. Each deletes at most <c>batchSize</c> rows older than <c>cutoff</c> and returns how many it deleted. The
/// implementation asserts that its table is <c>Purgeable</c> in <c>RecordClassification</c> and deletes by <c>ExecuteDelete</c>, the single
/// sanctioned use outside migrations (the source scan U-A2 enforces that).
/// </summary>
public interface IRetentionPurger
{
    /// <summary>Customer sessions that ended, or expired, before the cutoff (<c>retention.securityArtefactDays</c>).</summary>
    Task<int> PurgeCustomerSessionsAsync(DateTimeOffset cutoff, int batchSize, CancellationToken ct);

    Task<int> PurgeStaffSessionsAsync(DateTimeOffset cutoff, int batchSize, CancellationToken ct);

    /// <summary>Password-reset tokens that expired before the cutoff. A used token is dead anyway: a reset changes the security stamp it was issued for.</summary>
    Task<int> PurgePasswordResetTokensAsync(DateTimeOffset cutoff, int batchSize, CancellationToken ct);

    /// <summary>Web-vital batches received before the cutoff (<c>retention.webVitalsDays</c>); their samples go with them (same class, FK cascade).</summary>
    Task<int> PurgeWebVitalBatchesAsync(DateTimeOffset cutoff, int batchSize, CancellationToken ct);

    /// <summary>Backup-job log rows finished before the cutoff (<c>RetentionRules.BackupRunDays</c>, 400 days).</summary>
    Task<int> PurgeBackupRunsAsync(DateTimeOffset cutoff, int batchSize, CancellationToken ct);
}
