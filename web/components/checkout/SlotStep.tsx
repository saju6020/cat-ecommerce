'use client';

import { Amount } from '@/components/billing/Amount';
import { Button } from '@/components/ui/button';
import type { QuoteServiceLevel, ServiceLevel } from '@/lib/api/checkout';
import { formatDeliverySlot, formatSlotDay, serviceLevelLabel } from '@/lib/format/orders';

const RADIO = 'h-4 w-4 flex-none accent-[var(--accent)]';

/** "Delivery in 1–2 days" from the server's `leadTimeDays` (OQ-3: shown when there are no windows). */
export function leadTimeText(days: number): string {
  if (days <= 0) return 'Same-day delivery';
  return `Delivery in about ${days} ${days === 1 ? 'day' : 'days'}`;
}

/**
 * `components/checkout/SlotStep.tsx` — HO-ORD-002 §11.2 step 2, US-ORD-03 AC1/US-ORD-06 AC1. Service
 * levels as radio cards showing the **charge** (the server's string) and the lead time; for the chosen
 * level its delivery windows, labelled in branch time. A level with no windows needs no choice (OQ-3):
 * the step says *"Delivery in about N days"* instead. Nothing here prices or schedules anything.
 */
export function SlotStep({
  levels,
  selectedLevel,
  onLevel,
  slotDate,
  slotWindowStart,
  onSlot,
  zone,
  onContinue,
  continueDisabledReason,
}: {
  levels: QuoteServiceLevel[];
  selectedLevel: ServiceLevel | null;
  onLevel: (level: ServiceLevel) => void;
  slotDate: string | undefined;
  slotWindowStart: string | undefined;
  onSlot: (date: string, windowStart: string) => void;
  zone: string;
  onContinue: () => void;
  continueDisabledReason: string | null;
}) {
  const chosen = levels.find((level) => level.level === selectedLevel);
  const dates = chosen ? [...new Set(chosen.slots.map((slot) => slot.date))] : [];
  const activeDate = slotDate ?? dates[0];

  return (
    <div className="flex flex-col gap-4">
      <fieldset className="m-0 flex min-w-0 flex-col gap-2 border-0 p-0">
        <legend className="mb-1 text-sm font-bold text-ink">Delivery option</legend>
        {levels.map((level) => (
          <label
            key={level.level}
            className={`flex items-start gap-3 rounded-[var(--radius-md)] border p-3 text-sm ${selectedLevel === level.level ? 'border-accent bg-accent-soft' : 'border-border-strong bg-surface'}`}
          >
            <input
              type="radio"
              name="service-level"
              className={`${RADIO} mt-0.5`}
              checked={selectedLevel === level.level}
              onChange={() => onLevel(level.level)}
            />
            <span className="flex min-w-0 flex-1 flex-wrap items-baseline justify-between gap-x-4">
              <span>
                <b className="block text-ink">{serviceLevelLabel(level.level)}</b>
                <span className="text-ink-muted">
                  {level.slots.length === 0
                    ? leadTimeText(level.leadTimeDays)
                    : 'Choose a delivery time window'}
                </span>
              </span>
              <b className="text-ink">
                <Amount money={level.charge} />
              </b>
            </span>
          </label>
        ))}
      </fieldset>

      {chosen && chosen.slots.length > 0 && (
        <fieldset className="m-0 flex min-w-0 flex-col gap-3 border-0 p-0">
          <legend className="mb-1 text-sm font-bold text-ink">{`Delivery time (${zone})`}</legend>
          <div role="radiogroup" aria-label="Delivery date" className="flex flex-wrap gap-2">
            {dates.map((date) => {
              const sample = chosen.slots.find((slot) => slot.date === date);
              return (
                <button
                  key={date}
                  type="button"
                  role="radio"
                  aria-checked={activeDate === date}
                  onClick={() => {
                    const first = chosen.slots.find((slot) => slot.date === date);
                    if (first) onSlot(first.date, first.windowStart);
                  }}
                  className={`min-h-10 rounded-[var(--radius-sm)] border px-3 text-sm font-semibold ${activeDate === date ? 'border-accent bg-accent-soft text-accent-ink' : 'border-border-strong bg-surface text-ink'}`}
                >
                  {sample ? formatSlotDay(sample.date) : date}
                </button>
              );
            })}
          </div>
          <div role="radiogroup" aria-label="Time window" className="flex flex-wrap gap-2">
            {chosen.slots
              .filter((slot) => slot.date === activeDate)
              .map((slot) => {
                const selected = slotDate === slot.date && slotWindowStart === slot.windowStart;
                return (
                  <button
                    key={`${slot.date}-${slot.windowStart}`}
                    type="button"
                    role="radio"
                    aria-checked={selected}
                    onClick={() => onSlot(slot.date, slot.windowStart)}
                    className={`min-h-10 rounded-[var(--radius-sm)] border px-3 text-sm font-semibold ${selected ? 'border-accent bg-accent-soft text-accent-ink' : 'border-border-strong bg-surface text-ink'}`}
                  >
                    {`${slot.windowStart}–${slot.windowEnd}`}
                  </button>
                );
              })}
          </div>
          {slotDate && slotWindowStart && (
            <p className="text-sm text-ink-muted" role="status">
              {`Selected: ${formatDeliverySlot(slotDate, slotWindowStart, chosen.slots.find((s) => s.date === slotDate && s.windowStart === slotWindowStart)?.windowEnd ?? '', zone)}`}
            </p>
          )}
        </fieldset>
      )}

      <div className="flex flex-wrap items-center gap-3">
        <Button disabled={continueDisabledReason !== null} aria-describedby={continueDisabledReason ? 'slot-why' : undefined} onClick={onContinue}>
          Continue
        </Button>
        {continueDisabledReason && (
          <p id="slot-why" className="text-sm text-ink-muted">
            {continueDisabledReason}
          </p>
        )}
      </div>
    </div>
  );
}
