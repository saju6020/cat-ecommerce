'use client';

import { Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import type { ReceiptDraftLine } from '@/lib/drafts/receipt-draft';
import type { StockLocation } from '@/lib/api/inventory-lookup';
import { CheckboxField, SelectField, TextAreaField, TextField } from './fields';
import { LocationSelect } from './LocationSelect';
import { MoneyInput, QuantityInput } from './QuantityInput';
import { REJECTION_REASONS } from './labels';

export const RECEIPT_ID_PREFIX = 'receipt';

/**
 * `components/inventory/ReceiptLineCard.tsx` — HO-IMP-002 §11.3, US-IMP-04/16. One line of the
 * goods-receipt form: lot, dates, accepted and rejected quantities in the item's unit, a rejection
 * reason (shown, and required, only when rejected > 0), location, cost, hold-for-inspection, and the
 * item's storage requirement beside the heading (US-IMP-16 AC1). The expiry field is marked required
 * from the SKU's `expiryRequired`. One column on a phone; nothing scrolls sideways.
 */
export function ReceiptLineCard({
  line,
  index,
  errors,
  locations,
  onChange,
  onRemove,
}: {
  line: ReceiptDraftLine;
  index: number;
  errors: Record<string, string>;
  locations: StockLocation[] | undefined;
  onChange: (patch: Partial<ReceiptDraftLine>) => void;
  onRemove: () => void;
}) {
  const id = (field: string) => `${RECEIPT_ID_PREFIX}-line-${index}-${field}`;
  const rejecting = line.quantityRejected.trim() !== '' && !/^0*(\.0*)?$/.test(line.quantityRejected.trim());

  return (
    <li className="flex flex-col gap-3 rounded-[var(--radius-md)] border border-border bg-surface p-4" aria-label={`Line ${index + 1}: ${line.skuLabel}`}>
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="min-w-0">
          <p className="break-words text-sm font-semibold text-ink">
            {index + 1} · {line.skuLabel}
          </p>
          {line.storageLabel && (
            <p className="mt-1 inline-flex max-w-full items-center rounded-[var(--radius-sm)] bg-accent-soft px-2 py-0.5 text-xs font-semibold text-accent-ink">
              Store: {line.storageLabel}
            </p>
          )}
        </div>
        <Button variant="ghost" size="sm" onClick={onRemove} aria-label={`Remove line ${index + 1}, ${line.skuLabel}`}>
          <Trash2 size={14} aria-hidden="true" />
          Remove
        </Button>
      </div>

      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        <TextField id={id('lotNumber')} label="Lot number" required value={line.lotNumber} maxLength={60} error={errors.lotNumber} onChange={(e) => onChange({ lotNumber: e.target.value })} />
        <TextField id={id('manufacturedOn')} label="Manufactured" type="date" value={line.manufacturedOn} error={errors.manufacturedOn} onChange={(e) => onChange({ manufacturedOn: e.target.value })} />
        <TextField
          id={id('expiresOn')}
          label="Expiry date"
          type="date"
          required={line.expiryRequired}
          hint={line.expiryRequired ? 'Food and medicine must be received with an expiry date.' : 'Optional for this item.'}
          value={line.expiresOn}
          error={errors.expiresOn}
          onChange={(e) => onChange({ expiresOn: e.target.value })}
        />
        <QuantityInput id={id('quantityAccepted')} label="Accepted" required value={line.quantityAccepted} stockUnit={line.stockUnit} allowsFractional={line.allowsFractional} error={errors.quantityAccepted} onValueChange={(v) => onChange({ quantityAccepted: v })} />
        <QuantityInput id={id('quantityRejected')} label="Rejected" value={line.quantityRejected} stockUnit={line.stockUnit} allowsFractional={line.allowsFractional} error={errors.quantityRejected} onValueChange={(v) => onChange({ quantityRejected: v })} />
        <LocationSelect id={id('locationId')} label="Location" required locations={locations} movement="Receipt" value={line.locationId} error={errors.locationId} onChange={(v) => onChange({ locationId: v })} />
      </div>

      {rejecting && (
        <div className="grid gap-3 sm:grid-cols-2">
          <SelectField id={id('rejectionReason')} label="Why was it rejected?" required value={line.rejectionReason} error={errors.rejectionReason} onChange={(e) => onChange({ rejectionReason: e.target.value })}>
            <option value="">Choose a reason</option>
            {REJECTION_REASONS.map((r) => (<option key={r.value} value={r.value}>{r.label}</option>))}
          </SelectField>
          <TextAreaField id={id('rejectionNote')} label="Note" value={line.rejectionNote} error={errors.rejectionNote} onChange={(e) => onChange({ rejectionNote: e.target.value })} />
        </div>
      )}

      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        {line.costFromPurchaseOrder ? (
          <MoneyInput id={id('unitCostAmount')} label="Unit cost" decimals={4} perUnit={line.stockUnit} currencyCode={line.unitCostCurrency} value={line.unitCostAmount} readOnly onValueChange={() => undefined} hint="From the purchase order." />
        ) : (
          <>
            <MoneyInput id={id('unitCostAmount')} label="Unit cost" decimals={4} perUnit={line.stockUnit} currencyCode={line.unitCostCurrency || '—'} value={line.unitCostAmount} required error={errors.unitCostAmount} onValueChange={(v) => onChange({ unitCostAmount: v })} />
            <TextField id={id('unitCostCurrency')} label="Cost currency" maxLength={3} value={line.unitCostCurrency} error={errors.unitCostCurrency} onChange={(e) => onChange({ unitCostCurrency: e.target.value.toUpperCase() })} />
            <TextField id={id('exchangeRateToBase')} label="Exchange rate to base" inputMode="decimal" hint="Only if the currency differs from the base currency and no consignment gives a rate." value={line.exchangeRateToBase} error={errors.exchangeRateToBase} onChange={(e) => onChange({ exchangeRateToBase: e.target.value })} />
          </>
        )}
      </div>
      <CheckboxField id={id('holdForInspection')} label="Hold for inspection" hint="The batch is held (quarantined) until someone releases it." checked={line.holdForInspection} onChange={(e) => onChange({ holdForInspection: e.target.checked })} />
      {errors.skuId && <p role="alert" className="text-xs font-semibold text-danger-ink">{errors.skuId}</p>}
      {errors.purchaseOrderLineId && <p role="alert" className="text-xs font-semibold text-danger-ink">{errors.purchaseOrderLineId}</p>}
    </li>
  );
}
