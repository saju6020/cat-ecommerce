'use client';

import * as React from 'react';
import { Button } from '@/components/ui/button';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { Banner, SelectField, TextAreaField, TextField } from '@/components/inventory/fields';
import { LocationSelect } from '@/components/inventory/LocationSelect';
import { SkuPicker } from '@/components/inventory/SkuPicker';
import {
  EMPTY_ERRORS,
  distributeViolations,
  focusErrorTarget,
  newRowKey,
  type DistributedErrors,
} from '@/components/inventory/lineErrors';
import { quantityProblem, QUANTITY_PATTERN, UNIT_COST_PATTERN } from '@/lib/validation/inventory';
import { ApiValidationError } from '@/lib/api/client';
import { describeUnexpectedError } from '@/lib/api/errors';
import type { SkuLookup, StockLocation } from '@/lib/api/inventory-lookup';
import type { PurchaseOrderRequest } from '@/lib/api/procurement-orders';
import type { SupplierSummary } from '@/lib/api/procurement-suppliers';
import { PurchaseOrderLineEditor, type PoLineState } from './PurchaseOrderLineEditor';

export interface PurchaseOrderDraftState {
  supplierId: string;
  deliverToLocationId: string;
  currencyCode: string;
  expectedDeliveryOn: string;
  notes: string;
  lines: PoLineState[];
}

const TOP_FIELDS = ['supplierId', 'deliverToLocationId', 'currencyCode', 'expectedDeliveryOn', 'notes'] as const;

export function lineFromSku(sku: SkuLookup, quantity = '', unitCost = ''): PoLineState {
  return {
    key: newRowKey(),
    skuId: sku.skuId,
    skuCode: sku.skuCode,
    displayName: sku.displayName,
    stockUnit: sku.stockUnit,
    allowsFractional: sku.allowsFractionalQuantity,
    quantity,
    unitCost,
  };
}

/**
 * `components/procurement/PurchaseOrderBuilder.tsx` — HO-IMP-002 §11.2, US-IMP-02. Supplier,
 * deliver-to location, currency (defaulting from the supplier), expected delivery, and lines (find
 * an item → quantity in its unit → unit cost). It SAVES A DRAFT; issuing is a separate, confirmed
 * action on the order itself. A `400` lands on the field and the line it names; nothing typed is
 * lost. Pre-filled from `?supplierId=&lines=CODE:qty,…` (the reorder hand-off, OQ-2).
 */
