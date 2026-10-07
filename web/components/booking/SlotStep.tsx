'use client';

import { forwardRef } from 'react';
import { Clock } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { LoadingState } from '@/components/common/LoadingState';
import { ErrorState } from '@/components/common/ErrorState';
import { dayPeriodOf, formatBranchDateLong, type DayPeriod } from '@/lib/format/appointment';
import { timeOnlyInZone } from '@/lib/format/time';
import type { AvailableSlots, SlotGroup, SlotPerformer } from '@/lib/api/booking';
import { DateStrip } from './DateStrip';

const PERIOD_LABEL: Record<DayPeriod, string> = {
  morning: 'Morning',
  afternoon: 'Afternoon',
  evening: 'Evening',
};

export interface SlotStepProps {
  zone: string;
  todayIso: string;
  date: string;
  onDateChange: (dateIso: string) => void;
  openWeekdays?: number[];
  performers: SlotPerformer[];
  performerId: string;
  onPerformerChange: (performerId: string) => void;
  slots: AvailableSlots | undefined;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
  /** `performer` is the one shown on the button; `null` means "Any vet" — the server chooses. */
  onSelectSlot: (slot: SlotGroup, performer: SlotPerformer | null) => void;
  /** True while a hold request is in flight. */
  isSelecting?: boolean;
  /** A message that must be heard (a lost race, an ended hold) — rendered above the list. */
  notice?: string | null;
  nextDayWithSlots?: string | null;
  nextDayLoading?: boolean;
  onJoinWaitlist?: () => void;
  helpText?: string;
}

/**
 * ⭐ `components/booking/SlotStep.tsx` — HO-APT-002 §11.1 (US-CLN-02). Date strip, vet filter
 * ("Any vet" default) and the day's slots as real buttons in labelled groups, never a canvas
 * (UI-6). Every time is the branch's and the zone is stated once at the top (ADR-FE-014). It never
 * works out availability: whatever the server returned is shown, and an empty day offers the
 * waitlist and the next day that has times. Also used, unchanged, by the reschedule picker.
 *
 * The slot list container is focusable (`tabIndex={-1}`) and receives the ref the container uses to
 * return focus to it after a `409` (U-A5).
 */
