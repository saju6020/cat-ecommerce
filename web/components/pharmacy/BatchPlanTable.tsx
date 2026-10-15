'use client';

import { Ban, CheckCircle2, Clock } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Quantity } from '@/components/common/Quantity';
import { ResponsiveTable, type Column } from '@/components/billing/ResponsiveTable';
import type { PlanBatch } from '@/lib/api/pharmacy';
import { formatDay, fromScaled, toScaled } from '@/lib/format/pharmacy';
import { unitLabel } from '@/lib/format/units';

/**
 * ⭐ `BatchPlanTable` — HO-PHM-002 §11.4, ADR-FE-062. The server's batch plan, listed: lot, expiry,
 * available (`Quantity`), status as words, and a quantity box per batch. A batch is chosen with its
 * checkbox (a supply can be split across batches, so a radio — one choice — cannot express it; the
 * group is labelled and every control has a name). **Expired and non-dispensable batches are listed
 * but disabled, with the reason written as text** (never colour alone): *"Expired 12 Mar 2026 — cannot
 * be dispensed"*. The client has no expiry logic: `isExpired` and `isDispensable` are the server's.
 * The quantity unit is always beside the number.
 */
export function BatchPlanTable({
  batches,
  suggestedIds,
  selection,
  unit,
  needed,
  highlightId,
  onChange,
}: {
  batches: PlanBatch[];
  suggestedIds: ReadonlySet<string>;
  /** batchId → the quantity text typed; a batch is "chosen" while its text is non-empty. */
  selection: Record<string, string>;
  /** The dispensing unit of the prescription ("Millilitre"). */
  unit: string;
  /** The prescribed quantity, as the server sent it. */
  needed: string;
  highlightId: string | null;
  onChange: (batchId: string, quantity: string) => void;
}) {
  const unitText = unitLabel(unit, '2');

  function toggle(batch: PlanBatch, checked: boolean) {
    if (!checked) return onChange(batch.batchId, '');
    const others = Object.entries(selection)
      .filter(([id]) => id !== batch.batchId)
      .reduce((sum, [, text]) => sum + (Number.isNaN(toScaled(text)) ? 0 : toScaled(text)), 0);
    const remaining = Math.max(toScaled(needed) - others, 0);
    const take = Math.min(
      toScaled(batch.available.amount),
      remaining > 0 ? remaining : toScaled(needed),
    );
    onChange(batch.batchId, fromScaled(take));
  }

  const columns: Array<Column<PlanBatch>> = [
    {
      id: 'use',
      header: 'Use',
      cell: (batch) => (
        <input
          type="checkbox"
          aria-label={`Use lot ${batch.lotNumber}`}
          checked={Boolean(selection[batch.batchId])}
          disabled={!batch.isDispensable}
          onChange={(event) => toggle(batch, event.target.checked)}
          className="h-4 w-4 accent-[var(--accent)]"
        />
      ),
    },
    {
      id: 'lot',
      header: 'Lot',
      primary: true,
      cell: (batch) => (
        <span className={highlightId === batch.batchId ? 'rounded bg-accent-soft px-1' : undefined}>
          {batch.lotNumber}
          <span className="block text-xs font-normal text-ink-muted">
            {batch.productName} · {batch.variantLabel}
          </span>
        </span>
      ),
    },
    {
      id: 'expires',
      header: 'Expires',
      cell: (batch) => (batch.expiresOn ? formatDay(batch.expiresOn) : 'No expiry date'),
    },
    { id: 'available', header: 'Available', cell: (batch) => <Quantity value={batch.available} /> },
    {
      id: 'status',
      header: 'Status',
      cell: (batch) => {
        if (batch.isExpired)
          return (
            <span className="flex flex-col gap-1">
              <Badge variant="danger" icon={<Clock size={12} aria-hidden="true" />}>
                Expired
              </Badge>
              <span className="text-xs text-danger-ink">
                Expired {formatDay(batch.expiresOn)} — cannot be dispensed
              </span>
            </span>
          );
        if (!batch.isDispensable)
          return (
            <span className="flex flex-col gap-1">
              <Badge variant="neutral" icon={<Ban size={12} aria-hidden="true" />}>
                Not dispensable
              </Badge>
              <span className="text-xs text-ink-muted">
                {batch.notDispensableReason ?? 'This batch cannot be dispensed.'}
              </span>
            </span>
          );
        return suggestedIds.has(batch.batchId) ? (
          <Badge variant="success" icon={<CheckCircle2 size={12} aria-hidden="true" />}>
            Suggested
          </Badge>
        ) : (
          <span className="text-ink-muted">Available</span>
        );
      },
    },
    {
      id: 'quantity',
      header: 'Quantity to dispense',
      cell: (batch) => (
        <span className="inline-flex items-center gap-2">
          <input
            type="text"
            inputMode="decimal"
            autoComplete="off"
            aria-label={`Quantity from lot ${batch.lotNumber}, in ${unitText}`}
            value={selection[batch.batchId] ?? ''}
            disabled={!batch.isDispensable}
            onChange={(event) => onChange(batch.batchId, event.target.value)}
            className="w-24 min-w-0 rounded-[var(--radius-sm)] border border-border-strong bg-surface px-2 py-1.5 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent disabled:bg-surface-sunken"
          />
          <span className="text-sm text-ink-muted">{unitText}</span>
        </span>
      ),
    },
  ];

  return (
    <fieldset className="flex min-w-0 flex-col gap-2">
      <legend className="mb-1 text-sm font-semibold text-ink">Batches to dispense from</legend>
      <ResponsiveTable
        caption="Batches in the dispensary"
        columns={columns}
        rows={batches}
        getRowId={(batch) => batch.batchId}
        dense
        emptyState={{
          title: 'No batches of this medicine are in the dispensary',
          description: 'Ask Inventory to receive stock before this can be dispensed.',
        }}
      />
    </fieldset>
  );
}
