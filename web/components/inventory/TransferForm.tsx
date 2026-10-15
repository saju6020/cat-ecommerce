'use client';

import * as React from 'react';
import { Button } from '@/components/ui/button';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { ScanField } from '@/components/common/ScanField';
import { ActionDialog } from './ReasonDialog';
import { Banner, TextField } from './fields';
import { LocationSelect } from './LocationSelect';
import { ReceiptErrorSummary } from './ReceiptErrorSummary';
import { SkuPicker } from './SkuPicker';
import { StockLineEditor, type StockLineState } from './StockLineEditor';
import { EMPTY_ERRORS, distributeError, errorsForRow, focusErrorTarget, newRowKey, stampRowKeys, type DistributedErrors } from './lineErrors';
import { formatQuantity } from '@/lib/format/inventory';
import { quantityProblem } from '@/lib/validation/inventory';
import type { SkuLookup, StockLocation } from '@/lib/api/inventory-lookup';
import type { PostTransferRequest } from '@/lib/api/inventory-control';

const TOP_FIELDS = ['fromLocationId', 'toLocationId', 'reason'] as const;

export function stockLineFromSku(sku: SkuLookup): StockLineState {
  return {
    key: newRowKey(),
    skuId: sku.skuId,
    skuLabel: `${sku.displayName} — ${sku.skuCode}`,
    stockUnit: sku.stockUnit,
    allowsFractional: sku.allowsFractionalQuantity,
    batchId: '',
    quantity: '',
  };
}

/**
 * `components/inventory/TransferForm.tsx` — HO-IMP-002 §11.5, US-IMP-13 AC1. From and to locations
 * (the other end's location is disabled, with the reason in text), lines added by scan or picker,
 * each with a batch chosen from what is held at the SOURCE (with its expiry), a quantity in its unit
 * and an optional reason. The confirmation names both locations and every line. A transfer of stock
 * that is no longer free comes back as the server's own `409`, shown on the line.
 */
