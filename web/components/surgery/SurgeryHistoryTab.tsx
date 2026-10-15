import { Panel } from '@/components/inventory/fields';
import { formatAppointmentWhen } from '@/lib/format/appointment';
import { surgeryStatusLabel } from '@/lib/format/surgery';
import type { SurgeryDetail } from '@/lib/api/surgeries';

/** `SurgeryHistoryTab` — the server's `statusHistory`, in order, as text. Nothing is worked out here. */
export function SurgeryHistoryTab({ surgery }: { surgery: SurgeryDetail }) {
  const zone = surgery.branchTimeZoneId;
  return (
    <Panel title="Status history" headingId="srg-history">
      {surgery.statusHistory.length === 0 ? (
        <p className="text-sm text-ink-muted">No status changes yet.</p>
      ) : (
        <ol className="flex flex-col gap-2 text-sm text-ink">
          {surgery.statusHistory.map((change) => (
            <li key={`${change.changedOn}-${change.to}`}>
              <strong>{formatAppointmentWhen(change.changedOn, zone)}</strong>{' '}
              {change.from ? `${surgeryStatusLabel(change.from)} → ` : 'Booked as '}
              {surgeryStatusLabel(change.to)}
            </li>
          ))}
        </ol>
      )}
    </Panel>
  );
}
