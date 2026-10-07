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
 * HO-SHP-002 §11.3 `VariantSelector` — a radio group of the active SKUs by `label` (UI-6); each shows
 * its price and its availability in TEXT. An out-of-stock variant is still selectable and never hides
 * the others (US-SHP-02 AC2). Arrow keys move and select, as native radios do.
 */
export function VariantSelector({ skus, selectedSkuId, onSelect }: VariantSelectorProps) {
  if (skus.length <= 1) return null;
  return (
    <fieldset className="m-0 min-w-0 border-0 p-0">
      <legend className="mb-2 p-0 text-sm font-bold text-ink">Choose a size</legend>
      <div className="flex flex-col gap-2 sm:flex-row sm:flex-wrap">
        {skus.map((sku) => {
          const selected = sku.skuId === selectedSkuId;
          const inStock = sku.availability === 'InStock';
          return (
            <label
              key={sku.skuId}
              className={cn(
                'flex min-h-11 cursor-pointer items-center gap-2 rounded-[var(--radius-sm)] border bg-surface px-3 py-2 text-sm',
                selected ? 'border-accent ring-1 ring-accent' : 'border-border-strong',
              )}
            >
              <input
                type="radio"
                name="sku"
                value={sku.skuId}
                checked={selected}
                onChange={() => onSelect(sku.skuId)}
                className="h-4 w-4 accent-[var(--accent)]"
              />
              <span className="font-semibold text-ink">{sku.label}</span>
              {sku.price && <span className="text-ink-muted">{formatMoney(sku.price)}</span>}
              <span className="inline-flex items-center gap-1 text-xs text-ink-muted">
                {inStock ? (
                  <Check size={12} aria-hidden="true" />
                ) : (
                  <X size={12} aria-hidden="true" />
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
