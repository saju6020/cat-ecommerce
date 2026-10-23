'use client';

import { Banknote, Landmark, Store, TriangleAlert } from 'lucide-react';
import { OptionCard } from '@/components/hearth/OptionCard';
import type { PaymentMethodChoice, PaymentOption } from '@/lib/api/checkout';
import { paymentMethodExplanation, paymentMethodLabel } from '@/lib/format/orders';
import { StepFooter, StepLayout, type RenderSummary } from './StepLayout';

function MethodIcon({ method }: { method: PaymentMethodChoice }) {
  if (method === 'BankTransfer') return <Landmark size={20} aria-hidden="true" />;
  if (method === 'PayOnCollection') return <Store size={22} aria-hidden="true" />;
  return <Banknote size={20} aria-hidden="true" />;
}

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
  continueLabel,
  onBack,
  renderSummary,
}: {
  options: PaymentOption[];
  selected: PaymentMethodChoice | undefined;
  onSelect: (method: PaymentMethodChoice) => void;
  onContinue: () => void;
  continueDisabledReason: string | null;
  continueLabel?: string;
  onBack?: () => void;
  renderSummary?: RenderSummary;
}) {
  return (
    <StepLayout
      renderSummary={renderSummary}
      footer={
        <StepFooter
          id="payment"
          label={continueLabel}
          onContinue={onContinue}
          disabledReason={continueDisabledReason}
          back={onBack ? { label: 'Back', onClick: onBack } : undefined}
        />
      }
    >
      <fieldset className="m-0 flex min-w-0 flex-col gap-3.5 border-0 p-0">
        <legend className="mb-3 text-base font-bold text-ink">How will you pay?</legend>
        {options.map((option) => {
          const whyId = `pay-${option.method}-why`;
          return (
            <OptionCard
              key={option.method}
              name="payment-method"
              checked={selected === option.method}
              disabled={!option.available}
              onChange={() => onSelect(option.method)}
              icon={<MethodIcon method={option.method} />}
              title={paymentMethodLabel(option.method)}
              whyId={whyId}
              why={
                option.available ? undefined : (
                  <>
                    <TriangleAlert size={15} aria-hidden="true" className="mt-0.5 flex-none" />
                    {option.message ?? 'This payment method is not available for this order.'}
                  </>
                )
              }
            >
              {paymentMethodExplanation(option.method)}
            </OptionCard>
          );
        })}
      </fieldset>
    </StepLayout>
  );
}
