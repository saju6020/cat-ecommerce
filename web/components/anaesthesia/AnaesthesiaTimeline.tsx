'use client';

import { Activity, Flag, Pill, Pencil } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { EmptyState } from '@/components/common/EmptyState';
import { formatDateOnly, timeOnlyInZone } from '@/lib/format/time';
import { useServerNow } from '@/lib/hooks/useServerNow';
import {
  VITAL_KINDS,
  VITAL_LABELS,
  formatDose,
  formatDrawn,
  formatServerMgPerKg,
  formatVital,
  formatWeightKg,
  milestoneLabel,
  routeLabel,
} from '@/lib/format/surgery';
import { cn } from '@/lib/utils';
import type { AnaesthesiaEntry, AnaesthesiaLog, EntryValues } from '@/lib/api/anaesthesia-log';

const KIND_ICON = { Drug: Pill, Vitals: Activity, Milestone: Flag, Correction: Pencil } as const;

function EntryContent({ entry, log }: { entry: AnaesthesiaEntry; log: AnaesthesiaLog }) {
  const v: EntryValues = entry.values;
  const kind = entry.kind === 'Correction' ? (entry.correctedKind ?? 'Drug') : entry.kind;
  if (kind === 'Drug') {
    return (
      <div className="flex flex-col gap-0.5">
        <p className="font-semibold">
          {v.medicineName}{' '}
          {v.doseAmount !== null && v.doseUnit && formatDose(v.doseAmount, v.doseUnit)}
          {v.route && ` ${routeLabel(v.route)}`}
        </p>
        {v.batchNumber && (
          <p className="text-sm text-ink-muted">
            Batch {v.batchNumber}
            {v.quantityDrawn !== null && ` · drew ${formatDrawn(v.quantityDrawn, v.stockUnit)}`}
            {v.quantityWasted !== null && ` · wasted ${formatDrawn(v.quantityWasted, v.stockUnit)}`}
          </p>
        )}
        {v.administeredByName && (
          <p className="text-sm text-ink-muted">Given by {v.administeredByName}</p>
        )}
        {v.isControlled && (
          <p className="text-sm font-semibold text-warning-ink">
            Controlled — witnessed by {v.witnessName ?? 'a second member of staff'}
          </p>
        )}
        {v.stockShortfall && (
          <p className="text-sm font-semibold text-warning-ink">Stock shortfall recorded</p>
        )}
        {entry.effectiveDoseMgPerKg !== null && !entry.isCorrected && (
          <p className="text-xs text-ink-muted">
            {formatServerMgPerKg(
              entry.effectiveDoseMgPerKg,
              log.latestWeight?.kilograms ?? null,
              log.latestWeight ? formatDateOnly(log.latestWeight.recordedOn) : null,
            )}{' '}
            (for information)
          </p>
        )}
      </div>
    );
  }
  if (kind === 'Vitals') {
    const shown = VITAL_KINDS.filter((k) => v[k] !== null);
    return (
      <div className="flex flex-col gap-0.5">
        <ul className="flex flex-wrap gap-x-4 gap-y-0.5 text-sm">
          {shown.map((k) => (
            <li key={k}>
              <span className="text-ink-muted">{VITAL_LABELS[k]}</span>{' '}
              <strong>{formatVital(k, v[k] as number)}</strong>
            </li>
          ))}
        </ul>
        {v.note && <p className="text-sm text-ink-muted">{v.note}</p>}
      </div>
    );
  }
  return (
    <p className="font-semibold">
      {v.milestone ? milestoneLabel(v.milestone) : 'Milestone'}
      {v.note && <span className="font-normal text-ink-muted"> — {v.note}</span>}
    </p>
  );
}

/**
 * ⭐ `AnaesthesiaTimeline` — HO-SRG-002 §11.4, ADR-FE-070, US-SRG-04/08. An append-only ORDERED LIST by
 * `occurredOn`: the time as text on the left (branch zone, `HH:mm`), on the right the kind (icon AND word),
 * its content with every dose, quantity and vital shown WITH its unit (UI-8), who logged it and when. A
 * corrected entry stays, struck through and marked *"Corrected"* in words with a link to its correction;
 * a correction links back to the entry it replaces. The ONLY action on an entry is **Correct…** — there is
 * no edit control and no delete control anywhere (US-SRG-08 AC2, HO-SRG-002 §20.6). One column at every
 * width, so it never scrolls sideways.
 */
