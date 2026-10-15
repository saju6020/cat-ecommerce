import Link from 'next/link';
import { CircleCheck, CircleX } from 'lucide-react';
import type { RestoreTest } from '@/lib/api/operations';
import { formatDateOnly, formatInZone } from '@/lib/format/time';
import { OutcomeBadge } from './RestoreTestsTable';

/**
 * `components/operations/RestoreTestDetail.tsx` — HO-XCT-002 §11.5. One restore test: the verdict
 * "worked out by PurrCare" (the server's computation, ADR-313), the achieved RPO and RTO, the
 * checks and the drill output. The drill output is rendered as TEXT inside a `<pre>`, never as
 * markup (SECURITY gate: no `dangerouslySetInnerHTML`).
 */
export function RestoreTestDetail({ test, zone }: { test: RestoreTest; zone: string }) {
  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-3">
        <h1 className="font-display text-xl font-semibold text-ink">
          Restore test · {formatDateOnly(test.performedOn)}
        </h1>
        <OutcomeBadge outcome={test.outcome} />
      </div>
      <p className="text-sm text-ink-muted">Worked out by PurrCare from the times in the drill output.</p>
      <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-sm">
        <dt className="font-semibold text-ink">Data lost (RPO)</dt><dd className="text-ink-muted">{test.achievedRpoMinutes} minutes</dd>
        <dt className="font-semibold text-ink">Time to restore (RTO)</dt><dd className="text-ink-muted">{test.achievedRtoMinutes} minutes</dd>
        <dt className="font-semibold text-ink">Environment</dt><dd className="text-ink-muted">{test.environment}</dd>
        <dt className="font-semibold text-ink">Backup set</dt><dd className="text-ink-muted">{test.backupSetLabel}</dd>
        <dt className="font-semibold text-ink">Failure simulated at</dt><dd className="text-ink-muted">{formatInZone(test.simulatedFailureAt, zone)}</dd>
        <dt className="font-semibold text-ink">Recovered to</dt><dd className="text-ink-muted">{formatInZone(test.recoveredTo, zone)}</dd>
        <dt className="font-semibold text-ink">Restore ran</dt>
        <dd className="text-ink-muted">{formatInZone(test.restoreStartedAt, zone)} to {formatInZone(test.restoreCompletedAt, zone)}</dd>
      </dl>
      {test.notes && <p className="text-sm text-ink">{test.notes}</p>}
      {test.supersedesId && (
        <p className="text-sm text-ink-muted">
          This test replaces{' '}
          <Link href={`/staff/admin/operations/backup/${test.supersedesId}`} className="font-semibold text-accent-ink hover:underline">an earlier one</Link>.
        </p>
      )}
      {test.supersededByIds.length > 0 && (
        <p className="text-sm text-ink-muted">A later test replaces this one.</p>
      )}
      <section aria-labelledby="checks-title">
        <h2 id="checks-title" className="text-[15px] font-semibold text-ink">Checks</h2>
        <ul className="mt-2 flex flex-col gap-1 text-sm">
          {test.checks.map((check) => (
            <li key={check.name} className="flex items-start gap-2">
              {check.passed ? (
                <CircleCheck size={16} className="mt-0.5 flex-none text-accent-ink" aria-hidden="true" />
              ) : (
                <CircleX size={16} className="mt-0.5 flex-none text-danger-ink" aria-hidden="true" />
              )}
              <span>
                <span className="font-semibold text-ink">{check.name}</span>
                <span className="text-ink-muted"> · {check.passed ? 'passed' : 'failed'}{check.detail ? ` · ${check.detail}` : ''}</span>
              </span>
            </li>
          ))}
        </ul>
      </section>
      <section aria-labelledby="drill-title">
        <h2 id="drill-title" className="text-[15px] font-semibold text-ink">Drill output</h2>
        <pre
          tabIndex={0}
          aria-label="Drill output (scrolls)"
          className="mt-2 max-h-80 overflow-auto whitespace-pre-wrap break-words rounded-[var(--radius-sm)] border border-border bg-surface-sunken p-3 text-xs text-ink"
        >
          {test.drillOutput}
        </pre>
      </section>
    </div>
  );
}
