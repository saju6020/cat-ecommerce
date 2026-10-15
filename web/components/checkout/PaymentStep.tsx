'use client';

import { Button } from '@/components/ui/button';
import type { PaymentMethodChoice, PaymentOption } from '@/lib/api/checkout';
import { paymentMethodExplanation, paymentMethodLabel } from '@/lib/format/orders';

/**
 * `components/checkout/PaymentStep.tsx` — ⭐ HO-ORD-002 §11.2 step 3, US-ORD-07. **There is no payment
 * step in the card sense** (ADR-FE-092): this is a choice of *how you will pay a person later* —
 * *Cash on delivery*, *Bank transfer*, *Pay when you collect*. Options come from the quote; an
 * unavailable one is shown **disabled with the server's reason** (*"Cash on delivery is available for
 * orders up to BDT 5,000.00"*). No card option, no online payment control exists anywhere.
 */
export function PaymentStep({
  options,
  selected,
  onSelect,
  onContinue,
  continueDisabledReason,
}: {
  options: PaymentOption[];
  selected: PaymentMethodChoice | undefined;
  onSelect: (method: PaymentMethodChoice) => void;
  onContinue: () => void;
  continueDisabledReason: string | null;
}) {
  return (
    <div className="flex flex-col gap-4">
      <fieldset className="m-0 flex min-w-0 flex-col gap-2 border-0 p-0">
        <legend className="mb-1 text-sm font-bold text-ink">How will you pay?</legend>
        {options.map((option) => {
          const whyId = `pay-${option.method}-why`;
          return (
            <label
              key={option.method}
              className={`flex items-start gap-3 rounded-[var(--radius-md)] border p-3 text-sm ${
                !option.available
                  ? 'border-border bg-surface-sunken text-ink-muted'
                  : selected === option.method
                    ? 'border-accent bg-accent-soft'
                    : 'border-border-strong bg-surface'
              }`}
            >
              <input
                type="radio"
                name="payment-method"
                className="mt-0.5 h-4 w-4 flex-none accent-[var(--accent)]"
                disabled={!option.available}
                checked={selected === option.method}
                aria-describedby={whyId}
                onChange={() => onSelect(option.method)}
              />
              <span className="min-w-0">
                <b className={`block ${option.available ? 'text-ink' : 'text-ink-muted'}`}>
                  {paymentMethodLabel(option.method)}
                </b>
                <span id={whyId} className="text-ink-muted">
                  {option.available
                    ? paymentMethodExplanation(option.method)
                    : (option.message ?? 'This payment method is not available for this order.')}
                </span>
              </span>
            </label>
          );
        })}
      </fieldset>
      <div className="flex flex-wrap items-center gap-3">
        <Button disabled={continueDisabledReason !== null} aria-describedby={continueDisabledReason ? 'payment-why' : undefined} onClick={onContinue}>
          Continue
        </Button>
        {continueDisabledReason && (
          <p id="payment-why" className="text-sm text-ink-muted">
            {continueDisabledReason}
          </p>
        )}
      </div>
    </div>
  );
}
