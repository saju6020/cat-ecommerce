'use client';

import { Check, X } from 'lucide-react';
import { cn } from '@/lib/utils';
import { formatMoney } from '@/lib/format/money';
import { availabilityText } from '@/lib/shop/labels';
import type { DetailSku } from '@/lib/api/shop-catalogue';

export interface VariantSelectorProps {
  skus: DetailSku[];
  selectedSkuId: string;
  onSelect: (skuId: string) => void;
}

/**
 * HO-SHP-002 §11.3 `VariantSelector` — a radio group of the active SKUs by `label` (UI-6), drawn as
 * rounded option cards (the selected one has a 2px teal border); each shows its price and its
 * availability in TEXT. An out-of-stock variant is still selectable and never hides the others
 * (US-SHP-02 AC2). Arrow keys move and select, as native radios do.
 */
export function VariantSelector({ skus, selectedSkuId, onSelect }: VariantSelectorProps) {
  if (skus.length <= 1) return null;
  return (
    <fieldset className="m-0 min-w-0 border-0 p-0">
      <legend className="mb-2.5 p-0 text-sm font-semibold text-ink">Choose a size</legend>
      <div className="flex flex-wrap gap-2.5">
        {skus.map((sku) => {
          const selected = sku.skuId === selectedSkuId;
          const inStock = sku.availability === 'InStock';
          return (
            <label
              key={sku.skuId}
              className={cn(
                'flex min-h-11 min-w-[150px] flex-1 cursor-pointer flex-col gap-1 rounded-[18px] bg-surface px-4 py-3 has-[:focus-visible]:outline-2 has-[:focus-visible]:outline-offset-2 has-[:focus-visible]:outline-accent',
                selected ? 'border-2 border-accent px-[15px] py-[11px]' : 'border border-border',
              )}
            >
              <span className="flex items-center gap-2">
                <input
                  type="radio"
                  name="sku"
                  value={sku.skuId}
                  checked={selected}
                  onChange={() => onSelect(sku.skuId)}
                  className="m-0 size-[17px] accent-[var(--accent)]"
                />
                <strong className="text-[15px] text-ink">{sku.label}</strong>
              </span>
              {sku.price && (
                <span className="text-[13px] text-ink-muted">{formatMoney(sku.price)}</span>
              )}
              <span
                className={cn(
                  'inline-flex items-center gap-1.5 text-[13px]',
                  inStock ? 'text-accent-ink' : 'text-danger-ink',
                )}
              >
                {inStock ? (
                  <Check size={13} aria-hidden="true" />
                ) : (
                  <X size={13} aria-hidden="true" />
                )}
                {availabilityText(sku.availability)}
              </span>
            </label>
          );
        })}
      </div>
    </fieldset>
  );
}
