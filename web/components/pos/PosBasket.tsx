'use client';

import { Minus, Plus, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { Amount } from '@/components/billing/Amount';
import { Button } from '@/components/ui/button';
import type { CounterQuote } from '@/lib/api/pos';
import type { CatListItem } from '@/lib/api/cats';
import { describeExcludedReason } from '@/lib/api/errors';
import type { BasketLine } from './types';

/**
 * `components/pos/PosBasket.tsx` — HO-ORD-002 §11.11. The basket rows: product, variant, the batch the
 * server says will be used, a quantity stepper with named buttons, remove, and the line total **from the
 * server's counter quote** (matched by SKU and cat). A prescription row asks for the customer's cat (the
 * cat list is the attached customer's); a line the quote leaves out says why in words.
 */
/**
 * The quantity box keeps what is being typed as text and commits a whole number >= 1 on blur or Enter, so a cashier
 * can clear the box to type a new number without it snapping back to 1 (and appending a digit).
 */
function QuantityInput({ name, quantity, onCommit }: { name: string; quantity: number; onCommit: (quantity: number) => void }) {
  const [text, setText] = useState(String(quantity));
  const [editing, setEditing] = useState(false);
  const shown = editing ? text : String(quantity);
  function commit() {
    setEditing(false);
    const next = Number.parseInt(text, 10);
    if (Number.isInteger(next) && next >= 1 && next !== quantity) onCommit(next);
  }
  return (
    <input
      type="number"
      min={1}
      inputMode="numeric"
      aria-label={`Quantity of ${name}`}
      value={shown}
      onFocus={() => {
        setText(String(quantity));
        setEditing(true);
      }}
      onChange={(e) => {
        setEditing(true);
        setText(e.target.value);
      }}
      onBlur={commit}
      onKeyDown={(e) => {
        if (e.key === 'Enter') {
          e.preventDefault();
          commit();
        }
      }}
      className="h-8 w-14 rounded-[var(--radius-sm)] border border-border-strong bg-surface px-2 text-center text-sm text-ink"
    />
  );
}

export function PosBasket({
  lines,
  quote,
  cats,
  hasCustomer,
  onQuantity,
  onRemove,
  onCat,
}: {
  lines: BasketLine[];
  quote: CounterQuote | undefined;
  cats: CatListItem[];
  hasCustomer: boolean;
  onQuantity: (skuId: string, quantity: number) => void;
  onRemove: (skuId: string) => void;
  onCat: (skuId: string, catId: string) => void;
}) {
  if (lines.length === 0) {
    return <p className="rounded-[var(--radius-md)] border border-dashed border-border p-6 text-center text-sm text-ink-muted">The basket is empty. Scan an item to start the sale.</p>;
  }
  return (
    <ul aria-label="Basket" className="m-0 flex list-none flex-col p-0">
      {lines.map((line) => {
        const name = `${line.productName} ${line.variantLabel}`.trim();
        const priced = quote?.lines.find((q) => q.skuId === line.skuId);
        const excluded = quote?.excludedLines.find((q) => q.skuId === line.skuId);
        return (
          <li key={line.skuId} className="flex flex-col gap-2 border-b border-border py-3 last:border-0">
            <div className="flex flex-wrap items-start justify-between gap-2">
              <div className="min-w-0 flex-1 basis-48">
                <p className="font-semibold text-ink [overflow-wrap:anywhere]">{name}</p>
                {line.batchLabel && <p className="text-xs text-ink-muted">{line.batchLabel}</p>}
                {excluded && <p className="text-sm font-semibold text-danger-ink">{describeExcludedReason(excluded.reasonCode)}</p>}
              </div>
              <div className="flex items-center gap-1">
                <Button variant="secondary" size="icon" aria-label={`Decrease quantity of ${name}`} disabled={line.quantity <= 1} onClick={() => onQuantity(line.skuId, line.quantity - 1)}>
                  <Minus size={14} aria-hidden="true" />
                </Button>
                <QuantityInput name={name} quantity={line.quantity} onCommit={(quantity) => onQuantity(line.skuId, quantity)} />
                <Button variant="secondary" size="icon" aria-label={`Increase quantity of ${name}`} onClick={() => onQuantity(line.skuId, line.quantity + 1)}>
                  <Plus size={14} aria-hidden="true" />
                </Button>
                <Button variant="ghost" size="icon" aria-label={`Remove ${name}`} onClick={() => onRemove(line.skuId)}>
                  <Trash2 size={14} aria-hidden="true" />
                </Button>
              </div>
              <p className="w-24 text-right font-semibold">{priced ? <Amount money={priced.lineTotal} /> : <span className="text-ink-muted">…</span>}</p>
            </div>
            {line.requiresPrescription && (
              <div className="rounded-[var(--radius-sm)] bg-surface-sunken p-2 text-sm">
                {hasCustomer ? (
                  <>
                    <label htmlFor={`pos-cat-${line.skuId}`} className="font-semibold">Which cat is {line.productName} for?</label>
                    <select
                      id={`pos-cat-${line.skuId}`}
                      value={line.catId ?? ''}
                      onChange={(e) => onCat(line.skuId, e.target.value)}
                      className="mt-1 w-full rounded-[var(--radius-sm)] border border-border-strong bg-surface px-2 py-1.5 text-sm"
                    >
                      <option value="">Choose a cat</option>
                      {cats.map((cat) => (
                        <option key={cat.id} value={cat.id}>{cat.name}</option>
                      ))}
                    </select>
                  </>
                ) : (
                  <p>Prescription food. Attach an existing customer and choose their cat before the sale.</p>
                )}
              </div>
            )}
          </li>
        );
      })}
    </ul>
  );
}
