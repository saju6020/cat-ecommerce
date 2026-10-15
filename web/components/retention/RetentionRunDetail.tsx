import type { RetentionRun } from '@/lib/api/retention';
import { formatInZone } from '@/lib/format/time';
import { glossErrorCode, passKindLabel } from '@/lib/retention/labels';
import { RunStatusBadge } from './RetentionRunsTable';

/**
 * `components/retention/RetentionRunDetail.tsx` — HO-XCT-002 §11.5, U-B3. One run, pass by pass:
 * what was examined, changed, held back and failed, with every error code glossed in plain
 * language (the code itself is never shown).
 */
export function RetentionRunDetail({ run, zone }: { run: RetentionRun; zone: string }) {
  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-3">
        <h1 className="font-display text-xl font-semibold text-ink">Retention run</h1>
        <RunStatusBadge status={run.status} />
      </div>
      <p className="text-sm text-ink-muted">
        {run.trigger === 'Manual' ? 'Started by staff (run now)' : 'Started by the nightly schedule'} ·
        requested {formatInZone(run.requestedOn, zone)}
        {run.finishedOn ? ` · finished ${formatInZone(run.finishedOn, zone)}` : ''}
      </p>
      <p className="text-sm text-ink">
        Clinical, financial, regulatory and audit records are never deleted by a run. They are only
        counted for review.
      </p>
      {run.passes.length === 0 ? (
        <p className="text-sm text-ink-muted">This run has not recorded any steps yet.</p>
      ) : (
        <ul className="flex flex-col gap-2">
          {run.passes.map((pass) => (
            <li
              key={pass.kind}
              className="rounded-[var(--radius-md)] border border-border bg-surface p-4 text-sm"
            >
              <p className="font-semibold text-ink">{passKindLabel(pass.kind)}</p>
              <p className="text-ink-muted">
                {pass.examined} examined · {pass.affected} changed · {pass.held} held back ·{' '}
                {pass.failed} failed · {pass.durationMs} ms
                {pass.cutoff ? ` · older than ${formatInZone(pass.cutoff, zone)}` : ''}
              </p>
              {pass.errorCodes.length > 0 && (
                <ul className="mt-2 list-disc pl-5 text-warning-ink">
                  {pass.errorCodes.map((code) => (
                    <li key={code}>{glossErrorCode(code)}</li>
                  ))}
                </ul>
              )}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
