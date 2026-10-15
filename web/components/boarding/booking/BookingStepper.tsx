import { Check } from 'lucide-react';
import { cn } from '@/lib/utils';

export type BookingStep = 'search' | 'cats' | 'gate' | 'profiles' | 'review' | 'reserved';

const STEPS: Array<{ key: BookingStep; label: string }> = [
  { key: 'search', label: 'Dates and units' },
  { key: 'cats', label: 'Cats' },
  { key: 'gate', label: 'Vaccinations' },
  { key: 'profiles', label: 'Care profiles' },
  { key: 'review', label: 'Review' },
  { key: 'reserved', label: 'Reserved' },
];

/**
 * `components/boarding/booking/BookingStepper.tsx` — the progress list of the booking flow. An
 * ordered list with `aria-current="step"` on the current one; the state of every step is **text**
 * ("done", "current"), not colour alone (UI-6). The vaccinations step is listed only when the gate
 * has been involved.
 */
export function BookingStepper({ current, showGate }: { current: BookingStep; showGate: boolean }) {
  const steps = STEPS.filter((s) => s.key !== 'gate' || showGate);
  const index = steps.findIndex((s) => s.key === current);
  return (
    <nav aria-label="Booking progress">
      <ol className="flex flex-wrap gap-2">
        {steps.map((step, i) => {
          const done = i < index;
          const isCurrent = i === index;
          return (
            <li
              key={step.key}
              aria-current={isCurrent ? 'step' : undefined}
              className={cn(
                'flex items-center gap-1.5 rounded-full px-3 py-1 text-[12.5px] font-semibold',
                isCurrent && step.key === 'gate' && 'bg-danger-bg text-danger-ink',
                isCurrent && step.key !== 'gate' && 'bg-accent text-white',
                done && 'bg-accent-soft text-accent-ink',
                !done && !isCurrent && 'bg-surface-sunken text-ink-muted',
              )}
            >
              {done && <Check size={12} aria-hidden="true" />}
              {step.label}
              <span className="sr-only">
                {done ? ' (done)' : isCurrent ? ' (current step)' : ''}
              </span>
            </li>
          );
        })}
      </ol>
    </nav>
  );
}
