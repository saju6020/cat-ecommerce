import { CheckCircle2, Info, XCircle } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import type { VisitDetail } from '@/lib/api/visits';
import { formatInstantInZone } from '@/lib/format/pharmacy';

/**
 * `VisitContextBar` — HO-PHM-002 §11.2. Which visit a prescription is being written from. Three states,
 * each in words: an open visit (service, when it was opened), a visit that is finalised
 * (*"This visit has been finalised — prescriptions can no longer be added to it."*), and a remote
 * renewal, which has no visit by owner decision (2026-10-10).
 */
export const VISIT_CLOSED_MESSAGE =
  'This visit has been finalised — prescriptions can no longer be added to it.';
export const NO_VISIT_MESSAGE =
  'Prescriptions are issued from an open visit. Open the cat’s visit first.';

export function VisitContextBar({
  visit,
  remoteRenewal,
  closed,
}: {
  visit: VisitDetail | undefined;
  remoteRenewal?: boolean;
  /** The server answered `VisitNotOpen`. */
  closed?: boolean;
}) {
  if (remoteRenewal) {
    return (
      <div
        role="status"
        className="flex flex-wrap items-center gap-2 rounded-[var(--radius-md)] border border-border bg-surface-sunken px-3 py-2 text-sm"
      >
        <Badge variant="neutral" icon={<Info size={12} aria-hidden="true" />}>
          Remote renewal
        </Badge>
        <span className="text-ink-muted">
          No open visit is needed to renew a prescription. All checks run again on today’s weight
          and records.
        </span>
      </div>
    );
  }
  if (closed || visit?.status === 'Finalised') {
    return (
      <p
        role="alert"
        className="flex items-center gap-2 rounded-[var(--radius-md)] bg-danger-bg px-3 py-2 text-sm font-semibold text-danger-ink"
      >
        <XCircle size={16} aria-hidden="true" />
        {VISIT_CLOSED_MESSAGE}
      </p>
    );
  }
  if (!visit) return null;
  return (
    <div className="flex flex-wrap items-center gap-2 rounded-[var(--radius-md)] border border-border bg-surface px-3 py-2 text-sm">
      <Badge variant="success" icon={<CheckCircle2 size={12} aria-hidden="true" />}>
        Visit open
      </Badge>
      <b>{visit.service.name}</b>
      <span className="text-ink-muted">
        opened {formatInstantInZone(visit.openedOn, visit.branchTimeZoneId)} (
        {visit.branchTimeZoneId}) by {visit.openedBy}
      </span>
    </div>
  );
}
