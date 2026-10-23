import { Check } from 'lucide-react';
import { cn } from '@/lib/utils';

export type PillStepStatus = 'done' | 'current' | 'todo' | 'skipped';

export interface PillStep {
  label: string;
  status: PillStepStatus;
  /** Small second line; defaults to Done / You are here / Skipped (nothing for todo). */
  hint?: string;
}

const SR_STATUS: Record<PillStepStatus, string> = {
  done: 'done',
  current: 'current step',
  todo: 'not started',
  skipped: 'skipped',
};
const DEFAULT_HINT: Record<PillStepStatus, string | undefined> = {
  done: 'Done',
  current: 'You are here',
  todo: undefined,
  skipped: 'Skipped',
};

/**
 * The checkout stepper (docs/design/orders/CheckoutReview.dc.html). Done = teal dot with a tick,
 * current = warm-soft with a warm-ink numbered dot, todo = white, skipped = sunken and dashed. The
 * step number stays in the accessible name ("Step 2, done: Delivery time") even though the dot
 * shows a tick.
 */
export function PillSteps({
  steps,
  label = 'Checkout steps',
  className,
}: {
  steps: PillStep[];
  label?: string;
  className?: string;
}) {
  return (
    <ol
      aria-label={label}
      className={cn('m-0 grid list-none gap-3 p-0 sm:grid-cols-2 lg:grid-cols-4', className)}
    >
      {steps.map((step, index) => {
        const hint = step.hint ?? DEFAULT_HINT[step.status];
        return (
          <li
            key={step.label}
            aria-current={step.status === 'current' ? 'step' : undefined}
            data-status={step.status}
            className={cn(
              'flex min-w-0 items-center gap-3 rounded-pill border py-2.5 pl-2.5 pr-4',
              step.status === 'current' && 'border-warm bg-warm-soft',
              step.status === 'skipped' && 'border-dashed border-border bg-surface-sunken',
              (step.status === 'done' || step.status === 'todo') && 'border-border bg-surface',
            )}
          >
            <span
              aria-hidden="true"
              className={cn(
                'grid size-8 flex-none place-items-center rounded-pill border text-sm font-bold',
                step.status === 'done' && 'border-accent bg-accent text-white',
                step.status === 'current' && 'border-warm-ink bg-warm-ink text-white',
                (step.status === 'todo' || step.status === 'skipped') &&
                  'border-border-strong bg-surface text-ink-muted',
              )}
            >
              {step.status === 'done' ? <Check size={16} strokeWidth={3} /> : index + 1}
            </span>
            <span className="flex min-w-0 flex-col">
              <strong className="text-sm">
                <span className="sr-only">
                  Step {index + 1}, {SR_STATUS[step.status]}:{' '}
                </span>
                {step.label}
              </strong>
              {hint && <span className="truncate text-xs text-ink-muted">{hint}</span>}
            </span>
          </li>
        );
      })}
    </ol>
  );
}
