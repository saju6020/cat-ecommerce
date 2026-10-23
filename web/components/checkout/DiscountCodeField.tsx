'use client';

import { useState } from 'react';
import { TriangleAlert } from 'lucide-react';
import { pillInputClass } from '@/components/hearth/fields';
import { Button } from '@/components/ui/button';
import type { QuoteDiscount } from '@/lib/api/checkout';
import { describeBlockingReason } from '@/lib/api/errors';
import { cn } from '@/lib/utils';

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
        <p className="m-0 text-[13px] font-semibold text-ink">Discount code</p>
        <div className="flex flex-wrap items-center gap-2 text-sm">
          <span className="rounded-pill border border-border-strong bg-surface px-3.5 py-1.5 font-semibold text-ink">
            {appliedCode}
          </span>
          {discount?.applied && <span className="font-semibold text-accent-ink">Applied</span>}
          <Button
            variant="pillSecondary"
            size="pillMd"
            onClick={onRemove}
            aria-label={`Remove discount code ${appliedCode}`}
          >
            Remove
          </Button>
        </div>
        {rejected && (
          <p
            role="alert"
            className="m-0 flex items-start gap-1.5 text-[13px] font-semibold text-danger-ink"
          >
            <TriangleAlert size={15} aria-hidden="true" className="mt-0.5 flex-none" />
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
      <label htmlFor="discount-code" className="text-[13px] font-semibold text-ink">
        Discount code
      </label>
      <div className="flex gap-2">
        <input
          id="discount-code"
          value={text}
          onChange={(event) => setText(event.target.value.toUpperCase())}
          autoComplete="off"
          maxLength={40}
          className={cn(pillInputClass, 'min-w-0 flex-1 uppercase')}
        />
        <Button
          type="submit"
          variant="pillSecondary"
          size="pillMd"
          disabled={!text.trim() || checking}
        >
          Apply
        </Button>
      </div>
    </form>
  );
}
