'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import type { InvoiceDetail } from '@/lib/api/billing-staff';
import { describeBillingError } from '@/lib/api/errors';
import { Amount } from './Amount';
import { FormRootError, TextField } from './Fields';

/**
 * `components/billing/LoyaltyRedemptionControl.tsx` — HO-BIL-002 §11.2 (US-BIL-10 AC2, E-E1). Shown
 * only when loyalty is enabled and the draft exists. The points go to the server, which checks the
 * balance, the minimum and the invoice's net and returns the redemption (points and the money they
 * are worth) and the re-computed draft — nothing is worked out here.
 */
export function LoyaltyRedemptionControl({
  balance,
  redemption,
  disabled,
  onApply,
  onRemove,
}: {
  balance: number | null;
  redemption: InvoiceDetail['loyaltyRedemption'];
  disabled?: boolean;
  onApply: (points: string) => Promise<unknown>;
  onRemove: () => Promise<unknown>;
}) {
  const [points, setPoints] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function run(action: () => Promise<unknown>, clear: boolean) {
    setBusy(true);
    setError(null);
    try {
      await action();
      if (clear) setPoints('');
    } catch (failure) {
      setError(describeBillingError(failure));
    } finally {
      setBusy(false);
    }
  }

  return (
    <section aria-labelledby="loyalty-heading" className="flex flex-col gap-2">
      <h3 id="loyalty-heading" className="text-sm font-semibold text-ink">
        Loyalty points
      </h3>
      <p className="text-sm text-ink-muted">Balance: {balance ?? 0} points.</p>
      {redemption ? (
        <div className="flex flex-wrap items-center gap-3 text-sm" role="status">
          <span>
            Redeeming {redemption.points} points — <Amount money={redemption.value} className="font-semibold" /> off.
          </span>
          <Button variant="secondary" size="sm" disabled={disabled || busy} onClick={() => void run(onRemove, false)}>
            Remove redemption
          </Button>
        </div>
      ) : (
        <form
          className="flex flex-wrap items-end gap-2"
          onSubmit={(event) => {
            event.preventDefault();
            if (points.trim()) void run(() => onApply(points.trim()), true);
          }}
        >
          <TextField
            id="loyalty-points"
            label="Points to redeem"
            inputMode="numeric"
            autoComplete="off"
            value={points}
            onChange={(event) => setPoints(event.target.value.replace(/\D/g, ''))}
            wrapperClassName="w-40"
          />
          <Button type="submit" variant="secondary" size="sm" disabled={disabled || busy || !points}>
            Redeem points
          </Button>
        </form>
      )}
      <FormRootError message={error ?? undefined} />
    </section>
  );
}
