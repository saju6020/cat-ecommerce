'use client';

import { forwardRef, useState } from 'react';
import { Section } from '@/components/billing/PageHeader';
import { Button } from '@/components/ui/button';
import type { TenderMethod } from '@/lib/api/pos';
import { formatMoney } from '@/lib/format/money';
import { tenderMethodLabel } from '@/lib/format/orders';
import type { PosTender } from './types';

/**
 * `components/pos/PosTenderPanel.tsx` — ⭐ HO-ORD-002 §11.11, US-ORD-16. Payments taken at the counter:
 * **Cash** (the money handed over — the server works out the change) and **Card in store** (the amount
 * charged on the terminal and the **terminal slip reference**, never a card number). Amounts are kept as
 * typed; nothing is added up here, so whether they cover the total is the server's answer when the sale is
 * completed. Keyboard path: `F2` focuses the amount; **Enter** adds the payment and opens the review.
 */
export const PosTenderPanel = forwardRef<
  HTMLInputElement,
  {
    tenders: PosTender[];
    currencyCode: string;
    onAdd: (tender: Omit<PosTender, 'id'>) => void;
    onRemove: (id: string) => void;
    onReview: () => void;
    disabled: boolean;
  }
>(function PosTenderPanel({ tenders, currencyCode, onAdd, onRemove, onReview, disabled }, ref) {
  const [method, setMethod] = useState<TenderMethod>('Cash');
  const [amount, setAmount] = useState('');
  const [reference, setReference] = useState('');
  const [error, setError] = useState<string | null>(null);

  function add(): boolean {
    if (!/^\d+(\.\d{1,2})?$/.test(amount.trim())) {
      setError('Enter the amount as a number, like 1500.00.');
      return false;
    }
    if (method === 'InStoreCard' && !reference.trim()) {
      setError('Enter the card terminal slip reference.');
      return false;
    }
    setError(null);
    onAdd({ method, amount: amount.trim(), reference: method === 'InStoreCard' ? reference.trim() : '' });
    setAmount('');
    setReference('');
    return true;
  }

  const field = 'w-full rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent';
  return (
    <Section title="Payment" description="Press F2 to jump here. Enter adds the payment and opens the review.">
      <div className="grid gap-3 sm:grid-cols-2">
        <div className="flex flex-col gap-1">
          <label htmlFor="pos-tender-method" className="text-sm font-semibold">Paid with</label>
          <select id="pos-tender-method" className={field} value={method} onChange={(e) => setMethod(e.target.value as TenderMethod)}>
            <option value="Cash">Cash</option>
            <option value="InStoreCard">Card in store</option>
          </select>
        </div>
        <div className="flex flex-col gap-1">
          <label htmlFor="pos-tender-amount" className="text-sm font-semibold">{method === 'Cash' ? `Cash handed over (${currencyCode})` : `Amount charged (${currencyCode})`}</label>
          <input
            ref={ref}
            id="pos-tender-amount"
            className={field}
            inputMode="decimal"
            autoComplete="off"
            value={amount}
            disabled={disabled}
            onChange={(e) => setAmount(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') {
                e.preventDefault();
                if (add()) onReview();
              }
            }}
          />
        </div>
        {method === 'InStoreCard' && (
          <div className="flex flex-col gap-1 sm:col-span-2">
            <label htmlFor="pos-tender-ref" className="text-sm font-semibold">Terminal slip reference</label>
            <input id="pos-tender-ref" className={field} maxLength={100} value={reference} onChange={(e) => setReference(e.target.value)} />
            <p className="text-xs text-ink-muted">The number on the terminal’s slip. Never type a card number.</p>
          </div>
        )}
      </div>
      {error && <p role="alert" className="text-sm font-semibold text-danger-ink">{error}</p>}
      <div>
        <Button variant="secondary" onClick={() => add()} disabled={disabled}>Add payment</Button>
      </div>
      {tenders.length > 0 && (
        <ul aria-label="Payments taken" className="m-0 flex list-none flex-col gap-1 p-0">
          {tenders.map((t) => (
            <li key={t.id} className="flex items-center justify-between gap-2 text-sm">
              <span>
                {tenderMethodLabel(t.method)} {formatMoney({ amount: t.amount, currencyCode })}
                {t.reference ? ` · slip ${t.reference}` : ''}
              </span>
              <Button variant="ghost" size="sm" onClick={() => onRemove(t.id)}>
                Remove<span className="sr-only"> {tenderMethodLabel(t.method)} payment</span>
              </Button>
            </li>
          ))}
        </ul>
      )}
    </Section>
  );
});