export function AnaesthesiaTimeline({
  log,
  canCorrect,
  onCorrect,
}: {
  log: AnaesthesiaLog;
  /** Whether this person may correct an entry of that kind (drug corrections are vets only). */
  canCorrect: (entry: AnaesthesiaEntry) => boolean;
  onCorrect: (entry: AnaesthesiaEntry) => void;
}) {
  const zone = log.branchTimeZoneId;
  const now = useServerNow(30_000);
  const byId = new Map(log.entries.map((e) => [e.id, e]));
  const lastVitals = [...log.entries]
    .reverse()
    .find((e) => (e.correctedKind ?? e.kind) === 'Vitals' && !e.isCorrected);
  const minutesSince = lastVitals
    ? Math.max(0, Math.round((now - new Date(lastVitals.occurredOn).getTime()) / 60_000))
    : null;

  return (
    <section aria-labelledby="srg-log" className="flex flex-col gap-3">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h2 id="srg-log" className="font-display text-lg font-semibold text-ink">
          Anaesthesia log
        </h2>
        <p className="text-sm text-ink-muted">
          {log.isOpen ? 'Open' : 'Closed'}
          {log.latestWeight && (
            <>
              {' · '}Weight {formatWeightKg(log.latestWeight.kilograms)} (weighed{' '}
              {formatDateOnly(log.latestWeight.recordedOn)})
            </>
          )}
          {' · '}
          {minutesSince === null ? 'No vitals yet' : `Last vitals ${minutesSince} min ago`}
        </p>
      </div>
      {log.entries.length === 0 ? (
        <EmptyState
          title="Nothing has been logged yet."
          description="Drugs, vitals and milestones appear here as they are recorded."
        />
      ) : (
        <ol className="flex flex-col divide-y divide-border rounded-[var(--radius-md)] border border-border bg-surface">
          {log.entries.map((entry) => {
            const Icon = KIND_ICON[entry.kind];
            const correction = entry.correctedByEntryId
              ? byId.get(entry.correctedByEntryId)
              : undefined;
            const kindText =
              entry.kind === 'Correction'
                ? `Correction (${entry.correctedKind ?? 'entry'})`
                : entry.kind;
            return (
              <li
                key={entry.id}
                id={`entry-${entry.id}`}
                tabIndex={-1}
                className="grid grid-cols-[4.5rem_1fr] gap-3 p-3 focus:outline-2 focus:outline-accent"
              >
                <time
                  dateTime={entry.occurredOn}
                  className="text-sm font-bold tabular-nums text-ink"
                >
                  {timeOnlyInZone(entry.occurredOn, zone)}
                </time>
                <div className="flex min-w-0 flex-col gap-1">
                  <p className="flex flex-wrap items-center gap-2 text-xs font-bold uppercase tracking-wide text-ink-muted">
                    <Icon size={14} aria-hidden="true" />
                    {kindText}
                    {entry.isCorrected && <Badge variant="warning">Corrected</Badge>}
                  </p>
                  <div
                    className={cn(
                      'break-words',
                      entry.isCorrected && 'text-ink-muted line-through',
                    )}
                  >
                    <EntryContent entry={entry} log={log} />
                  </div>
                  {entry.correctsEntryId && (
                    <p className="text-sm text-ink">
                      Corrects{' '}
                      <a
                        className="font-semibold underline"
                        href={`#entry-${entry.correctsEntryId}`}
                      >
                        an earlier entry
                      </a>
                      {entry.correctionReason ? `: ${entry.correctionReason}` : ''}
                    </p>
                  )}
                  {entry.isCorrected && correction && (
                    <p className="text-sm font-semibold text-warning-ink">
                      Corrected at {timeOnlyInZone(correction.loggedOn, zone)} by{' '}
                      {correction.loggedByName}
                      {correction.correctionReason ? ` — ${correction.correctionReason}` : ''}{' '}
                      <a className="underline" href={`#entry-${correction.id}`}>
                        See the correction
                      </a>
                    </p>
                  )}
                  <p className="text-xs text-ink-muted">
                    logged {timeOnlyInZone(entry.loggedOn, zone)} by {entry.loggedByName}
                  </p>
                  {log.isOpen && !entry.isCorrected && canCorrect(entry) && (
                    <div>
                      <Button
                        variant="secondary"
                        size="sm"
                        aria-label={`Correct the ${timeOnlyInZone(entry.occurredOn, zone)} ${kindText.toLowerCase()} entry`}
                        onClick={() => onCorrect(entry)}
                      >
                        Correct…
                      </Button>
                    </div>
                  )}
                </div>
              </li>
            );
          })}
        </ol>
      )}
    </section>
  );
}
