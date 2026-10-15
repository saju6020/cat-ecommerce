'use client';

import * as React from 'react';
import { Button } from '@/components/ui/button';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { ScanField } from '@/components/common/ScanField';
import { ActionDialog } from './ReasonDialog';
import { Banner, SelectField, TextField } from './fields';
import { SkuPicker } from './SkuPicker';
import { ReceiptLineCard } from './ReceiptLineCard';
import { ReceiptErrorSummary } from './ReceiptErrorSummary';
import { EMPTY_ERRORS, distributeViolations, errorsForRow, focusErrorTarget, stampRowKeys, type DistributedErrors } from './lineErrors';
import {
  EMPTY_RECEIPT,
  defaultReceiptLocation,
  lineFromSku,
  toPostRequest,
  validateReceipt,
  type ReceiptFormState,
} from './receiptModel';
import { formatQuantity } from '@/lib/format/inventory';
import { formatDateOnly, timeOnlyInZone } from '@/lib/format/time';
import { ApiValidationError } from '@/lib/api/client';
import { describeUnexpectedError } from '@/lib/api/errors';
import {
  clearReceiptDraft,
  loadReceiptDraft,
  saveReceiptDraft,
  type ReceiptDraft,
  type ReceiptDraftLine,
} from '@/lib/drafts/receipt-draft';
import type { SkuLookup, StockLocation } from '@/lib/api/inventory-lookup';
import type { PostGoodsReceiptRequest } from '@/lib/api/inventory-receipts';
import type { ConsignmentSummary } from '@/lib/api/procurement-consignments';
import type { PurchaseOrderSummary } from '@/lib/api/procurement-orders';

const AUTOSAVE_MS = 400;
const TOP_FIELDS = ['purchaseOrderId', 'consignmentId', 'supplierId', 'deliveryReference'] as const;

export interface PurchaseOrderLinesFor {
  purchaseOrderId: string;
  lines: ReceiptDraftLine[];
}

/**
 * `components/inventory/GoodsReceiptForm.tsx` — HO-IMP-002 §11.3, US-IMP-04. The screen someone uses
 * standing at a delivery. Source (against a purchase order, a cleared consignment, or direct),
 * delivery reference, a scan field that adds a line per scan (HW-2) beside the SKU picker, and one
 * card per line. **Autosaves a draft to `sessionStorage`** (ADR-FE-043) and offers to restore it.
 * **Post** is confirmed — naming the lines, their quantities with units, where they go, and that a
 * posted receipt cannot be edited. A `400` puts EVERY violation on its own field and scrolls to the
 * first. No quantity is added up here: the confirmation lists each line's quantity with its unit.
 */