export const SlotStep = forwardRef<HTMLDivElement, SlotStepProps>(function SlotStep(
  {
    zone,
    todayIso,
    date,
    onDateChange,
    openWeekdays,
    performers,
    performerId,
    onPerformerChange,
    slots,
    isLoading,
    isError,
    onRetry,
    onSelectSlot,
    isSelecting,
    notice,
    nextDayWithSlots,
    nextDayLoading,
    onJoinWaitlist,
    helpText,
  },
  listRef,
) {
  const groups = (['morning', 'afternoon', 'evening'] as DayPeriod[]).map((period) => ({
    period,
    slots: (slots?.slots ?? []).filter((slot) => dayPeriodOf(slot.startsOn, zone) === period),
  }));
  const dateLabel = formatBranchDateLong(date);
  const empty = !!slots && slots.slots.length === 0;
  const chosenVet = performers.find((p) => p.id === performerId) ?? null;

  return (
    <div className="flex flex-col gap-4">
      <p className="flex items-center gap-2 rounded-[var(--radius-sm)] bg-surface-sunken px-3 py-2 text-sm text-ink">
        <Clock size={15} aria-hidden="true" className="flex-none" />
        <span>
          All times are clinic time — <strong>{zone}</strong>, whatever time zone your phone is in.
        </span>
      </p>

      <div className="flex flex-col gap-4 rounded-[var(--radius-md)] border border-border bg-surface p-4">
        <DateStrip
          todayIso={todayIso}
          selectedDate={date}
          onSelect={onDateChange}
          openWeekdays={openWeekdays}
        />

        <div className="flex flex-col gap-1.5">
          <label htmlFor="slot-vet" className="text-sm font-semibold text-ink">
            Vet or nurse
          </label>
          <select
            id="slot-vet"
            value={performerId}
            onChange={(event) => onPerformerChange(event.target.value)}
            className="w-full max-w-xs rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          >
            <option value="">Any vet (fastest)</option>
            {performers.map((performer) => (
              <option key={performer.id} value={performer.id}>
                {performer.name}
              </option>
            ))}
          </select>
          {!chosenVet && (
            <p className="text-xs text-ink-muted">
              With &ldquo;Any vet&rdquo;, the clinic assigns whoever is free; the name shown is who
              it will most likely be.
            </p>
          )}
        </div>

        {notice && (
          <p
            role="alert"
            className="rounded-[var(--radius-sm)] bg-warning-bg px-3 py-2 text-sm font-semibold text-warning-ink"
          >
            {notice}
          </p>
        )}

        <div
          ref={listRef}
          tabIndex={-1}
          role="group"
          aria-labelledby="slot-list-heading"
          className="outline-none focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-accent"
        >
          <h2 id="slot-list-heading" className="text-[15px] font-semibold text-ink">
            Times on {dateLabel}
          </h2>

          {isLoading && (
            <LoadingState variant="card" rows={3} label="Loading times…" className="mt-3" />
          )}
          {isError && !slots && (
            <ErrorState
              className="mt-3"
              title="We couldn’t load the times just now."
              onRetry={onRetry}
            />
          )}

          {empty && (
            <div className="mt-3 flex flex-col gap-3">
              <p role="status" className="text-sm text-ink">
                No times left on {dateLabel}.
              </p>
              {nextDayLoading && (
                <p className="text-sm text-ink-muted">Looking for the next day…</p>
              )}
              {nextDayWithSlots && (
                <p className="text-sm text-ink">
                  The next day with times is{' '}
                  <button
                    type="button"
                    onClick={() => onDateChange(nextDayWithSlots)}
                    className="font-semibold text-accent-ink underline underline-offset-2 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
                  >
                    {formatBranchDateLong(nextDayWithSlots)}
                  </button>
                  .
                </p>
              )}
              {onJoinWaitlist && (
                <div>
                  <Button variant="secondary" onClick={onJoinWaitlist}>
                    Join the waitlist
                  </Button>
                </div>
              )}
            </div>
          )}

          {slots && !empty && (
            <div className="mt-2 flex flex-col gap-4">
              {groups.map(({ period, slots: groupSlots }) => (
                <section key={period} aria-labelledby={`slot-period-${period}`}>
                  <h3
                    id={`slot-period-${period}`}
                    className="mb-2 text-[11px] font-bold uppercase tracking-wide text-ink-muted"
                  >
                    {PERIOD_LABEL[period]} <span className="normal-case">({zone})</span>
                  </h3>
                  {groupSlots.length === 0 ? (
                    <p className="text-sm text-ink-muted">
                      None left in the {PERIOD_LABEL[period].toLowerCase()} on this day.
                    </p>
                  ) : (
                    <div className="flex flex-wrap gap-2">
                      {groupSlots.map((slot) => {
                        const shown = chosenVet ?? slot.performers[0] ?? null;
                        return (
                          <button
                            key={slot.startsOn}
                            type="button"
                            disabled={isSelecting}
                            onClick={() => onSelectSlot(slot, chosenVet)}
                            className="rounded-[var(--radius-sm)] border border-accent bg-surface px-3 py-2 text-[13.5px] font-semibold text-accent-ink hover:bg-accent-soft disabled:opacity-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
                          >
                            {timeOnlyInZone(slot.startsOn, zone)}
                            {shown && (
                              <span className="font-normal text-ink-muted"> · {shown.name}</span>
                            )}
                          </button>
                        );
                      })}
                    </div>
                  )}
                </section>
              ))}
            </div>
          )}
        </div>
        {helpText && <p className="text-xs text-ink-muted">{helpText}</p>}
      </div>
    </div>
  );
});
