import { timeOnlyInZone } from '@/lib/format/time';
import type { BusyInterval } from '@/lib/api/surgeries';

/**
 * `BusyExplainer` — HO-SRG-002 §11.1 part 3, ADR-FE-069. Under the picker, each resource's busy
 * intervals with the server's labels (*"Dr Rahman: in surgery 10:00–11:00"*), so a missing time is
 * explained BEFORE anyone clicks. It lists what the server sent and works nothing out.
 */
export function BusyExplainer({
  busy,
  zone,
}: {
  busy: { theatre: BusyInterval[]; surgeon: BusyInterval[]; assistant: BusyInterval[] };
  zone: string;
}) {
  const groups: Array<[string, BusyInterval[]]> = [
    ['Theatre', busy.theatre],
    ['Surgeon', busy.surgeon],
    ['Assistant', busy.assistant],
  ];
  const any = groups.some(([, intervals]) => intervals.length > 0);
  return (
    <div className="flex flex-col gap-2 rounded-[var(--radius-md)] bg-surface-sunken p-3 text-sm">
      <h3 className="text-sm font-semibold text-ink">Why some times are missing</h3>
      {!any ? (
        <p className="text-ink-muted">
          Nothing is booked for the chosen theatre, surgeon or assistant that day.
        </p>
      ) : (
        groups.map(([title, intervals]) =>
          intervals.length === 0 ? null : (
            <div key={title}>
              <p className="text-xs font-bold uppercase tracking-wide text-ink-muted">{title}</p>
              <ul className="list-disc pl-5 text-ink">
                {intervals.map((interval) => (
                  <li key={`${interval.startsOn}-${interval.label}`}>
                    <strong>
                      {timeOnlyInZone(interval.startsOn, zone)}–
                      {timeOnlyInZone(interval.endsOn, zone)}
                    </strong>{' '}
                    {interval.label}
                  </li>
                ))}
              </ul>
            </div>
          ),
        )
      )}
    </div>
  );
}
