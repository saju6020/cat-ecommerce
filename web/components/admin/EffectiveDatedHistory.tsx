import { formatInZone } from '@/lib/format/time';

/**
 * `components/admin/EffectiveDatedHistory.tsx` — ⭐ HO-ADM-002 §11.3, ADR-FE-019 (renumbered —
 * see that ADR's note). Shared by service prices and tax rates: rows grouped **Current ·
 * Scheduled · Past**, each `effectiveFrom` shown in the primary branch's time zone (prices/rates
 * are not branch-specific). **No edit or delete control anywhere** — the only action a caller adds
 * around this is "Schedule a new …" (`ScheduleChangeDialog`).
 */
export interface EffectiveDatedRow {
  id: string;
  effectiveFrom: string; // instant
  label: string; // pre-formatted value, e.g. "৳1,500.00" or "15%"
  setBy?: string;
}

export interface EffectiveDatedHistoryProps {
  rows: EffectiveDatedRow[];
  timeZoneId: string;
  nowIso?: string;
}

function groupRows(rows: EffectiveDatedRow[], nowIso: string) {
  const sorted = [...rows].sort((a, b) => b.effectiveFrom.localeCompare(a.effectiveFrom));
  const scheduled = sorted.filter((row) => row.effectiveFrom > nowIso);
  const notFuture = sorted.filter((row) => row.effectiveFrom <= nowIso);
  const [current, ...past] = notFuture;
  return { current, scheduled: scheduled.reverse(), past };
}

export function EffectiveDatedHistory({
  rows,
  timeZoneId,
  nowIso = new Date().toISOString(),
}: EffectiveDatedHistoryProps) {
  if (rows.length === 0) {
    return <p className="text-sm text-ink-muted">Not priced — this cannot be booked yet.</p>;
  }

  const { current, scheduled, past } = groupRows(rows, nowIso);

  function renderRow(row: EffectiveDatedRow) {
    return (
      <li key={row.id} className="flex flex-wrap items-baseline justify-between gap-2 py-1.5">
        <span className="text-sm font-semibold text-ink">{row.label}</span>
        <span className="text-xs text-ink-muted">
          from {formatInZone(row.effectiveFrom, timeZoneId)}
        </span>
      </li>
    );
  }

  return (
    <div className="flex flex-col gap-4">
      <div>
        <h3 className="text-xs font-bold uppercase tracking-wide text-ink-muted">Current</h3>
        {current ? (
          <ul className="divide-y divide-border">{renderRow(current)}</ul>
        ) : (
          <p className="text-sm text-ink-muted">Not priced — this cannot be booked yet.</p>
        )}
      </div>
      {scheduled.length > 0 && (
        <div>
          <h3 className="text-xs font-bold uppercase tracking-wide text-ink-muted">Scheduled</h3>
          <ul className="divide-y divide-border">{scheduled.map(renderRow)}</ul>
        </div>
      )}
      {past.length > 0 && (
        <div>
          <h3 className="text-xs font-bold uppercase tracking-wide text-ink-muted">Past</h3>
          <ul className="divide-y divide-border">{past.map(renderRow)}</ul>
        </div>
      )}
    </div>
  );
}
