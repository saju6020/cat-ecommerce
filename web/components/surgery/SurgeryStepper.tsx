import { Check } from 'lucide-react';
import { cn } from '@/lib/utils';

const STEPS = [
  { status: 'Scheduled', label: 'Scheduled' },
  { status: 'PreOp', label: 'Pre-op' },
  { status: 'InTheatre', label: 'In theatre' },
  { status: 'Recovery', label: 'Recovery' },
  { status: 'Discharged', label: 'Discharged' },
] as const;

/**
 * `SurgeryStepper` — HO-SRG-002 §11.3. *Scheduled → Pre-op → In theatre → Recovery → Discharged*, with
 * the current step stated in words ("Current") and done steps ticked and labelled "Done" for assistive
 * technology — never colour alone (UI-6). A cancelled surgery shows no stepper (the header says so).
 */
export function SurgeryStepper({ status }: { status: string }) {
  const index = STEPS.findIndex((step) => step.status === status);
  if (index < 0) return null;
  return (
    <ol
      aria-label="Progress"
      className="grid grid-cols-1 overflow-hidden rounded-[var(--radius-md)] border border-border bg-surface sm:grid-cols-5"
    >
      {STEPS.map((step, i) => {
        const done = i < index;
        const current = i === index;
        return (
          <li
            key={step.status}
            aria-current={current ? 'step' : undefined}
            className={cn(
              'flex items-center gap-2 border-b border-border px-3 py-2 text-sm last:border-b-0 sm:border-b-0 sm:border-r sm:last:border-r-0',
              current && 'bg-accent-soft font-bold text-accent-ink',
              done && 'text-ink-muted',
              !done && !current && 'text-ink-muted',
            )}
          >
            {done && <Check size={14} className="text-accent" aria-hidden="true" />}
            <span>{step.label}</span>
            {done && <span className="sr-only">(done)</span>}
            {current && (
              <span className="ml-auto rounded border border-current px-1 text-[10px] font-bold uppercase tracking-wide">
                Current
              </span>
            )}
          </li>
        );
      })}
    </ol>
  );
}
