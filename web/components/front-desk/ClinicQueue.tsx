'use client';

import { Footprints, CalendarClock } from 'lucide-react';
import { EmptyState } from '@/components/common/EmptyState';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { AppointmentStatusBadge } from '@/components/appointments/AppointmentStatusBadge';
import { Button } from '@/components/ui/button';
import { formatEstimatedWait } from '@/lib/format/appointment';
import { timeOnlyInZone } from '@/lib/format/time';
import type { ClinicQueue as ClinicQueueData, QueueEntry } from '@/lib/api/front-desk';
import type { AppointmentAction, TriagePriority } from '@/lib/api/types';
import { cn } from '@/lib/utils';
import { QueueVisitActions } from '@/components/visit/QueueVisitActions';
import { CompactCatHeader } from './CompactCatHeader';
import { PRIORITY_OPTIONS, PriorityBadge } from './PriorityBadge';

export interface ClinicQueueProps {
  queue: ClinicQueueData | undefined;
  zone: string;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
  /** Per appointment, from the detail's `allowedTransitions` — `undefined` while still loading. */
  allowedByAppointmentId: Record<string, AppointmentAction[] | undefined>;
  highlightedId?: string | null;
  isMine?: boolean;
  onChangePriority: (entry: QueueEntry, priority: TriagePriority) => void;
  onStart: (entry: QueueEntry) => void;
  onComplete: (entry: QueueEntry) => void;
}

/**
 * ⭐ `components/front-desk/ClinicQueue.tsx` — HO-APT-002 §11.5 (US-CLN-05, 10). The entries in the
 * SERVER's order (never re-sorted here), each as a card: position, priority as text, the compact
 * cat header, customer, service, walk-in or booked, when checked in, the estimated wait with a
 * one-sentence explanation, the status — and only the actions the server allows
 * (`allowedTransitions`, ADR-FE-028): change priority, Start, Complete. Below 768 px the cards stack
 * (UI-1); nothing here resets when the 30 s poll replaces `queue`.
 */
