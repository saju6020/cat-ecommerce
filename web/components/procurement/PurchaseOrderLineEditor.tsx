'use client';

import { Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { MoneyInput, QuantityInput } from '@/components/inventory/QuantityInput';

export interface PoLineState {
  key: string;
  skuId: string;
  skuCode: string;
  displayName: string;
  stockUnit: string;
  allowsFractional: boolean;
  quantity: string;
  unitCost: string;
}

/**
 * `components/procurement/PurchaseOrderLineEditor.tsx` — HO-IMP-002 §11.2. The purchase-order
 * builder's lines: each is a card (one column on a phone, three across from `sm` up) with the
 * quantity in the SKU's own unit and the unit cost in the order's currency. NO line total is shown
 * while drafting — totals are the server's, shown once the order is saved (ADR-FE-040/041).
 */
export function PurchaseOrderLineEditor({
  lines,
  currencyCode,
  errors,
  onChange,
  onRemove,
}: {
  lines: PoLineState[];
  currencyCode: string;
  errors: Record<number, Record<string, string>>;
  onChange: (key: string, patch: Partial<PoLineState>) => void;
  onRemove: (key: string) => void;
}) {
  if (lines.length === 0) {
    return (
      <p className="rounded-[var(--radius-md)] border border-dashed border-border-strong p-4 text-sm text-ink-muted">
        No lines yet. Find an item above to add the first one.
      </p>
    );
  }
  return (
    <ol className="flex flex-col gap-3">
      {lines.map((line, index) => {
        const lineErrors = errors[index] ?? {};
        return (
          <li
            key={line.key}
            className="rounded-[var(--radius-md)] border border-border bg-surface p-3"
            aria-label={`Line ${index + 1}: ${line.displayName}`}
          >
            <div className="mb-2 flex items-start justify-between gap-2">
              <p className="min-w-0 text-sm font-semibold text-ink">
                {index + 1} · {line.displayName}{' '}
                <span className="font-normal text-ink-muted">— {line.skuCode}</span>
              </p>
              <Button
                variant="ghost"
                size="sm"
                onClick={() => onRemove(line.key)}
                aria-label={`Remove line ${index + 1}, ${line.displayName}`}
              >
                <Trash2 size={14} aria-hidden="true" />
                Remove
              </Button>
            </div>
            {lineErrors.skuId && (
              <p role="alert" className="mb-2 text-xs font-semibold text-danger-ink">
                {lineErrors.skuId}
              </p>
            )}
            <div className="grid gap-3 sm:grid-cols-2">
              <QuantityInput
                id={`po-line-${index}-orderedQuantity`}
                label="Quantity"
                value={line.quantity}
                onValueChange={(value) => onChange(line.key, { quantity: value })}
                stockUnit={line.stockUnit}
                allowsFractional={line.allowsFractional}
                error={lineErrors.orderedQuantity}
                required
              />
              <MoneyInput
                id={`po-line-${index}-unitCostAmount`}
                label="Unit cost"
                value={line.unitCost}
                onValueChange={(value) => onChange(line.key, { unitCost: value })}
                currencyCode={currencyCode || '—'}
                decimals={4}
                perUnit={line.stockUnit}
                error={lineErrors.unitCostAmount}
                required
              />
            </div>
          </li>
        );
      })}
    </ol>
  );
}
