'use client';

import Link from 'next/link';
import { STAY_STATUS_LABEL } from '@/components/boarding/shared/labels';
import type { BoardingOccupancy, OccupancyUnitRow } from '@/lib/api/types';
import { formatShortDate } from '@/lib/format/boarding';
import {
  boardingSearchHref,
  STAFF_BOARDING_NEW_STAY,
  staffStayHref,
} from '@/lib/routing/boarding-links';
import { cn } from '@/lib/utils';

const STATUS_STYLE: Record<string, string> = {
  CheckedIn: 'bg-accent text-white',
  Confirmed: 'bg-accent-soft text-accent-ink border-accent',
  Reserved: 'bg-warning-bg text-warning-ink border-warning-ink/40',
};

function colOf(days: string[], date: string): number {
  return days.indexOf(date) + 1;
}

function UnitRow({ row, days }: { row: OccupancyUnitRow; days: string[] }) {
  const taken = new Set<string>();
  for (const s of row.stays) for (const d of days) if (d >= s.fromOn && d <= s.toOn) taken.add(d);
  for (const m of row.maintenance)
    for (const d of days) if (d >= m.fromOn && d <= m.toOn) taken.add(d);
  return (
    <li
      className="grid items-stretch border-b border-border"
      style={{ gridTemplateColumns: `9rem repeat(${days.length}, minmax(2.5rem, 1fr))` }}
    >
      <div
        style={{ gridColumn: 1, gridRow: 1 }}
        className="px-2 py-2 text-sm font-semibold text-ink"
      >
        {row.code}
        <span className="block text-xs font-normal text-ink-muted">{row.name}</span>
      </div>
      {days.map((d, i) =>
        taken.has(d) ? null : row.turnaroundDays.includes(d) ? (
          <div
            key={d}
            aria-label={`${row.name}, ${formatShortDate(d)}: kept free for cleaning`}
            style={{
              gridColumn: i + 2,
              gridRow: 1,
              backgroundImage:
                'repeating-linear-gradient(45deg, var(--border) 0 4px, transparent 4px 8px)',
            }}
            className="min-h-10 border-l border-border text-center text-[10px] text-ink-muted"
          >
            clean
          </div>
        ) : (
          <Link
            key={d}
            tabIndex={-1}
            aria-label={`Book ${row.name} from ${formatShortDate(d)}`}
            href={boardingSearchHref(
              { arrivalOn: d, departureOn: days[i + 1] ?? d },
              STAFF_BOARDING_NEW_STAY,
            )}
            style={{ gridColumn: i + 2, gridRow: 1 }}
            className="min-h-10 border-l border-border hover:bg-surface-sunken"
          />
        ),
      )}
      {row.stays.map((s) => {
        const start = colOf(days, s.fromOn < days[0]! ? days[0]! : s.fromOn);
        const end = colOf(days, s.toOn > days.at(-1)! ? days.at(-1)! : s.toOn);
        if (start < 1 || end < 1) return null;
        return (
          <Link
            key={s.stayId}
            href={staffStayHref(s.stayId)}
            style={{ gridColumn: `${start + 1} / ${end + 2}`, gridRow: 1 }}
            className={cn(
              'z-10 m-0.5 overflow-hidden rounded-[var(--radius-sm)] border px-2 py-1 text-xs font-semibold',
              STATUS_STYLE[s.status] ?? 'bg-surface-sunken text-ink',
            )}
          >
            {s.catNames.length > 0 ? `${s.catNames.join(' and ')} · ` : 'Stay · '}
            {STAY_STATUS_LABEL[s.status] ?? s.status}
            {s.awayInSurgery ? ' · Away — in surgery' : ''}
          </Link>
        );
      })}
      {row.maintenance.map((m) => {
        const start = colOf(days, m.fromOn < days[0]! ? days[0]! : m.fromOn);
        const end = colOf(days, m.toOn > days.at(-1)! ? days.at(-1)! : m.toOn);
        if (start < 1 || end < 1) return null;
        return (
          <div
            key={m.blockId}
            style={{
              gridColumn: `${start + 1} / ${end + 2}`,
              gridRow: 1,
              backgroundImage:
                'repeating-linear-gradient(135deg, var(--border-strong) 0 5px, var(--surface) 5px 10px)',
            }}
            className="z-10 m-0.5 rounded-[var(--radius-sm)] border border-border-strong px-2 py-1 text-xs font-bold text-ink"
          >
            <span className="rounded bg-surface px-1">Maintenance — {m.reason}</span>
          </div>
        );
      })}
    </li>
  );
}

