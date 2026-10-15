import type { SnapshotAppointmentItem } from '@/lib/api/offline';
import { timeOnlyInZone } from '@/lib/format/time';
import { humanizeCode } from '@/lib/offline/labels';
import { HighRiskNote } from './HighRiskNote';

function sortByStart(items: SnapshotAppointmentItem[]): SnapshotAppointmentItem[] {
  return [...items].sort(
    (a, b) => Date.parse(a.startsOn ?? '') - Date.parse(b.startsOn ?? '') || 0,
  );
}

function timeRange(item: SnapshotAppointmentItem, zone: string): string {
  if (!item.startsOn) return 'No time';
  const start = timeOnlyInZone(item.startsOn, zone);
  return item.endsOn ? `${start}–${timeOnlyInZone(item.endsOn, zone)}` : start;
}

/**
 * `components/offline/OfflineAppointmentsList.tsx` — HO-XCT-002 §11.4. Today's appointments from the
 * device copy: time, cat, customer name, service, vet, room, status as text, and a
 * high-risk badge with its handling note. Sorted by start. Read-only: there is no button here. No phone number is shown or stored (owner decision 2026-10-14).
 */
export function OfflineAppointmentsList({
  items,
  timeZoneId,
}: {
  items: SnapshotAppointmentItem[];
  timeZoneId: string;
}) {
  const sorted = sortByStart(items);
  return (
    <section aria-labelledby="offline-appointments-heading" className="flex flex-col gap-3">
      <h2 id="offline-appointments-heading" className="font-display text-lg font-semibold text-ink">
        Today&rsquo;s appointments{' '}
        <span className="text-sm font-normal text-ink-muted">
          {sorted.length} · sorted by start time
        </span>
      </h2>
      {sorted.length === 0 ? (
        <p className="rounded-[var(--radius-md)] border border-border bg-surface p-4 text-sm text-ink-muted">
          No appointments were booked for today when this copy was saved.
        </p>
      ) : (
        <ul className="flex flex-col gap-2">
          {sorted.map((item) => (
            <li
              key={item.appointmentId}
              className="grid gap-x-4 gap-y-1 rounded-[var(--radius-md)] border border-border bg-surface p-4 text-sm sm:grid-cols-[8rem_1fr_1fr]"
            >
              <p className="font-semibold text-ink">{timeRange(item, timeZoneId)}</p>
              <div>
                <p className="font-semibold text-ink">{item.catName}</p>
                {item.catIsHighRisk && <HighRiskNote note={item.handlingNote} />}
                {item.isWalkIn && (
                  <p className="text-ink-muted">
                    Walk-in
                    {item.triagePriority ? ` · triage: ${humanizeCode(item.triagePriority)}` : ''}
                  </p>
                )}
                <p className="text-ink-muted">
                  {item.serviceName}
                  {' · '}
                  {item.performerName ?? 'Not yet assigned'}
                  {' · '}
                  {item.roomName ?? 'No room yet'}
                </p>
              </div>
              <div>
                <p className="text-ink">{item.customerName}</p>
                <p className="mt-1 text-ink-muted">
                  <span className="sr-only">Status: </span>
                  {humanizeCode(item.status)}
                </p>
              </div>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