export function GoodsReceiptForm({
  staffAccountId,
  locations,
  purchaseOrders,
  consignments,
  purchaseOrderLines,
  initial,
  onPurchaseOrderChange,
  onScan,
  onPost,
  isPosting,
  timeZoneLabel,
}: {
  staffAccountId: string;
  locations: StockLocation[] | undefined;
  purchaseOrders: PurchaseOrderSummary[];
  consignments: ConsignmentSummary[];
  /** The selected order's lines, once loaded (null while there is none or it is loading). */
  purchaseOrderLines: PurchaseOrderLinesFor | null;
  initial?: Partial<ReceiptFormState>;
  onPurchaseOrderChange: (purchaseOrderId: string) => void;
  onScan: (barcode: string) => Promise<SkuLookup | null>;
  onPost: (request: PostGoodsReceiptRequest) => Promise<void>;
  isPosting: boolean;
  timeZoneLabel: string;
}) {
  const [state, setState] = React.useState<ReceiptFormState>({ ...EMPTY_RECEIPT, ...initial });
  const [errors, setErrors] = React.useState<DistributedErrors>(EMPTY_ERRORS);
  const [restorable, setRestorable] = React.useState<ReceiptDraft | null>(null);
  const [savedAt, setSavedAt] = React.useState<string | null>(null);
  const [confirming, setConfirming] = React.useState(false);
  const [discarding, setDiscarding] = React.useState(false);
  const [dirty, setDirty] = React.useState(false);
  const [submitting, setSubmitting] = React.useState(false);
  const appliedPoRef = React.useRef<string>('');
  const focusKeyRef = React.useRef<string | null>(null);
  const defaultLocation = defaultReceiptLocation(locations);

  // Offer the draft left by a reload or a lost tab (ADR-FE-043) — once, on arrival.
  React.useEffect(() => {
    const draft = loadReceiptDraft(staffAccountId);
    if (draft && draft.lines.length > 0) setRestorable(draft);
  }, [staffAccountId]);

  // Autosave on change (never while the restore prompt is still waiting for an answer).
  React.useEffect(() => {
    if (restorable || !dirty) return;
    const timer = setTimeout(() => {
      if (state.lines.length === 0) {
        clearReceiptDraft(staffAccountId);
        setSavedAt(null);
        return;
      }
      const ok = saveReceiptDraft(staffAccountId, { ...state, savedAt: new Date().toISOString() });
      if (ok) setSavedAt(new Date().toISOString());
    }, AUTOSAVE_MS);
    return () => clearTimeout(timer);
  }, [state, dirty, restorable, staffAccountId]);

  // Flush the pending autosave when the page is hidden or the form unmounts, so a change made inside
  // the debounce window is not lost to a tab close or an in-app navigation.
  const latestRef = React.useRef({ state, dirty, restorable });
  latestRef.current = { state, dirty, restorable };
  React.useEffect(() => {
    function flush() {
      const { state: current, dirty: isDirty, restorable: waiting } = latestRef.current;
      if (waiting || !isDirty || current.lines.length === 0) return;
      saveReceiptDraft(staffAccountId, { ...current, savedAt: new Date().toISOString() });
    }
    window.addEventListener('pagehide', flush);
    return () => {
      window.removeEventListener('pagehide', flush);
      flush();
    };
  }, [staffAccountId]);

  // A purchase order's lines arrive after it is chosen: pre-fill them once per order.
  React.useEffect(() => {
    if (!purchaseOrderLines || state.source !== 'purchase-order') return;
    if (purchaseOrderLines.purchaseOrderId !== state.purchaseOrderId) return;
    if (appliedPoRef.current === purchaseOrderLines.purchaseOrderId) return;
    appliedPoRef.current = purchaseOrderLines.purchaseOrderId;
    setState((current) => ({
      ...current,
      lines: [...purchaseOrderLines.lines, ...current.lines.filter((l) => l.purchaseOrderLineId === '')],
    }));
    setDirty(true);
  }, [purchaseOrderLines, state.source, state.purchaseOrderId]);

  // After a line is added, put the cursor in its first quantity field.
  React.useEffect(() => {
    if (!focusKeyRef.current) return;
    const index = state.lines.findIndex((l) => l.key === focusKeyRef.current);
    focusKeyRef.current = null;
    if (index >= 0) document.getElementById(`receipt-line-${index}-quantityAccepted`)?.focus();
  }, [state.lines]);

  function patch(next: Partial<ReceiptFormState>) {
    setState((current) => ({ ...current, ...next }));
    setDirty(true);
  }

  function patchLine(key: string, change: Partial<ReceiptDraftLine>) {
    setState((current) => ({ ...current, lines: current.lines.map((l) => (l.key === key ? { ...l, ...change } : l)) }));
    setDirty(true);
  }

  function addSku(sku: SkuLookup) {
    const line = lineFromSku(sku, defaultLocation);
    focusKeyRef.current = line.key;
    patch({ lines: [...state.lines, line] });
  }

  async function handleScan(barcode: string): Promise<string | null> {
    const sku = await onScan(barcode);
    if (!sku) return null;
    addSku(sku);
    return `${sku.displayName} — ${sku.skuCode}`;
  }

  function changeSource(source: ReceiptFormState['source']) {
    appliedPoRef.current = '';
    patch({ source, purchaseOrderId: '', consignmentId: '' });
  }

  function chooseOrder(id: string) {
    appliedPoRef.current = '';
    patch({ purchaseOrderId: id });
    if (id) onPurchaseOrderChange(id);
  }

  function restore(draft: ReceiptDraft) {
    appliedPoRef.current = draft.purchaseOrderId;
    // Lines scanned or added while the prompt was open are kept after the restored ones.
    const keptKeys = new Set(draft.lines.map((line) => line.key));
    const addedSince = state.lines.filter((line) => !keptKeys.has(line.key));
    setState({
      source: draft.source,
      purchaseOrderId: draft.purchaseOrderId,
      consignmentId: draft.consignmentId,
      deliveryReference: draft.deliveryReference,
      lines: [...draft.lines, ...addedSince],
    });
    setRestorable(null);
    setDirty(true);
  }

  function discardDraft() {
    clearReceiptDraft(staffAccountId);
    appliedPoRef.current = '';
    setState({ ...EMPTY_RECEIPT });
    setRestorable(null);
    setSavedAt(null);
    setDirty(false);
    setErrors(EMPTY_ERRORS);
  }

  function review(event: React.FormEvent) {
    event.preventDefault();
    const local = validateReceipt(state);
    setErrors(stampRowKeys(local, state.lines.map((l) => l.key)));
    if (local.firstTarget || local.general.length > 0) {
      focusErrorTarget('receipt', local.firstTarget);
      return;
    }
    setConfirming(true);
  }

  async function post() {
    setSubmitting(true);
    try {
      await onPost(toPostRequest(state));
      clearReceiptDraft(staffAccountId);
      setDirty(false);
    } catch (error) {
      setSubmitting(false);
      setConfirming(false);
      const distributed =
        error instanceof ApiValidationError
          ? distributeViolations(error, { knownFields: TOP_FIELDS })
          : { ...EMPTY_ERRORS, general: [describeUnexpectedError(error)] };
      setErrors(stampRowKeys(distributed, state.lines.map((l) => l.key)));
      // Wait a frame so the closing dialog has returned focus before we move it.
      setTimeout(() => focusErrorTarget('receipt', distributed.firstTarget), 0);
    }
  }

  const locationName = (id: string) => locations?.find((l) => l.id === id)?.name ?? 'the chosen location';
  const lineCount = state.lines.length;

  return (
    <form onSubmit={review} noValidate className="flex max-w-5xl flex-col gap-5">
      <UnsavedChangesGuard when={dirty && !submitting} />
      {restorable && (
        <Banner tone="info" role="status">
          <p className="font-semibold">Restore the receipt you were entering?</p>
          <p className="mt-1">
            A draft with {restorable.lines.length} {restorable.lines.length === 1 ? 'line' : 'lines'} was kept in this browser tab.
          </p>
          <div className="mt-2 flex flex-wrap gap-2">
            <Button size="sm" onClick={() => restore(restorable)}>Restore the draft</Button>
            <Button size="sm" variant="secondary" onClick={discardDraft}>Start a new receipt</Button>
          </div>
        </Banner>
      )}
      <ReceiptErrorSummary errors={errors} />

      <section aria-labelledby="receipt-source-heading" className="grid gap-4 rounded-[var(--radius-md)] border border-border bg-surface p-4 sm:grid-cols-2">
        <fieldset className="flex flex-col gap-2 sm:col-span-2">
          <legend id="receipt-source-heading" className="text-sm font-semibold text-ink">Receiving</legend>
          {(
            [
              ['purchase-order', 'Against a purchase order'],
              ['consignment', 'Against a consignment (cleared ones only)'],
              ['direct', 'Direct, with no order'],
            ] as const
          ).map(([value, label]) => (
            <label key={value} className="flex items-center gap-2 text-sm text-ink">
              <input
                type="radio"
                name="receipt-source"
                value={value}
                checked={state.source === value}
                onChange={() => changeSource(value)}
                className="h-4 w-4 accent-[var(--accent)]"
              />
              {label}
            </label>
          ))}
        </fieldset>
        {state.source === 'purchase-order' && (
          <SelectField
            id="receipt-purchaseOrderId"
            label="Purchase order"
            required
            value={state.purchaseOrderId}
            error={errors.fields.purchaseOrderId}
            hint="Lines are filled in with what the order still expects."
            onChange={(e) => chooseOrder(e.target.value)}
          >
            <option value="">Choose an issued order</option>
            {purchaseOrders.map((po) => (
              <option key={po.id} value={po.id}>
                {po.number} · {po.supplierName} · due {formatDateOnly(po.expectedDeliveryOn)}
              </option>
            ))}
          </SelectField>
        )}
        {state.source === 'consignment' && (
          <SelectField
            id="receipt-consignmentId"
            label="Consignment"
            required
            value={state.consignmentId}
            error={errors.fields.consignmentId}
            onChange={(e) => patch({ consignmentId: e.target.value })}
          >
            <option value="">Choose a cleared consignment</option>
            {consignments.map((c) => (
              <option key={c.id} value={c.id}>
                {c.reference} · shipment {c.shipmentReference}
              </option>
            ))}
          </SelectField>
        )}
        <TextField
          id="receipt-deliveryReference"
          label="Delivery reference"
          maxLength={60}
          value={state.deliveryReference}
          error={errors.fields.deliveryReference}
          onChange={(e) => patch({ deliveryReference: e.target.value })}
        />
        <p className="text-sm text-ink-muted sm:col-span-2">Received: now ({timeZoneLabel}) — the server records the time when you post.</p>
      </section>

      <section aria-labelledby="receipt-add-heading" className="flex flex-col gap-3">
        <h2 id="receipt-add-heading" className="text-sm font-bold uppercase tracking-wide text-ink-muted">Add lines</h2>
        <ScanField id="receipt-scan" onScan={handleScan} />
        <SkuPicker id="receipt-sku-picker" label="Or find an item" onSelect={addSku} />
      </section>

      <section aria-labelledby="receipt-lines-heading" className="flex flex-col gap-3">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h2 id="receipt-lines-heading" className="text-sm font-bold uppercase tracking-wide text-ink-muted">
            Lines ({lineCount})
          </h2>
          <p role="status" className="text-xs text-ink-muted">
            {savedAt ? `Draft saved ${timeOnlyInZone(savedAt, timeZoneLabel)} (${timeZoneLabel}) · kept in this browser tab only` : lineCount > 0 ? 'Saving a draft as you type…' : ''}
          </p>
        </div>
        {lineCount === 0 ? (
          <p className="rounded-[var(--radius-md)] border border-dashed border-border-strong p-4 text-sm text-ink-muted">
            No lines yet. Scan a barcode, find an item above, or choose a purchase order to fill them in.
          </p>
        ) : (
          <ol className="flex flex-col gap-3">
            {state.lines.map((line, index) => (
              <ReceiptLineCard
                key={line.key}
                line={line}
                index={index}
                errors={errorsForRow(errors, line.key, index)}
                locations={locations}
                onChange={(change) => patchLine(line.key, change)}
                onRemove={() => patch({ lines: state.lines.filter((l) => l.key !== line.key) })}
              />
            ))}
          </ol>
        )}
      </section>

      <div className="flex flex-wrap items-center gap-3 border-t border-border pt-4">
        <Button type="submit" disabled={isPosting || submitting || lineCount === 0}>
          Review and post
        </Button>
        <Button variant="secondary" disabled={lineCount === 0 && !savedAt} onClick={() => setDiscarding(true)}>
          Discard draft
        </Button>
        <p className="text-xs text-ink-muted">A posted receipt cannot be edited; corrections are adjustments.</p>
      </div>

      <ActionDialog
        open={confirming}
        onOpenChange={setConfirming}
        title="Post this receipt?"
        consequence={`${lineCount} ${lineCount === 1 ? 'line' : 'lines'} will be received. A posted receipt cannot be edited; corrections are adjustments.`}
        confirmLabel="Post receipt"
        pendingLabel="Posting…"
        onConfirm={post}
      >
        <ul className="mt-3 max-h-60 list-disc overflow-y-auto pl-5 text-sm text-ink">
          {state.lines.map((l) => (
            <li key={l.key}>
              {l.quantityAccepted.trim() === '' ? 'Nothing accepted' : formatQuantity(l.quantityAccepted, l.stockUnit)} of {l.skuLabel}
              {l.lotNumber ? `, lot ${l.lotNumber}` : ''} → {locationName(l.locationId)}
              {l.quantityRejected.trim() !== '' && l.quantityRejected.trim() !== '0' ? `; ${formatQuantity(l.quantityRejected, l.stockUnit)} rejected` : ''}
            </li>
          ))}
        </ul>
      </ActionDialog>
      <ActionDialog
        open={discarding}
        onOpenChange={setDiscarding}
        title="Discard this receipt?"
        consequence="The lines you have entered, and the draft kept in this browser tab, are thrown away. Nothing has been posted."
        confirmLabel="Discard draft"
        variant="destructive"
        onConfirm={async () => discardDraft()}
      />
    </form>
  );
}
