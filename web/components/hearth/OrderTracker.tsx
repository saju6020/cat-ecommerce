import * as React from 'react';
import { Check } from 'lucide-react';
import { cn } from '@/lib/utils';

export type TrackerStatus = 'done' | 'current' | 'future';

export interface TrackerStep {
  label: string;
  status: TrackerStatus;
  /** Small line under the label, e.g. a time. Only what the server sent. */
  caption?: string;
  /** Icon shown in the node when not done; the step number is used if omitted. Done shows a tick. */
  icon?: React.ReactNode;
}

const SR_STATUS: Record<TrackerStatus, string> = {
  done: 'done',
  current: 'current',
  future: 'not yet',
};

/**
 * Order progress: round nodes joined by a line (docs/design/orders/OrderDetail.dc.html). Done =
 * teal, current = apricot with a soft ring, future = dashed. The statuses come from the order's
 * server-side status (the caller maps it); this component never infers progress.
 */
export function OrderTracker({
  steps,
  label = 'Order progress',
  className,
}: {
  steps: TrackerStep[];
  label?: string;
  className?: string;
}) {
  return (
    <ol
      aria-label={label}
      className={cn('m-0 grid list-none p-0', className)}
      style={{ gridTemplateColumns: `repeat(${steps.length}, minmax(0, 1fr))` }}
    >
      {steps.map((step, index) => (
        <li
          key={step.label}
          aria-current={step.status === 'current' ? 'step' : undefined}
          data-status={step.status}
          className={cn(
            'relative flex flex-col items-center gap-2.5 text-center',
            index > 0 &&
              'before:absolute before:-left-1/2 before:top-[21px] before:h-1 before:w-full sm:before:top-[25px]',
            index > 0 && (step.status === 'future' ? 'before:bg-border' : 'before:bg-accent'),
          )}
        >
          <span
            aria-hidden="true"
            className={cn(
              'relative z-10 grid size-11 place-items-center rounded-pill text-sm font-bold sm:size-[54px]',
              step.status === 'done' && 'bg-accent text-white',
              step.status === 'current' && 'bg-warm text-ink ring-[5px] ring-warm-soft',
              step.status === 'future' &&
                'border-2 border-dashed border-border-strong bg-surface text-ink-muted',
            )}
          >
            {step.status === 'done' ? (
              <Check size={22} strokeWidth={2.5} />
            ) : (
              (step.icon ?? index + 1)
            )}
          </span>
          <strong className="text-sm">
            {step.label}
            <span className="sr-only"> ({SR_STATUS[step.status]})</span>
          </strong>
          {step.caption && <span className="text-xs text-ink-muted">{step.caption}</span>}
        </li>
      ))}
    </ol>
  );
}