export function TransferForm({
  locations,
  onScan,
  onPost,
  isPending,
}: {
  locations: StockLocation[] | undefined;
  onScan: (barcode: string) => Promise<SkuLookup | null>;
  onPost: (request: PostTransferRequest) => Promise<void>;
  isPending: boolean;
}) {
  const [fromId, setFromId] = React.useState('');
  const [toId, setToId] = React.useState('');
  const [reason, setReason] = React.useState('');
  const [lines, setLines] = React.useState<StockLineState[]>([]);
  const [errors, setErrors] = React.useState<DistributedErrors>(EMPTY_ERRORS);
  const [confirming, setConfirming] = React.useState(false);
  const [dirty, setDirty] = React.useState(false);
  const [submitting, setSubmitting] = React.useState(false);

  const name = (id: string) => locations?.find((l) => l.id === id)?.name ?? '';
  const touch = () => setDirty(true);

  function addSku(sku: SkuLookup) {
    setLines((current) => [...current, stockLineFromSku(sku)]);
    touch();
  }

  function review(event: React.FormEvent) {
    event.preventDefault();
    const local: DistributedErrors = { fields: {}, lines: {}, general: [], firstTarget: null };
    if (!fromId) { local.fields.fromLocationId = 'Choose where the stock is coming from.'; local.firstTarget ??= 'fromLocationId'; }
    if (!toId) { local.fields.toLocationId = 'Choose where it is going.'; local.firstTarget ??= 'toLocationId'; }
    if (fromId && fromId === toId) { local.fields.toLocationId = 'Choose a different location from the source.'; local.firstTarget ??= 'toLocationId'; }
    if (lines.length === 0) local.general.push('Add at least one line.');
    lines.forEach((line, index) => {
      const lineErrors: Record<string, string> = {};
      if (!line.batchId) lineErrors.batchId = 'Choose which batch to move.';
      const problem = line.quantity.trim() === '' ? `Enter how many ${line.stockUnit}.` : quantityProblem(line.quantity, { stockUnit: line.stockUnit, allowsFractional: line.allowsFractional });
      if (problem) lineErrors.quantity = problem;
      if (Object.keys(lineErrors).length > 0) {
        local.lines[index] = lineErrors;
        local.firstTarget ??= `line-${index}-${lineErrors.batchId ? 'batchId' : 'quantity'}`;
      }
    });
    setErrors(stampRowKeys(local, lines.map((l) => l.key)));
    if (local.firstTarget || local.general.length > 0) {
      focusErrorTarget('transfer', local.firstTarget);
      return;
    }
    setConfirming(true);
  }

  async function post() {
    setSubmitting(true);
    try {
      await onPost({
        fromLocationId: fromId,
        toLocationId: toId,
        reason: reason.trim() === '' ? null : reason.trim(),
        lines: lines.map((l) => ({ skuId: l.skuId, batchId: l.batchId, quantity: l.quantity.trim() })),
      });
      setDirty(false);
    } catch (error) {
      setSubmitting(false);
      setConfirming(false);
      const distributed = distributeError(error, { knownFields: TOP_FIELDS });
      setErrors(stampRowKeys(distributed, lines.map((l) => l.key)));
      setTimeout(() => focusErrorTarget('transfer', distributed.firstTarget), 0);
    }
  }

  return (
    <form onSubmit={review} noValidate className="flex max-w-4xl flex-col gap-5">
      <UnsavedChangesGuard when={dirty && !submitting} />
      <ReceiptErrorSummary errors={errors} idPrefix="transfer" title="This transfer can't be posted yet." />
      <div className="grid gap-4 sm:grid-cols-2">
        <LocationSelect
          id="transfer-fromLocationId"
          label="From"
          required
          locations={locations}
          movement="TransferOut"
          value={fromId}
          disabledReasons={toId ? { [toId]: 'already the destination' } : undefined}
          error={errors.fields.fromLocationId}
          onChange={(v) => { setFromId(v); setLines((cur) => cur.map((l) => ({ ...l, batchId: '' }))); touch(); }}
        />
        <LocationSelect
          id="transfer-toLocationId"
          label="To"
          required
          locations={locations}
          movement="TransferIn"
          value={toId}
          disabledReasons={fromId ? { [fromId]: 'it is the source' } : undefined}
          error={errors.fields.toLocationId}
          onChange={(v) => { setToId(v); touch(); }}
        />
      </div>
      <TextField id="transfer-reason" label="Reason (optional)" maxLength={500} value={reason} error={errors.fields.reason} onChange={(e) => { setReason(e.target.value); touch(); }} />
      <section aria-labelledby="transfer-lines-heading" className="flex flex-col gap-3">
        <h2 id="transfer-lines-heading" className="text-sm font-bold uppercase tracking-wide text-ink-muted">Lines</h2>
        <ScanField
          id="transfer-scan"
          onScan={async (barcode) => {
            const sku = await onScan(barcode);
            if (!sku) return null;
            addSku(sku);
            return `${sku.displayName} — ${sku.skuCode}`;
          }}
        />
        <SkuPicker id="transfer-sku-picker" label="Or find an item" onSelect={addSku} />
        {lines.length === 0 ? (
          <p className="rounded-[var(--radius-md)] border border-dashed border-border-strong p-4 text-sm text-ink-muted">No lines yet.</p>
        ) : (
          <ol className="flex flex-col gap-3">
            {lines.map((line, index) => (
              <StockLineEditor
                key={line.key}
                line={line}
                index={index}
                idPrefix="transfer"
                locationId={fromId}
                errors={errorsForRow(errors, line.key, index)}
                onChange={(patch) => { setLines((cur) => cur.map((l) => (l.key === line.key ? { ...l, ...patch } : l))); touch(); }}
                onRemove={() => { setLines((cur) => cur.filter((l) => l.key !== line.key)); touch(); }}
              />
            ))}
          </ol>
        )}
      </section>
      {fromId && toId && fromId !== toId && <Banner tone="neutral">Stock moves from {name(fromId)} to {name(toId)}. Both ends are written to the ledger in one step.</Banner>}
      <div>
        <Button type="submit" disabled={isPending || submitting}>Review and post</Button>
      </div>
      <ActionDialog
        open={confirming}
        onOpenChange={setConfirming}
        title="Post this transfer?"
        consequence={`${lines.length} ${lines.length === 1 ? 'line' : 'lines'} will move from ${name(fromId)} to ${name(toId)}.`}
        confirmLabel="Post transfer"
        pendingLabel="Posting…"
        onConfirm={post}
      >
        <ul className="mt-3 max-h-60 list-disc overflow-y-auto pl-5 text-sm text-ink">
          {lines.map((l) => (
            <li key={l.key}>
              {formatQuantity(l.quantity, l.stockUnit)} of {l.skuLabel}
            </li>
          ))}
        </ul>
      </ActionDialog>
    </form>
  );
}