/**
 * `components/boarding/board/OccupancyGrid.tsx` — ⭐ HO-BRD-002 §11.10, ADR-FE-102. Rows = units grouped
 * by block, columns = days. A stay is a bar with its cats and its status **in words**, maintenance a
 * patterned band labelled “Maintenance”, a turnaround day hatched and labelled “clean” — none of it
 * relies on colour. Every bar is a link to the stay, reachable by keyboard in row order; an empty
 * cell opens staff booking prefilled. There is no drag-and-drop. The server decides what is where.
 */
export function OccupancyGrid({ data }: { data: BoardingOccupancy }) {
  const blocks = new Map<string, OccupancyUnitRow[]>();
  for (const u of data.units)
    blocks.set(u.block ?? 'No block', [...(blocks.get(u.block ?? 'No block') ?? []), u]);
  return (
    <div
      role="group"
      aria-label="Occupancy by unit and day"
      className="overflow-x-auto rounded-[var(--radius-md)] border border-border bg-surface"
    >
      <div
        className="grid border-b border-border bg-surface-sunken"
        style={{ gridTemplateColumns: `9rem repeat(${data.days.length}, minmax(2.5rem, 1fr))` }}
      >
        <div className="px-2 py-2 text-xs font-bold uppercase text-ink-muted">Unit</div>
        {data.days.map((d) => (
          <div key={d} className="px-1 py-2 text-center text-[11px] font-bold text-ink-muted">
            {formatShortDate(d)}
          </div>
        ))}
      </div>
      {[...blocks.entries()].map(([block, rows]) => (
        <div key={block} aria-label={block}>
          <p className="bg-surface-sunken px-2 py-1 text-xs font-bold uppercase tracking-wide text-ink-muted">
            {block}
          </p>
          <ul className="m-0 list-none p-0">
            {rows.map((r) => (
              <UnitRow key={r.unitId} row={r} days={data.days} />
            ))}
          </ul>
        </div>
      ))}
    </div>
  );
}

/** Below 768 px (and as the list equivalent): per day, every unit and who is in it. */
export function OccupancyDayList({ data }: { data: BoardingOccupancy }) {
  return (
    <ol aria-label="Occupancy by day" className="flex flex-col gap-3">
      {data.days.map((day) => (
        <li key={day} className="rounded-[var(--radius-md)] border border-border bg-surface p-3">
          <h3 className="font-display text-base font-semibold text-ink">{formatShortDate(day)}</h3>
          <ul className="mt-1 flex flex-col gap-1 text-sm">
            {data.units.map((u) => {
              const stay = u.stays.find((s) => day >= s.fromOn && day <= s.toOn);
              const block = u.maintenance.find((m) => day >= m.fromOn && day <= m.toOn);
              return (
                <li key={u.unitId}>
                  <strong>{u.code}</strong>:{' '}
                  {stay ? (
                    <Link
                      href={staffStayHref(stay.stayId)}
                      className="font-semibold text-accent-ink underline"
                    >
                      {stay.catNames.join(' and ')} ({STAY_STATUS_LABEL[stay.status]})
                    </Link>
                  ) : block ? (
                    <span>Maintenance — {block.reason}</span>
                  ) : u.turnaroundDays.includes(day) ? (
                    <span className="text-ink-muted">Kept free for cleaning</span>
                  ) : (
                    <Link
                      href={boardingSearchHref(
                        { arrivalOn: day, departureOn: day },
                        STAFF_BOARDING_NEW_STAY,
                      )}
                      className="text-ink-muted underline"
                    >
                      Free — book
                    </Link>
                  )}
                </li>
              );
            })}
          </ul>
        </li>
      ))}
    </ol>
  );
}
