'use client';

import { useBatches } from '@/lib/hooks/useInventory';
import { formatQuantity } from '@/lib/format/units';
import { formatDateOnly } from '@/lib/format/time';
import type { BatchSummary } from '@/lib/api/inventory-stock';
import type { FormularyLinkedSku } from '@/lib/api/formulary';
import { errorClass, hintClass, labelClass } from '@/components/surgery/styles';

function SkuBatches({
  sku,
  value,
  onChange,
}: {
  sku: FormularyLinkedSku;
  value: string;
  onChange: (batch: BatchSummary, isFirst: boolean) => void;
}) {
  const batches = useBatches({ skuId: sku.skuId, status: 'Available', pageSize: 50 });
  // Usable batches only, soonest expiry first (FEFO) — the server's own status and expiry flags decide
  // usability; this list neither re-checks stock nor judges a date.
  const usable = (batches.data?.items ?? [])
    .filter((b) => b.status === 'Available' && !b.isExpired)
    .sort((a, b) => (a.expiresOn ?? '9999').localeCompare(b.expiresOn ?? '9999'));
  return (
    <fieldset className="flex flex-col gap-2">
      <legend className="text-sm text-ink-muted">
        {sku.productName} {sku.variantLabel}
      </legend>
      {batches.isLoading && <p className={hintClass}>Loading batches…</p>}
      {batches.isError && <p className={errorClass}>The batches could not be loaded.</p>}
      {!batches.isLoading && !batches.isError && usable.length === 0 && (
        <p className="text-sm font-semibold text-warning-ink">
          No usable batch is held at the clinic for this medicine.
        </p>
      )}
      {usable.map((batch, index) => (
        <label key={batch.id} className="flex items-start gap-2 text-sm text-ink">
          <input
            type="radio"
            name="drug-batch"
            className="mt-1"
            checked={value === batch.id}
            onChange={() => onChange(batch, index === 0)}
          />
          <span>
            Lot {batch.lotNumber}
            {batch.expiresOn && ` · expires ${formatDateOnly(batch.expiresOn)}`} ·{' '}
            {formatQuantity({ amount: batch.onHand, unit: batch.stockUnit })} on hand
          </span>
        </label>
      ))}
    </fieldset>
  );
}

/**
 * `DrugBatchChooser` — HO-SRG-002 §8.5 / §11.4. HO-IMP-002 built no `BatchPicker` component (only its
 * batch list API and `useBatches`, which this reads), so this is a thin radio list of the linked SKUs'
 * USABLE batches, FEFO first — recorded in HO-SRG-002 §1 Amended as a deviation from §20.2. Expired and
 * quarantined batches are not offered; a batch the server later refuses is shown as `BatchNotUsable`.
 */
export function DrugBatchChooser({
  skus,
  value,
  onChange,
  error,
}: {
  skus: FormularyLinkedSku[];
  value: string;
  onChange: (batch: BatchSummary, isFirst: boolean) => void;
  error?: string;
}) {
  return (
    <div className="flex flex-col gap-2" role="group" aria-labelledby="drug-batch-label">
      <p id="drug-batch-label" className={labelClass}>
        Batch
      </p>
      {skus.map((sku) => (
        <SkuBatches key={sku.skuId} sku={sku} value={value} onChange={onChange} />
      ))}
      {error && (
        <p role="alert" className={errorClass}>
          {error}
        </p>
      )}
    </div>
  );
}
