'use client';

import * as React from 'react';
import Link from 'next/link';
import { Button, buttonVariants } from '@/components/ui/button';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { ScanField } from '@/components/common/ScanField';
import { ActionDialog } from './ReasonDialog';
import { Banner, TextAreaField } from './fields';
import { LocationSelect } from './LocationSelect';
import { ReceiptErrorSummary } from './ReceiptErrorSummary';
import { SkuPicker } from './SkuPicker';
import { StockLineEditor, type StockLineState } from './StockLineEditor';
import { stockLineFromSku } from './TransferForm';
import { ADJUSTMENT_REASONS, ADJUSTMENT_STATUS_LABELS } from './labels';
import { EMPTY_ERRORS, distributeError, errorsForRow, focusErrorTarget, renameLineField, stampRowKeys, type DistributedErrors } from './lineErrors';
import { formatQuantity } from '@/lib/format/inventory';
import { formatMoney } from '@/lib/format/money';
import { quantityProblem } from '@/lib/validation/inventory';
import type { SkuLookup, StockLocation } from '@/lib/api/inventory-lookup';
import type { AdjustmentReason, RequestAdjustmentRequest, RequestedAdjustment } from '@/lib/api/inventory-control';

const TOP_FIELDS = ['locationId', 'reason', 'note'] as const;

/**
 * `components/inventory/AdjustmentForm.tsx` — HO-IMP-002 §11.5, US-IMP-13 AC2. Location; a REASON as
 * a radio list with plain-language help (the reason fixes whether stock is removed or added, shown as
 * *"removes"* / *"adds"*); a REQUIRED note; lines by scan or picker with a batch and a quantity. The
 * confirmation is explicit about permanence. Whether the adjustment needs a second person's approval
 * is the SERVER's answer, shown only after it is given — no ledger claim is made for a pending one.
 */