export function PurchaseOrderBuilder({
  initial,
  suppliers,
  locations,
  today,
  onSubmit,
  isPending,
  submitLabel,
}: {
  initial: PurchaseOrderDraftState;
  suppliers: SupplierSummary[];
  locations: StockLocation[] | undefined;
  today: string;
  onSubmit: (request: PurchaseOrderRequest) => Promise<void>;
  isPending: boolean;
  submitLabel: string;
}) {
  const [state, setState] = React.useState<PurchaseOrderDraftState>(initial);
  const [errors, setErrors] = React.useState<DistributedErrors>(EMPTY_ERRORS);
  const [dirty, setDirty] = React.useState(false);
  const [saved, setSaved] = React.useState(false);
  const [submitting, setSubmitting] = React.useState(false);

  function patch(next: Partial<PurchaseOrderDraftState>) {
    setState((current) => ({ ...current, ...next }));
    setDirty(true);
  }

  function patchLine(key: string, change: Partial<PoLineState>) {
    patch({ lines: state.lines.map((l) => (l.key === key ? { ...l, ...change } : l)) });
  }

  function chooseSupplier(supplierId: string) {
    const supplier = suppliers.find((s) => s.id === supplierId);
    patch({ supplierId, currencyCode: supplier?.currencyCode ?? state.currencyCode });
  }

  function validate(): DistributedErrors {
    const found: DistributedErrors = { fields: {}, lines: {}, general: [], firstTarget: null };
    const flag = (target: string) => (found.firstTarget ??= target);
    if (!state.supplierId) { found.fields.supplierId = 'Choose a supplier.'; flag('supplierId'); }
    if (!state.deliverToLocationId) { found.fields.deliverToLocationId = 'Choose where it will be delivered.'; flag('deliverToLocationId'); }
    if (!state.expectedDeliveryOn) { found.fields.expectedDeliveryOn = 'Choose the expected delivery date.'; flag('expectedDeliveryOn'); }
    else if (state.expectedDeliveryOn < today) { found.fields.expectedDeliveryOn = 'The expected delivery date cannot be in the past.'; flag('expectedDeliveryOn'); }
    if (state.lines.length === 0) found.general.push('Add at least one line.');
    state.lines.forEach((line, index) => {
      const lineErrors: Record<string, string> = {};
      const problem = quantityProblem(line.quantity, { stockUnit: line.stockUnit, allowsFractional: line.allowsFractional });
      if (line.quantity.trim() === '') lineErrors.orderedQuantity = `Enter the quantity in ${line.stockUnit}.`;
      else if (problem || !QUANTITY_PATTERN.test(line.quantity.trim())) lineErrors.orderedQuantity = problem ?? 'Enter a valid quantity.';
      if (!UNIT_COST_PATTERN.test(line.unitCost.trim())) lineErrors.unitCostAmount = 'Enter the unit cost with at most 4 decimal places.';
      if (Object.keys(lineErrors).length > 0) {
        found.lines[index] = lineErrors;
        flag(`line-${index}-${lineErrors.orderedQuantity ? 'orderedQuantity' : 'unitCostAmount'}`);
      }
    });
    return found;
  }

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    setSaved(false);
    const local = validate();
    if (local.firstTarget || local.general.length > 0) {
      setErrors(local);
      focusErrorTarget('po', local.firstTarget);
      return;
    }
    setErrors(EMPTY_ERRORS);
    setSubmitting(true);
    try {
      await onSubmit({
        supplierId: state.supplierId,
        deliverToLocationId: state.deliverToLocationId,
        currencyCode: state.currencyCode.trim() === '' ? null : state.currencyCode.trim().toUpperCase(),
        expectedDeliveryOn: state.expectedDeliveryOn,
        notes: state.notes.trim() === '' ? null : state.notes.trim(),
        lines: state.lines.map((l) => ({ skuId: l.skuId, orderedQuantity: l.quantity.trim(), unitCostAmount: l.unitCost.trim() })),
      });
      setDirty(false);
      setSaved(true);
    } catch (error) {
      setSubmitting(false);
      const distributed =
        error instanceof ApiValidationError
          ? distributeViolations(error, { knownFields: TOP_FIELDS })
          : { ...EMPTY_ERRORS, general: [describeUnexpectedError(error)] };
      setErrors(distributed);
      focusErrorTarget('po', distributed.firstTarget);
    }
  }

  return (
    <form onSubmit={submit} noValidate className="flex max-w-4xl flex-col gap-5">
      <UnsavedChangesGuard when={dirty && !saved && !submitting} />
      {errors.general.length > 0 && (
        <Banner tone="danger">
          <ul className="list-disc pl-5">{errors.general.map((m) => <li key={m}>{m}</li>)}</ul>
        </Banner>
      )}
      <div className="grid gap-4 sm:grid-cols-2">
        <SelectField
          id="po-supplierId"
          label="Supplier"
          required
          value={state.supplierId}
          onChange={(event) => chooseSupplier(event.target.value)}
          error={errors.fields.supplierId}
        >
          <option value="">Choose a supplier</option>
          {suppliers.map((s) => (
            <option key={s.id} value={s.id}>
              {s.name} ({s.currencyCode})
            </option>
          ))}
        </SelectField>
        <LocationSelect
          id="po-deliverToLocationId"
          label="Deliver to"
          required
          locations={locations}
          movement="Receipt"
          value={state.deliverToLocationId}
          onChange={(value) => patch({ deliverToLocationId: value })}
          error={errors.fields.deliverToLocationId}
        />
        <TextField
          id="po-expectedDeliveryOn"
          label="Expected delivery"
          type="date"
          required
          min={today}
          value={state.expectedDeliveryOn}
          onChange={(event) => patch({ expectedDeliveryOn: event.target.value })}
          error={errors.fields.expectedDeliveryOn}
        />
        <TextField
          id="po-currencyCode"
          label="Currency"
          maxLength={3}
          value={state.currencyCode}
          hint="Defaults to the supplier's currency."
          onChange={(event) => patch({ currencyCode: event.target.value.toUpperCase() })}
          error={errors.fields.currencyCode}
        />
      </div>
      <TextAreaField
        id="po-notes"
        label="Notes"
        value={state.notes}
        onChange={(event) => patch({ notes: event.target.value })}
        error={errors.fields.notes}
      />
      <section aria-labelledby="po-lines-heading" className="flex flex-col gap-3">
        <h2 id="po-lines-heading" className="text-sm font-bold uppercase tracking-wide text-ink-muted">
          Lines
        </h2>
        <SkuPicker
          id="po-sku-picker"
          label="Add an item"
          onSelect={(sku) => {
            if (state.lines.some((l) => l.skuId === sku.skuId)) return;
            patch({ lines: [...state.lines, lineFromSku(sku)] });
          }}
        />
        <PurchaseOrderLineEditor
          lines={state.lines}
          currencyCode={state.currencyCode}
          errors={errors.lines}
          onChange={patchLine}
          onRemove={(key) => patch({ lines: state.lines.filter((l) => l.key !== key) })}
        />
        <p className="text-xs text-ink-muted">
          Line totals and the order total are worked out by the server when you save.
        </p>
      </section>
      {saved && <Banner tone="info">Saved.</Banner>}
      <div>
        <Button type="submit" disabled={isPending} aria-busy={isPending}>
          {isPending ? 'Saving…' : submitLabel}
        </Button>
      </div>
    </form>
  );
}
