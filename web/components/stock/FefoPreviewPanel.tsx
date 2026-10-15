'use client';

import * as React from 'react';
import { Button } from '@/components/ui/button';
import { Banner, Panel, SelectField } from '@/components/inventory/fields';
import { QuantityInput } from '@/components/inventory/QuantityInput';
import { StackedTable } from '@/components/inventory/StackedTable';
import { describeInventoryError } from '@/lib/api/errors';
import { formatDateOrDash, formatQuantity } from '@/lib/format/inventory';
import { quantityProblem } from '@/lib/validation/inventory';
import type { StockLocation } from '@/lib/api/inventory-lookup';
import type { FefoPreview, FefoPreviewLine, FefoPurpose } from '@/lib/api/inventory-stock';

export const FEFO_PURPOSES: ReadonlyArray<{ value: FefoPurpose; label: string }> = [
  { value: 'Sale', label: 'A sale' },
  { value: 'Dispense', label: 'Dispensing medicine' },
  { value: 'ClinicalUse', label: 'Clinical use' },
  { value: 'BoardingUse', label: 'Boarding use' },
];

export interface FefoRequest {
  quantity: string;
  purpose: FefoPurpose;
  locationId?: string;
}

/**
 * `components/stock/FefoPreviewPanel.tsx` — HO-IMP-002 §11.4, US-IMP-07 AC1. Quantity, purpose and an
 * optional location → the SERVER's plan, listed in the order it returned (never re-sorted — that
 * order IS first-expiry-first-out, ADR-FE-040), each with lot, expiry, location and quantity. It
 * says *"This is what would be picked next."* A shortfall states how much is missing. A refusal
 * (a location that does not take that kind of movement) is the server's own sentence.
 */
export function FefoPreviewPanel({
  stockUnit,
  allowsFractional,
  locations,
  preview,
  isLoading,
  error,
  onPreview,
}: {
  stockUnit: string;
  allowsFractional: boolean;
  locations: StockLocation[] | undefined;
  preview: FefoPreview | undefined;
  isLoading: boolean;
  error: unknown;
  onPreview: (request: FefoRequest) => void;
}) {
  const [quantity, setQuantity] = React.useState('1');
  const [purpose, setPurpose] = React.useState<FefoPurpose>('Sale');
  const [locationId, setLocationId] = React.useState('');
  const [localError, setLocalError] = React.useState<string | undefined>();

  function submit(event: React.FormEvent) {
    event.preventDefault();
    const problem = quantityProblem(quantity, { stockUnit, allowsFractional }) ?? (quantity.trim() === '' ? `Enter how many ${stockUnit}.` : undefined);
    setLocalError(problem);
    if (problem) return;
    onPreview({ quantity: quantity.trim(), purpose, locationId: locationId || undefined });
  }

  return (
    <Panel title="What would be picked next?">
      <form onSubmit={submit} noValidate className="grid items-end gap-3 sm:grid-cols-[10rem_12rem_1fr_auto]">
        <QuantityInput id="fefo-quantity" label="Quantity" value={quantity} stockUnit={stockUnit} allowsFractional={allowsFractional} error={localError} onValueChange={setQuantity} />
        <SelectField id="fefo-purpose" label="For" value={purpose} onChange={(e) => setPurpose(e.target.value as FefoPurpose)}>
          {FEFO_PURPOSES.map((p) => (<option key={p.value} value={p.value}>{p.label}</option>))}
        </SelectField>
        <SelectField id="fefo-location" label="From (optional)" value={locationId} onChange={(e) => setLocationId(e.target.value)}>
          <option value="">Any location</option>
          {(locations ?? []).map((l) => (<option key={l.id} value={l.id}>{l.name}</option>))}
        </SelectField>
        <Button type="submit" disabled={isLoading}>{isLoading ? 'Working…' : 'Preview'}</Button>
      </form>
      <div className="mt-4" aria-live="polite">
        {error ? <Banner tone="danger">{describeInventoryError(error)}</Banner> : null}
        {!error && preview && (
          <div className="flex flex-col gap-2">
            <p className="text-sm font-semibold text-ink">This is what would be picked next.</p>
            {preview.lines.length === 0 ? (
              <p className="text-sm text-ink-muted">Nothing is available to pick.</p>
            ) : (
              <StackedTable
                caption="Pick order, first-expiry-first-out"
                rows={preview.lines}
                getKey={(l, i) => `${l.batchId}-${l.locationId}-${i}`}
                columns={[
                  { id: 'order', header: 'Pick', rowHeader: true, cell: (_l: FefoPreviewLine, i: number) => `${i + 1}` },
                  { id: 'lot', header: 'Lot', cell: (l) => l.lotNumber },
                  { id: 'expiry', header: 'Expiry', cell: (l) => formatDateOrDash(l.expiresOn, 'No expiry date') },
                  { id: 'loc', header: 'Location', cell: (l) => l.locationCode },
                  { id: 'qty', header: 'Quantity', numeric: true, cell: (l) => formatQuantity(l.quantity, preview.stockUnit) },
                ]}
              />
            )}
            {preview.shortfall !== '0' && (
              <Banner tone="warning">
                Short by {formatQuantity(preview.shortfall, preview.stockUnit)} — there isn&rsquo;t enough usable stock for the whole quantity.
              </Banner>
            )}
          </div>
        )}
      </div>
    </Panel>
  );
}
