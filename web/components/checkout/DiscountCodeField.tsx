'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import type { QuoteDiscount } from '@/lib/api/checkout';
import { describeBlockingReason } from '@/lib/api/errors';

/**
 * `components/checkout/DiscountCodeField.tsx` — HO-ORD-002 §11.2, US-ORD-05. Apply or remove a code. A
 * rejected code shows **the server's specific reason inline** and the summary is unchanged (the quote
 * simply did not apply it). The code is the customer's text, upper-cased as typed; whether it is valid
 * is entirely the server's answer.
 */
export function DiscountCodeField({
  appliedCode,
  discount,
  onApply,
  onRemove,
  checking,
}: {
  appliedCode: string | undefined;
  discount: QuoteDiscount | null;
  onApply: (code: string) => void;
  onRemove: () => void;
  checking: boolean;
}) {
  const [text, setText] = useState('');

  if (appliedCode) {
    const rejected = discount && !discount.applied;
    return (
      <div className="flex flex-col gap-2">
        <p className="text-sm font-semibold text-ink">Discount code</p>
        <div className="flex flex-wrap items-center gap-2 text-sm">
          <span className="rounded-full border border-border-strong bg-surface px-3 py-1 font-semibold text-ink">
            {appliedCode}
          </span>
          {discount?.applied && <span className="font-semibold text-accent-ink">Applied</span>}
          <Button variant="ghost" size="sm" onClick={onRemove} aria-label={`Remove discount code ${appliedCode}`}>
            Remove
          </Button>
        </div>
        {rejected && (
          <p role="alert" className="text-sm font-semibold text-danger-ink">
            {discount.message || describeBlockingReason(discount.reasonCode ?? '')}
          </p>
        )}
      </div>
    );
  }

  return (
    <form
      className="flex flex-col gap-1.5"
      onSubmit={(event) => {
        event.preventDefault();
        const code = text.trim().toUpperCase();
        if (code) {
          onApply(code);
          setText('');
        }
      }}
    >
      <label htmlFor="discount-code" className="text-sm font-semibold text-ink">
        Discount code
      </label>
      <div className="flex gap-2">
        <input
          id="discount-code"
          value={text}
          onChange={(event) => setText(event.target.value.toUpperCase())}
          autoComplete="off"
          maxLength={40}
          className="min-w-0 flex-1 rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm uppercase text-ink"
        />
        <Button type="submit" variant="secondary" disabled={!text.trim() || checking}>
          Apply
        </Button>
      </div>
    </form>
  );
}
