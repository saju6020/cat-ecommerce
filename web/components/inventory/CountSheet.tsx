'use client';

import * as React from 'react';
import { Button } from '@/components/ui/button';
import { ScanField } from '@/components/common/ScanField';
import { ActionDialog } from './ReasonDialog';
import { Banner, Panel, SelectField } from './fields';
import { QuantityInput } from './QuantityInput';
import { SkuPicker } from './SkuPicker';
import { COUNT_SCOPE_LABELS } from './labels';
import { describeInventoryError } from '@/lib/api/errors';
import { formatExpiry } from '@/lib/format/inventory';
import { quantityProblem } from '@/lib/validation/inventory';
import { useBatches } from '@/lib/hooks/useInventory';
import type { SkuLookup } from '@/lib/api/inventory-lookup';
import type { CountDetail } from '@/lib/api/inventory-control';

type RowStatus = 'saved' | 'dirty' | 'saving' | 'error';

interface SheetRow {
  key: string;
  skuId: string;
  skuLabel: string;
  stockUnit: string;
  allowsFractional: boolean;
  batchId: string;
  lotLabel: string;
  value: string;
  status: RowStatus;
  error?: string;
}

const rowKey = (skuId: string, batchId: string) => `${skuId}|${batchId}`;

/** Lot and expiry ONLY — the batch list the server returns carries an on-hand figure, which a blind
 *  count must never show, so it is never rendered. */
function BatchChooser({ sku, onChoose, onCancel }: { sku: SkuLookup; onChoose: (batchId: string, label: string) => void; onCancel: () => void }) {
  const batches = useBatches({ skuId: sku.skuId, pageSize: 100 });
  const [batchId, setBatchId] = React.useState('');
  const items = batches.data?.items ?? [];
  return (
    <div className="flex flex-col gap-3 rounded-[var(--radius-md)] border border-border bg-surface p-3">
      <SelectField
        id="count-batch-chooser"
        label={`Which lot of ${sku.displayName}?`}
        value={batchId}
        hint={batches.isLoading ? 'Loading lots…' : items.length === 0 ? 'No lot of this item is known at this branch.' : undefined}
        onChange={(e) => setBatchId(e.target.value)}
      >
        <option value="">Choose a lot</option>
        {items.map((b) => (
          <option key={b.id} value={b.id}>
            Lot {b.lotNumber} · {formatExpiry(b.expiresOn, b.isExpired, b.daysToExpiry).text}
          </option>
        ))}
      </SelectField>
      <div className="flex gap-2">
        <Button
          disabled={!batchId}
          onClick={() => {
            const batch = items.find((b) => b.id === batchId);
            if (batch) onChoose(batch.id, `Lot ${batch.lotNumber}`);
          }}
        >
          Add to the sheet
        </Button>
        <Button variant="secondary" onClick={onCancel}>Cancel</Button>
      </div>
    </div>
  );
}

/**
 * `components/inventory/CountSheet.tsx` — HO-IMP-002 §11.5, US-IMP-14, ADR-FE-044. The count sheet is
 * BLIND: while the count is open there is no book quantity anywhere on it (the server sends none, and
 * the lot chooser shows lot and expiry only). Scan or pick an item, choose its lot, enter what was
 * counted; **each line is saved to the server as it is entered** (shared, reload-proof) with a
 * per-row saved / saving / not-saved indicator. Submitting is confirmed and stops the counting.
 */
