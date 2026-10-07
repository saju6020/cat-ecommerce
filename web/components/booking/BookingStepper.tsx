import { cn } from '@/lib/utils';

/**
 * `components/booking/BookingStepper.tsx` — HO-APT-002 §11.1. An ordered list of the booking
 * steps; a finished step names the choice made and offers **Change**, the current step is marked
 * `aria-current="step"`. Steps that were skipped (one cat, one branch) are simply not listed.
 */
export interface StepperStep {
  key: string;
  label: string;
  /** The choice made, for a finished step (e.g. the cat's name). */
  summary?: string;
  state: 'done' | 'current' | 'upcoming';
  onChange?: () => void;
}

export function BookingStepper({ steps }: { steps: StepperStep[] }) {
  return (
    <ol
      aria-label="Booking steps"
      className="m-0 mb-5 flex list-none flex-col overflow-hidden rounded-[var(--radius-md)] border border-border bg-surface p-0 sm:flex-row"
    >
      {steps.map((step, index) => (
        <li
          key={step.key}
          aria-current={step.state === 'current' ? 'step' : undefined}
          className={cn(
            'flex-1 border-b border-border px-3 py-2 text-xs leading-snug last:border-b-0 sm:border-b-0 sm:border-r sm:last:border-r-0',
            step.state === 'current' && 'bg-accent-soft font-semibold text-accent-ink',
            step.state === 'done' && 'text-ink',
            step.state === 'upcoming' && 'text-ink-muted',
          )}
        >
          <span className="block text-[11px] font-bold uppercase tracking-wide">
            {index + 1} · {step.label}
          </span>
          {step.summary && <span className="mr-2">{step.summary}</span>}
          {step.state === 'done' && step.onChange && (
            <button
              type="button"
              onClick={step.onChange}
              aria-label={`Change ${step.label.toLowerCase()}`}
              className="text-xs font-semibold text-accent-ink underline underline-offset-2 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
            >
              Change
            </button>
          )}
        </li>
      ))}
    </ol>
  );
}
