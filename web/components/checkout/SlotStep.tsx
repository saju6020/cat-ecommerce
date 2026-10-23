'use client';

import { Clock, Truck, Zap } from 'lucide-react';
import { Amount } from '@/components/billing/Amount';
import { cardClass } from '@/components/hearth/card';
import { IconTile } from '@/components/hearth/IconTile';
import { OptionCard } from '@/components/hearth/OptionCard';
import type { QuoteServiceLevel, ServiceLevel } from '@/lib/api/checkout';
import { formatDeliverySlot, formatSlotDay, serviceLevelLabel } from '@/lib/format/orders';
import { cn } from '@/lib/utils';
import { StepFooter, StepLayout, type RenderSummary } from './StepLayout';

/** "Delivery in 1–2 days" from the server's `leadTimeDays` (OQ-3: shown when there are no windows). */
export function leadTimeText(days: number): string {
  if (days <= 0) return 'Same-day delivery';
  return `Delivery in about ${days} ${days === 1 ? 'day' : 'days'}`;
}

const CHIP =
  'inline-flex items-center justify-center rounded-pill border px-5 text-[15px] font-semibold ' +
  'min-h-12 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent';
const CHIP_OFF = 'border-border-strong bg-surface text-ink hover:border-ink-muted';
const CHIP_ON = 'border-warm bg-warm-soft text-warm-ink ring-1 ring-warm';

/**
 * `components/checkout/SlotStep.tsx` — HO-ORD-002 §11.2 step 2, US-ORD-03 AC1/US-ORD-06 AC1. Service
 * levels as choice cards showing the **charge** (the server's string) and the lead time; for the chosen
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
  continueLabel,
  onBack,
  renderSummary,
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
  continueLabel?: string;
  onBack?: () => void;
  renderSummary?: RenderSummary;
}) {
  const chosen = levels.find((level) => level.level === selectedLevel);
  const dates = chosen ? [...new Set(chosen.slots.map((slot) => slot.date))] : [];
  const activeDate = slotDate ?? dates[0];

  return (
    <StepLayout
      renderSummary={renderSummary}
      footer={
        <StepFooter
          id="slot"
          label={continueLabel}
          onContinue={onContinue}
          disabledReason={continueDisabledReason}
          back={onBack ? { label: 'Back', onClick: onBack } : undefined}
        />
      }
    >
      <fieldset className="m-0 flex min-w-0 flex-col gap-3 border-0 p-0">
        <legend className="mb-3 text-base font-bold text-ink">Delivery option</legend>
        <div className="grid gap-3.5 sm:grid-cols-2">
          {levels.map((level) => (
            <OptionCard
              key={level.level}
              name="service-level"
              checked={selectedLevel === level.level}
              onChange={() => onLevel(level.level)}
              icon={
                level.level === 'Express' ? (
                  <Zap size={20} aria-hidden="true" />
                ) : (
                  <Truck size={20} aria-hidden="true" />
                )
              }
              title={serviceLevelLabel(level.level)}
              trailing={<Amount money={level.charge} />}
            >
              {level.slots.length === 0
                ? leadTimeText(level.leadTimeDays)
                : 'Choose a delivery time window'}
            </OptionCard>
          ))}
        </div>
      </fieldset>

      {chosen && chosen.slots.length > 0 && (
        <fieldset className={cn(cardClass, 'm-0 flex min-w-0 flex-col gap-3.5')}>
          <legend className="sr-only">{`Delivery time (${zone})`}</legend>
          <div className="flex items-center gap-3.5">
            <IconTile shape="tile" tone="warm" size="md">
              <Clock size={20} aria-hidden="true" />
            </IconTile>
            <div>
              <p className="m-0 text-base font-bold text-ink">When should we come?</p>
              <p className="m-0 text-[13px] text-ink-muted">{`All times ${zone}`}</p>
            </div>
          </div>
          <div role="radiogroup" aria-label="Delivery date" className="flex flex-wrap gap-2.5">
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
                  className={cn(
                    CHIP,
                    'min-h-16 rounded-2xl',
                    activeDate === date ? CHIP_ON : CHIP_OFF,
                  )}
                >
                  {sample ? formatSlotDay(sample.date) : date}
                </button>
              );
            })}
          </div>
          <div role="radiogroup" aria-label="Time window" className="flex flex-wrap gap-2.5">
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
                    className={cn(CHIP, selected ? CHIP_ON : CHIP_OFF)}
                  >
                    {`${slot.windowStart}–${slot.windowEnd}`}
                  </button>
                );
              })}
          </div>
          {slotDate && slotWindowStart && (
            <p className="m-0 text-sm text-ink-muted" role="status">
              {`Selected: ${formatDeliverySlot(slotDate, slotWindowStart, chosen.slots.find((s) => s.date === slotDate && s.windowStart === slotWindowStart)?.windowEnd ?? '', zone)}`}
            </p>
          )}
        </fieldset>
      )}
    </StepLayout>
  );
}