export function CountSheet({
  count,
  onScan,
  onRecordLine,
  onSubmit,
}: {
  count: CountDetail;
  onScan: (barcode: string) => Promise<SkuLookup | null>;
  onRecordLine: (skuId: string, batchId: string, countedQuantity: string) => Promise<unknown>;
  onSubmit: () => Promise<unknown>;
}) {
  const [rows, setRows] = React.useState<SheetRow[]>(() =>
    count.lines.map((l) => ({
      key: rowKey(l.skuId, l.batchId),
      skuId: l.skuId,
      skuLabel: `${l.displayName} — ${l.skuCode}`,
      stockUnit: l.stockUnit,
      allowsFractional: true,
      batchId: l.batchId,
      lotLabel: `Lot ${l.lotNumber}`,
      value: l.countedQuantity,
      status: 'saved' as RowStatus,
    })),
  );
  const [adding, setAdding] = React.useState<SkuLookup | null>(null);
  const [confirming, setConfirming] = React.useState(false);

  function patchRow(key: string, patch: Partial<SheetRow>) {
    setRows((current) => current.map((r) => (r.key === key ? { ...r, ...patch } : r)));
  }

  async function save(row: SheetRow) {
    const problem =
      row.value.trim() === ''
        ? `Enter how many ${row.stockUnit} you counted (0 if none).`
        : quantityProblem(row.value, { stockUnit: row.stockUnit, allowsFractional: row.allowsFractional });
    if (problem) {
      patchRow(row.key, { status: 'error', error: problem });
      return;
    }
    patchRow(row.key, { status: 'saving', error: undefined });
    try {
      await onRecordLine(row.skuId, row.batchId, row.value.trim());
      patchRow(row.key, { status: 'saved' });
    } catch (error) {
      patchRow(row.key, { status: 'error', error: describeInventoryError(error) });
    }
  }

  function addRow(sku: SkuLookup, batchId: string, lotLabel: string) {
    const key = rowKey(sku.skuId, batchId);
    setRows((current) =>
      current.some((r) => r.key === key)
        ? current
        : [
            ...current,
            {
              key, skuId: sku.skuId, skuLabel: `${sku.displayName} — ${sku.skuCode}`, stockUnit: sku.stockUnit,
              allowsFractional: sku.allowsFractionalQuantity, batchId, lotLabel, value: '', status: 'dirty' as RowStatus,
            },
          ],
    );
    setAdding(null);
    setTimeout(() => document.getElementById(`count-row-${key}`)?.focus(), 0);
  }

  const unsaved = rows.some((r) => r.status !== 'saved');
  const scope = count.scope === 'FullLocation' ? COUNT_SCOPE_LABELS.FullLocation : COUNT_SCOPE_LABELS.SelectedSkus;

  return (
    <div className="flex flex-col gap-5">
      <Banner tone="info">
        This count is blind: you never see what the books say while you count. Enter what is actually on the shelf. Each line is saved as you go, so you can leave and come back.
      </Banner>
      <Panel title="This count">
        <p className="text-sm text-ink">
          {count.number} at {count.locationCode ?? 'this location'} · {scope}
          {count.scope === 'FullLocation' && ' — anything you do not count is counted as none.'}
        </p>
      </Panel>
      <section aria-labelledby="count-add-heading" className="flex flex-col gap-3">
        <h2 id="count-add-heading" className="text-sm font-bold uppercase tracking-wide text-ink-muted">Add a counted line</h2>
        <ScanField
          id="count-scan"
          onScan={async (barcode) => {
            const sku = await onScan(barcode);
            if (!sku) return null;
            setAdding(sku);
            return `${sku.displayName} — ${sku.skuCode}`;
          }}
        />
        <SkuPicker id="count-sku-picker" label="Or find an item" onSelect={setAdding} />
        {adding && <BatchChooser key={adding.skuId} sku={adding} onChoose={(batchId, label) => addRow(adding, batchId, label)} onCancel={() => setAdding(null)} />}
      </section>
      <section aria-labelledby="count-lines-heading" className="flex flex-col gap-3">
        <h2 id="count-lines-heading" className="text-sm font-bold uppercase tracking-wide text-ink-muted">Counted so far ({rows.length})</h2>
        {rows.length === 0 ? (
          <p className="rounded-[var(--radius-md)] border border-dashed border-border-strong p-4 text-sm text-ink-muted">Nothing counted yet.</p>
        ) : (
          <ul className="flex flex-col gap-3">
            {rows.map((row) => (
              <li key={row.key} className="grid items-end gap-3 rounded-[var(--radius-md)] border border-border bg-surface p-3 sm:grid-cols-[1fr_12rem_9rem]">
                <p className="min-w-0 break-words text-sm font-semibold text-ink">
                  {row.skuLabel}
                  <span className="block font-normal text-ink-muted">{row.lotLabel}</span>
                </p>
                <QuantityInput
                  id={`count-row-${row.key}`}
                  label="Counted"
                  value={row.value}
                  stockUnit={row.stockUnit}
                  allowsFractional={row.allowsFractional}
                  error={row.status === 'error' ? row.error : undefined}
                  onValueChange={(value) => patchRow(row.key, { value, status: 'dirty', error: undefined })}
                  onBlur={() => row.status === 'dirty' && void save(row)}
                  onEnter={() => void save(row)}
                />
                <div className="flex flex-col gap-1 text-sm" role="status" aria-live="polite">
                  <span className={row.status === 'error' ? 'font-semibold text-danger-ink' : 'text-ink-muted'}>
                    {row.status === 'saved' ? 'Saved' : row.status === 'saving' ? 'Saving…' : row.status === 'error' ? 'Not saved' : 'Not saved yet'}
                  </span>
                  {(row.status === 'dirty' || row.status === 'error') && (
                    <Button size="sm" variant="secondary" onClick={() => void save(row)}>
                      {row.status === 'error' ? 'Try again' : 'Save line'}
                    </Button>
                  )}
                </div>
              </li>
            ))}
          </ul>
        )}
      </section>
      <div className="flex flex-wrap items-center gap-3 border-t border-border pt-4">
        <Button onClick={() => setConfirming(true)} disabled={rows.length === 0 || unsaved}>
          Submit the count
        </Button>
        {unsaved && <p className="text-xs text-ink-muted">Save every line before submitting.</p>}
      </div>
      <ActionDialog
        open={confirming}
        onOpenChange={setConfirming}
        title="Submit this count?"
        consequence={`${rows.length} ${rows.length === 1 ? 'line' : 'lines'} counted. Counting stops and you will see the variance report next. Nothing is written to stock until you commit it.${count.scope === 'FullLocation' ? ' Anything at this location that you have not counted is counted as none.' : ''}`}
        confirmLabel="Submit the count"
        pendingLabel="Submitting…"
        onConfirm={onSubmit}
      />
    </div>
  );
}