export function ClinicQueue({
  queue,
  zone,
  isLoading,
  isError,
  onRetry,
  allowedByAppointmentId,
  highlightedId,
  isMine,
  onChangePriority,
  onStart,
  onComplete,
}: ClinicQueueProps) {
  if (isLoading) return <LoadingState variant="card" rows={4} label="Loading the queue…" />;
  if (isError && !queue) {
    return <ErrorState title="We couldn’t load the queue just now." onRetry={onRetry} />;
  }
  if (!queue) return null;

  return (
    <section aria-labelledby="queue-heading" className="flex flex-col gap-3">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h2 id="queue-heading" className="font-display text-lg font-semibold text-ink">
          {isMine ? 'My queue' : 'Clinic queue'}
        </h2>
        <p className="text-sm text-ink-muted">
          {queue.performersOnShift} {queue.performersOnShift === 1 ? 'vet' : 'vets'} on shift ·
          updated {timeOnlyInZone(queue.generatedOn, zone)}
        </p>
      </div>

      {queue.entries.length === 0 ? (
        <EmptyState
          title={isMine ? 'Nobody is waiting for you.' : 'Nobody is waiting.'}
          description="Checked-in appointments and walk-ins appear here as they arrive."
        />
      ) : (
        <ol className="m-0 flex list-none flex-col gap-3 p-0">
          {queue.entries.map((entry) => {
            const allowed = allowedByAppointmentId[entry.appointmentId];
            const waiting = entry.status === 'CheckedIn';
            return (
              <li
                key={entry.appointmentId}
                data-highlighted={highlightedId === entry.appointmentId ? 'true' : undefined}
                className={cn(
                  'flex flex-col gap-2 rounded-[var(--radius-md)] border bg-surface p-4',
                  highlightedId === entry.appointmentId
                    ? 'border-accent ring-2 ring-accent'
                    : 'border-border',
                )}
              >
                <div className="flex flex-wrap items-center gap-2">
                  <span className="flex h-7 w-7 flex-none items-center justify-center rounded-full bg-surface-sunken text-sm font-bold text-ink">
                    <span className="sr-only">Position </span>
                    {entry.position}
                  </span>
                  <PriorityBadge priority={entry.triagePriority} />
                  <AppointmentStatusBadge status={entry.status} />
                </div>
                <CompactCatHeader name={entry.catName} isHighRisk={entry.catIsHighRisk} />
                <p className="text-sm text-ink">
                  {entry.customerName} · {entry.service}
                </p>
                <p className="flex flex-wrap items-center gap-x-3 gap-y-1 text-sm text-ink-muted">
                  {entry.isWalkIn ? (
                    <span className="inline-flex items-center gap-1">
                      <Footprints size={14} aria-hidden="true" /> Walk-in
                    </span>
                  ) : (
                    <span className="inline-flex items-center gap-1">
                      <CalendarClock size={14} aria-hidden="true" /> Booked{' '}
                      {timeOnlyInZone(entry.dueOn, zone)}
                    </span>
                  )}
                  {entry.checkedInOn && (
                    <span>Checked in {timeOnlyInZone(entry.checkedInOn, zone)}</span>
                  )}
                  <span>{entry.performerName ?? 'No vet yet'}</span>
                </p>
                {waiting && (
                  <details className="text-sm">
                    <summary className="cursor-pointer font-semibold text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent">
                      {formatEstimatedWait(entry.estimatedWaitMinutes)}
                      <span className="ml-2 font-normal text-accent-ink underline underline-offset-2">
                        How is this worked out?
                      </span>
                    </summary>
                    <p className="mt-1 text-ink-muted">
                      It is an estimate: the time of the cases ahead of this one, shared between the
                      vets on shift, recalculated every time this list refreshes.
                    </p>
                  </details>
                )}
                <QueueActions
                  entry={entry}
                  allowed={allowed}
                  onChangePriority={onChangePriority}
                  onStart={onStart}
                  onComplete={onComplete}
                />
                {/* HO-EMR-002 §11.9/§13 — the two card actions only (Record vitals, Open consultation). */}
                <QueueVisitActions entry={entry} zone={zone} onStart={onStart} />
              </li>
            );
          })}
        </ol>
      )}
    </section>
  );
}

function QueueActions({
  entry,
  allowed,
  onChangePriority,
  onStart,
  onComplete,
}: {
  entry: QueueEntry;
  allowed: AppointmentAction[] | undefined;
  onChangePriority: ClinicQueueProps['onChangePriority'];
  onStart: ClinicQueueProps['onStart'];
  onComplete: ClinicQueueProps['onComplete'];
}) {
  if (allowed === undefined) {
    return <p className="text-xs text-ink-muted">Checking what can be done…</p>;
  }
  const can = (action: AppointmentAction) => allowed.includes(action);
  if (!can('SetTriagePriority') && !can('Start') && !can('Complete')) return null;
  return (
    <div className="flex flex-wrap items-end gap-3">
      {can('SetTriagePriority') && (
        <div className="flex flex-col gap-1">
          <label
            htmlFor={`priority-${entry.appointmentId}`}
            className="text-xs font-semibold text-ink-muted"
          >
            Priority
          </label>
          <select
            id={`priority-${entry.appointmentId}`}
            value={entry.triagePriority}
            onChange={(event) => onChangePriority(entry, event.target.value as TriagePriority)}
            className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-2 py-1.5 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          >
            {PRIORITY_OPTIONS.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </div>
      )}
      {can('Start') && (
        <Button size="sm" aria-label={`Start ${entry.catName}`} onClick={() => onStart(entry)}>
          Start
        </Button>
      )}
      {can('Complete') && (
        <Button
          size="sm"
          aria-label={`Complete ${entry.catName}`}
          onClick={() => onComplete(entry)}
        >
          Complete
        </Button>
      )}
    </div>
  );
}
