import { CircleAlert, CircleCheck } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import type { BackupRunStatus, BackupStatus } from '@/lib/api/operations';
import { formatInZone } from '@/lib/format/time';

/** "27 hours ago" / "12 minutes ago" — an age, never a ticking clock. */
export function formatAge(iso: string, now: number = Date.now()): string {
  const minutes = Math.max(0, Math.round((now - Date.parse(iso)) / 60_000));
  if (minutes < 60) return `${minutes} ${minutes === 1 ? 'minute' : 'minutes'} ago`;
  const hours = Math.round(minutes / 60);
  if (hours < 48) return `${hours} ${hours === 1 ? 'hour' : 'hours'} ago`;
  return `${Math.round(hours / 24)} days ago`;
}

function Row({
  label,
  run,
  stale,
  zone,
}: {
  label: string;
  run: BackupRunStatus | null;
  stale: boolean;
  zone: string;
}) {
  return (
    <div className="grid gap-1 border-b border-border py-2 last:border-0 sm:grid-cols-[12rem_1fr_auto] sm:items-center">
      <dt className="font-semibold text-ink">{label}</dt>
      <dd className="text-ink-muted">
        {run ? `${formatInZone(run.finishedOn, zone)} · ${formatAge(run.finishedOn)}` : 'Never recorded'}
        {run && !run.succeeded && ' · the job reported a failure'}
      </dd>
      <dd>
        {stale || !run || !run.succeeded ? (
          <Badge variant="warning" icon={<CircleAlert size={12} aria-hidden="true" />}>
            Stale
          </Badge>
        ) : (
          <Badge variant="success" icon={<CircleCheck size={12} aria-hidden="true" />}>
            Up to date
          </Badge>
        )}
      </dd>
    </div>
  );
}

/**
 * `components/operations/BackupStatusCard.tsx` — HO-XCT-002 §11.5, U-B10. Last full backup, last
 * differential, last archive check and "WAL archived until", each with its age and a Stale state as
 * text plus icon, beside the recovery objectives and the last test's achieved values.
 */
export function BackupStatusCard({ status, zone }: { status: BackupStatus; zone: string }) {
  const last = status.lastRestoreTest;
  return (
    <section aria-labelledby="backup-status-title" className="rounded-[var(--radius-md)] border border-border bg-surface p-4">
      <h2 id="backup-status-title" className="text-[15px] font-semibold text-ink">Backup health</h2>
      {status.isBackupStale && (
        <p role="alert" className="mt-2 text-sm font-semibold text-warning-ink">
          Backups are stale. The latest backup is older than it should be.
        </p>
      )}
      <dl className="mt-2 text-sm">
        <Row label="Last full backup" run={status.lastFull} stale={status.isBackupStale} zone={zone} />
        <Row label="Last differential" run={status.lastDiff} stale={status.isBackupStale} zone={zone} />
        <Row label="Last archive check" run={status.lastArchiveCheck} stale={status.isArchiveStale} zone={zone} />
        <div className="grid gap-1 py-2 sm:grid-cols-[12rem_1fr_auto] sm:items-center">
          <dt className="font-semibold text-ink">WAL archived until</dt>
          <dd className="text-ink-muted">
            {status.walArchivedUntil
              ? `${formatInZone(status.walArchivedUntil, zone)} · ${formatAge(status.walArchivedUntil)}`
              : 'Never recorded'}
          </dd>
          <dd>
            {status.isArchiveStale || !status.walArchivedUntil ? (
              <Badge variant="warning" icon={<CircleAlert size={12} aria-hidden="true" />}>Stale</Badge>
            ) : (
              <Badge variant="success" icon={<CircleCheck size={12} aria-hidden="true" />}>Up to date</Badge>
            )}
          </dd>
        </div>
      </dl>
      <p className="mt-3 text-sm text-ink-muted">
        Objectives: lose at most {status.objectives.rpoMinutes} minutes of data (RPO) and be back
        within {Math.round(status.objectives.rtoMinutes / 60)} hours (RTO).
        {last
          ? ` Last restore test: ${last.achievedRpoMinutes} minutes of data lost, restored in ${last.achievedRtoMinutes} minutes (${last.outcome === 'Passed' ? 'passed' : 'failed'}).`
          : ' No restore test has been recorded yet.'}
      </p>
    </section>
  );
}
