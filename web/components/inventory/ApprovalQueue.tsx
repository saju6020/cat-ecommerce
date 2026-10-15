import Link from 'next/link';
import { Panel } from './fields';
import { adjustmentReasonLabel } from './labels';
import { formatMoney } from '@/lib/format/money';
import { formatInZone } from '@/lib/format/time';
import type { AdjustmentSummary, CountSummary } from '@/lib/api/inventory-control';

/**
 * `components/inventory/ApprovalQueue.tsx` — HO-IMP-002 §11.5, ADR-FE-048. ONE queue for the two
 * things that can wait for a different person's approval: adjustments and counts. Each row opens the
 * record, where Approve / Reject appear only if the server's `allowedActions` list them — and where
 * *"Someone else has to approve your request"* is the server's answer, not a surprise.
 */
export function ApprovalQueue({
  adjustments,
  counts,
  timeZoneId,
}: {
  adjustments: AdjustmentSummary[];
  counts: CountSummary[];
  timeZoneId: string;
}) {
  const empty = adjustments.length === 0 && counts.length === 0;
  return (
    <Panel title="Waiting for approval" headingId="approval-queue-heading">
      {empty ? (
        <p className="text-sm text-ink-muted">Nothing is waiting for approval.</p>
      ) : (
        <ul className="flex flex-col divide-y divide-border text-sm">
          {adjustments.map((a) => (
            <li key={a.id} className="flex flex-wrap items-center justify-between gap-2 py-2">
              <span className="min-w-0">
                <strong>{a.number}</strong> — adjustment, {adjustmentReasonLabel(a.reason).toLowerCase()}, {formatMoney(a.valueBase)}
                <span className="block text-xs text-ink-muted">Requested {formatInZone(a.requestedOn, timeZoneId)}</span>
              </span>
              <Link href={`/staff/inventory/adjustments/${a.id}`} className="font-semibold text-accent-ink" aria-label={`Review adjustment ${a.number}`}>
                Review
              </Link>
            </li>
          ))}
          {counts.map((c) => (
            <li key={c.id} className="flex flex-wrap items-center justify-between gap-2 py-2">
              <span className="min-w-0">
                <strong>{c.number}</strong> — stock count at {c.locationCode ?? 'a location'}
                {c.varianceValueBase ? `, variance ${formatMoney(c.varianceValueBase)}` : ''}
                <span className="block text-xs text-ink-muted">Submitted {formatInZone(c.openedOn, timeZoneId)}</span>
              </span>
              <Link href={`/staff/inventory/counts/${c.id}`} className="font-semibold text-accent-ink" aria-label={`Review count ${c.number}`}>
                Review
              </Link>
            </li>
          ))}
        </ul>
      )}
    </Panel>
  );
}
