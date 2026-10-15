'use client';

import type { ReactNode } from 'react';
import { Check } from 'lucide-react';
import { Button } from '@/components/ui/button';
import type { CheckoutStep } from '@/lib/hooks/useCheckout';

export type StepStatus = 'current' | 'done' | 'todo' | 'skipped';

export interface StepDescriptor {
  id: CheckoutStep;
  title: string;
  status: StepStatus;
  /** One line shown once the step has been passed (and for a skipped step, why). */
  summary: string | null;
  body: ReactNode;
}

/**
 * `components/checkout/CheckoutStepper.tsx` — HO-ORD-002 §11.2. Four addressable steps (`?step=`), each
 * **summarised once passed and re-openable** with a *Change* button. It is an ordered list: the current
 * step is marked `aria-current="step"` and in words, never colour alone, and the current step's body is
 * the only one in the document.
 */
export function CheckoutStepper({
  steps,
  onOpen,
}: {
  steps: StepDescriptor[];
  onOpen: (step: CheckoutStep) => void;
}) {
  return (
    <ol aria-label="Checkout steps" className="m-0 flex list-none flex-col gap-3 p-0">
      {steps.map((step, index) => (
        <li
          key={step.id}
          aria-current={step.status === 'current' ? 'step' : undefined}
          className={`rounded-[var(--radius-md)] border bg-surface p-4 ${step.status === 'current' ? 'border-accent shadow-[0_0_0_1px_var(--accent)]' : 'border-border'}`}
        >
          <div className="flex flex-wrap items-center justify-between gap-2">
            <h2 className="flex items-center gap-2 font-display text-lg font-semibold text-ink">
              <span
                aria-hidden="true"
                className={`flex h-6 w-6 items-center justify-center rounded-full text-xs font-bold ${step.status === 'done' ? 'bg-accent text-white' : 'border border-border-strong text-ink-muted'}`}
              >
                {step.status === 'done' ? <Check size={14} /> : index + 1}
              </span>
              <span>
                {step.title}
                <span className="sr-only">
                  {step.status === 'current'
                    ? ' (current step)'
                    : step.status === 'done'
                      ? ' (completed)'
                      : step.status === 'skipped'
                        ? ' (not needed)'
                        : ' (not started)'}
                </span>
              </span>
            </h2>
            {step.status === 'done' && (
              <Button variant="ghost" size="sm" onClick={() => onOpen(step.id)} aria-label={`Change ${step.title.toLowerCase()}`}>
                Change
              </Button>
            )}
          </div>
          {step.status !== 'current' && step.summary && (
            <p className={`mt-1 text-sm [overflow-wrap:anywhere] ${step.status === 'skipped' ? 'text-ink-muted' : 'text-ink-muted'}`}>
              {step.summary}
            </p>
          )}
          {step.status === 'current' && <div className="mt-3">{step.body}</div>}
        </li>
      ))}
    </ol>
  );
}
