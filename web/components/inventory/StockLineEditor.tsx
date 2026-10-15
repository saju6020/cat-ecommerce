'use client';

import { Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { describeBatchState } from '@/components/stock/BatchStateBadge';
import { useSkuStockMatrix } from '@/lib/hooks/useInventory';
import { formatExpiry, formatQuantity } from '@/lib/format/inventory';
import { QuantityInput } from './QuantityInput';
import { SelectField } from './fields';

export interface StockLineState {
  key: string;
  skuId: string;
  skuLabel: string;
  stockUnit: string;
  allowsFractional: boolean;
  batchId: string;
  quantity: string;
}

/**
 * `components/inventory/StockLineEditor.tsx` — HO-IMP-002 §11.5. One line of a transfer or an
 * adjustment: the item, a BATCH picker showing each batch's on-hand AT THE CHOSEN LOCATION with its
 * expiry and state in words (US-IMP-13 AC1), and the quantity in the item's unit. `direction` states
 * what the line does (*"removes"* / *"adds"*) when the reason fixes it.
 */
export function StockLineEditor({
  line,
  index,
  idPrefix,
  locationId,
  direction,
  errors,
  onChange,
  onRemove,
}: {
  line: StockLineState;
  index: number;
  idPrefix: string;
  locationId: string;
  direction?: 'removes' | 'adds' | null;
  errors: Record<string, string>;
  onChange: (patch: Partial<StockLineState>) => void;
  onRemove: () => void;
}) {
  const matrix = useSkuStockMatrix(line.skuId);
  const batches = (matrix.data?.batches ?? []).flatMap((batch) => {
    const cell = batch.locations.find((c) => c.locationId === locationId);
    return cell ? [{ batch, cell }] : [];
  });

  return (
    <li className="flex flex-col gap-3 rounded-[var(--radius-md)] border border-border bg-surface p-3" aria-label={`Line ${index + 1}: ${line.skuLabel}`}>
      <div className="flex flex-wrap items-start justify-between gap-2">
        <p className="min-w-0 break-words text-sm font-semibold text-ink">
          {index + 1} · {line.skuLabel}
          {direction && <span className="ml-2 font-normal text-ink-muted">({direction} stock)</span>}
        </p>
        <Button variant="ghost" size="sm" onClick={onRemove} aria-label={`Remove line ${index + 1}, ${line.skuLabel}`}>
          <Trash2 size={14} aria-hidden="true" />
          Remove
        </Button>
      </div>
      <div className="grid gap-3 sm:grid-cols-2">
        <SelectField
          id={`${idPrefix}-line-${index}-batchId`}
          label="Batch"
          required
          value={line.batchId}
          error={errors.batchId}
          hint={!locationId ? 'Choose the location first.' : matrix.isLoading ? 'Loading batches…' : batches.length === 0 ? 'No batch of this item is held at that location.' : undefined}
          onChange={(e) => onChange({ batchId: e.target.value })}
        >
          <option value="">Choose a batch</option>
          {batches.map(({ batch, cell }) => {
            const state = describeBatchState({ status: batch.status, isExpired: batch.isExpired });
            return (
              <option key={batch.batchId} value={batch.batchId}>
                Lot {batch.lotNumber} — {formatQuantity(cell.onHand, line.stockUnit)} at {cell.locationCode} · {formatExpiry(batch.expiresOn, batch.isExpired, batch.daysToExpiry).text}
                {state.usable ? '' : ` · ${state.label}`}
              </option>
            );
          })}
        </SelectField>
        <QuantityInput
          id={`${idPrefix}-line-${index}-quantity`}
          label="Quantity"
          required
          value={line.quantity}
          stockUnit={line.stockUnit}
          allowsFractional={line.allowsFractional}
          error={errors.quantity}
          onValueChange={(v) => onChange({ quantity: v })}
        />
      </div>
    </li>
  );
}