export function AdjustmentForm({
  locations,
  onScan,
  onRequest,
  isPending,
}: {
  locations: StockLocation[] | undefined;
  onScan: (barcode: string) => Promise<SkuLookup | null>;
  onRequest: (request: RequestAdjustmentRequest) => Promise<RequestedAdjustment>;
  isPending: boolean;
}) {
  const [locationId, setLocationId] = React.useState('');
  const [reason, setReason] = React.useState<AdjustmentReason | ''>('');
  const [correctionDirection, setCorrectionDirection] = React.useState<'removes' | 'adds'>('removes');
  const [note, setNote] = React.useState('');
  const [lines, setLines] = React.useState<StockLineState[]>([]);
  const [errors, setErrors] = React.useState<DistributedErrors>(EMPTY_ERRORS);
  const [confirming, setConfirming] = React.useState(false);
  const [dirty, setDirty] = React.useState(false);
  const [result, setResult] = React.useState<RequestedAdjustment | null>(null);

  const meta = ADJUSTMENT_REASONS.find((r) => r.value === reason);
  const direction: 'removes' | 'adds' | null = !meta ? null : meta.direction === 'either' ? correctionDirection : meta.direction;
  const touch = () => setDirty(true);
  const sign = direction === 'removes' ? '-' : '';

  function review(event: React.FormEvent) {
    event.preventDefault();
    const local: DistributedErrors = { fields: {}, lines: {}, general: [], firstTarget: null };
    if (!locationId) { local.fields.locationId = 'Choose the location.'; local.firstTarget ??= 'locationId'; }
    if (!reason) { local.fields.reason = 'Choose a reason.'; local.firstTarget ??= 'reason'; }
    if (note.trim() === '') { local.fields.note = 'A note is required — say what happened.'; local.firstTarget ??= 'note'; }
    if (lines.length === 0) local.general.push('Add at least one line.');
    lines.forEach((line, index) => {
      const lineErrors: Record<string, string> = {};
      if (!line.batchId) lineErrors.batchId = 'Choose which batch.';
      const problem = line.quantity.trim() === '' ? `Enter how many ${line.stockUnit}.` : quantityProblem(line.quantity, { stockUnit: line.stockUnit, allowsFractional: line.allowsFractional });
      if (problem) lineErrors.quantity = problem;
      if (Object.keys(lineErrors).length > 0) {
        local.lines[index] = lineErrors;
        local.firstTarget ??= `line-${index}-${lineErrors.batchId ? 'batchId' : 'quantity'}`;
      }
    });
    setErrors(stampRowKeys(local, lines.map((l) => l.key)));
    if (local.firstTarget || local.general.length > 0) {
      focusErrorTarget('adjustment', local.firstTarget);
      return;
    }
    setConfirming(true);
  }

  async function request() {
    try {
      const requested = await onRequest({
        locationId,
        reason: reason as AdjustmentReason,
        note: note.trim(),
        lines: lines.map((l) => ({ skuId: l.skuId, batchId: l.batchId, quantityDelta: `${sign}${l.quantity.trim()}` })),
      });
      setDirty(false);
      setResult(requested);
    } catch (error) {
      setConfirming(false);
      const distributed = renameLineField(distributeError(error, { knownFields: TOP_FIELDS }), 'quantityDelta', 'quantity');
      setErrors(stampRowKeys(distributed, lines.map((l) => l.key)));
      setTimeout(() => focusErrorTarget('adjustment', distributed.firstTarget), 0);
    }
  }

  if (result) {
    const pending = result.requiresApproval;
    return (
      <div className="flex max-w-2xl flex-col gap-3" role="status">
        <Banner tone={pending ? 'warning' : 'info'}>
          <p className="font-semibold">
            {result.number} — {ADJUSTMENT_STATUS_LABELS[result.status] ?? result.status}
          </p>
          <p className="mt-1">
            {pending
              ? `This adjustment is worth ${formatMoney(result.valueBase)}, so it needs approval from someone other than you before it changes any stock. Nothing has been written to the ledger yet.`
              : `Worth ${formatMoney(result.valueBase)}. It was recorded as posted.`}
          </p>
        </Banner>
        <div className="flex flex-wrap gap-2">
          <Link href={`/staff/inventory/adjustments/${result.adjustmentId}`} className={buttonVariants({ variant: 'primary' })}>
            View the adjustment
          </Link>
          <Button
            variant="secondary"
            onClick={() => {
              setResult(null);
              setLines([]);
              setNote('');
              setReason('');
            }}
          >
            Raise another
          </Button>
        </div>
      </div>
    );
  }

  const locationName = locations?.find((l) => l.id === locationId)?.name ?? 'the chosen location';

  return (
    <form onSubmit={review} noValidate className="flex max-w-4xl flex-col gap-5">
      <UnsavedChangesGuard when={dirty} />
      <ReceiptErrorSummary errors={errors} idPrefix="adjustment" title="This adjustment can't be requested yet." />
      <LocationSelect
        id="adjustment-locationId"
        label="Location"
        required
        locations={locations}
        value={locationId}
        error={errors.fields.locationId}
        className="max-w-md"
        onChange={(v) => { setLocationId(v); setLines((cur) => cur.map((l) => ({ ...l, batchId: '' }))); touch(); }}
      />
      <fieldset className="flex flex-col gap-2" aria-describedby={errors.fields.reason ? 'adjustment-reason-error' : undefined}>
        <legend className="text-sm font-semibold text-ink">Reason *</legend>
        {ADJUSTMENT_REASONS.map((r, index) => (
          <label key={r.value} className="flex items-start gap-2 text-sm text-ink">
            <input
              id={index === 0 ? 'adjustment-reason' : undefined}
              type="radio"
              name="adjustment-reason"
              value={r.value}
              checked={reason === r.value}
              onChange={() => { setReason(r.value); touch(); }}
              className="mt-0.5 h-4 w-4 accent-[var(--accent)]"
            />
            <span>
              <span className="font-semibold">{r.label}</span> — {r.help}
              {r.direction !== 'either' && <span className="text-ink-muted"> ({r.direction === 'removes' ? 'removes' : 'adds'} stock)</span>}
            </span>
          </label>
        ))}
        {errors.fields.reason && <p id="adjustment-reason-error" role="alert" className="text-xs font-semibold text-danger-ink">{errors.fields.reason}</p>}
        {meta?.direction === 'either' && (
          <label className="mt-1 flex items-center gap-2 text-sm text-ink">
            This correction
            <select
              aria-label="Does this correction add or remove stock?"
              value={correctionDirection}
              onChange={(e) => { setCorrectionDirection(e.target.value as 'removes' | 'adds'); touch(); }}
              className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-2 py-1 text-sm"
            >
              <option value="removes">removes stock</option>
              <option value="adds">adds stock</option>
            </select>
          </label>
        )}
      </fieldset>
      <TextAreaField id="adjustment-note" label="Note" required value={note} error={errors.fields.note} hint="Required — say what happened, so the next person understands the entry." maxLength={500} onChange={(e) => { setNote(e.target.value); touch(); }} />

      <section aria-labelledby="adjustment-lines-heading" className="flex flex-col gap-3">
        <h2 id="adjustment-lines-heading" className="text-sm font-bold uppercase tracking-wide text-ink-muted">Lines</h2>
        <ScanField
          id="adjustment-scan"
          onScan={async (barcode) => {
            const sku = await onScan(barcode);
            if (!sku) return null;
            setLines((cur) => [...cur, stockLineFromSku(sku)]);
            touch();
            return `${sku.displayName} — ${sku.skuCode}`;
          }}
        />
        <SkuPicker id="adjustment-sku-picker" label="Or find an item" onSelect={(sku) => { setLines((cur) => [...cur, stockLineFromSku(sku)]); touch(); }} />
        {lines.length === 0 ? (
          <p className="rounded-[var(--radius-md)] border border-dashed border-border-strong p-4 text-sm text-ink-muted">No lines yet.</p>
        ) : (
          <ol className="flex flex-col gap-3">
            {lines.map((line, index) => (
              <StockLineEditor
                key={line.key}
                line={line}
                index={index}
                idPrefix="adjustment"
                locationId={locationId}
                direction={direction}
                errors={errorsForRow(errors, line.key, index)}
                onChange={(patch) => { setLines((cur) => cur.map((l) => (l.key === line.key ? { ...l, ...patch } : l))); touch(); }}
                onRemove={() => { setLines((cur) => cur.filter((l) => l.key !== line.key)); touch(); }}
              />
            ))}
          </ol>
        )}
      </section>
      <div>
        <Button type="submit" disabled={isPending}>Review and request</Button>
      </div>

      <ActionDialog
        open={confirming}
        onOpenChange={setConfirming}
        title="Request this adjustment?"
        consequence={
          direction === 'removes'
            ? 'This reduces stock and cannot be undone — a correction is a new adjustment.'
            : direction === 'adds'
              ? 'This adds stock and cannot be undone — a correction is a new adjustment.'
              : 'This changes stock and cannot be undone — a correction is a new adjustment.'
        }
        confirmLabel="Request adjustment"
        pendingLabel="Requesting…"
        onConfirm={request}
      >
        <ul className="mt-3 max-h-60 list-disc overflow-y-auto pl-5 text-sm text-ink">
          {lines.map((l) => (
            <li key={l.key}>
              {direction === 'adds' ? 'Adds' : 'Reduces'} {l.skuLabel} by {formatQuantity(l.quantity, l.stockUnit)} at {locationName}
            </li>
          ))}
        </ul>
        <p className="mt-2 text-xs text-ink-muted">If it is worth more than the approval limit, someone else must approve it before any stock changes.</p>
      </ActionDialog>
    </form>
  );
}
