'use client';

import * as React from 'react';
import Link from 'next/link';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ActionDialog, ReasonDialog } from './ReasonDialog';
import { Banner, DefinitionList, PageHeading, Panel } from './fields';
import { StackedTable } from './StackedTable';
import { formatQuantity } from '@/lib/format/inventory';
import { formatInZone } from '@/lib/format/time';
import type { BreachAffectedBatch, BreachDetail } from '@/lib/api/inventory-breaches';

const STATUS_LABELS = { Open: 'Open', Quarantined: 'Stock quarantined', Closed: 'Closed' } as const;

/**
 * `components/inventory/StorageBreachDetail.tsx` — HO-IMP-002 §11.8, US-IMP-16 AC2. The breach, the
 * AFFECTED BATCHES the server flagged (each with how much is held at the location, with its unit),
 * then **Quarantine affected stock** — confirmed, naming where it goes, listing what moves — and
 * **Close** with a note. Actions come from the server's `allowedActions`.
 */
export function StorageBreachDetailView({
  breach,
  unitBySku,
  quarantineName,
  justReported,
  timeZoneId,
  onQuarantine,
  onClose,
}: {
  breach: BreachDetail;
  /** Stock unit by item code (the affected-batch rows carry none). */
  unitBySku: Record<string, string>;
  quarantineName: string | null;
  justReported: number | null;
  timeZoneId: string;
  onQuarantine: () => Promise<unknown>;
  onClose: (note: string) => Promise<unknown>;
}) {
  const [dialog, setDialog] = React.useState<'quarantine' | 'close' | null>(null);
  const { summary } = breach;
  const allowed = new Set(breach.allowedActions);
  const destination = quarantineName ?? 'the quarantine location';
  const held = (b: BreachAffectedBatch) => (b.skuCode && unitBySku[b.skuCode] ? formatQuantity(b.heldAtLocation, unitBySku[b.skuCode]!) : '—');

  return (
    <div className="flex flex-col gap-5">
      <PageHeading
        title="Storage breach"
        description={
          <span className="flex flex-wrap items-center gap-2">
            <Badge variant={summary.status === 'Open' ? 'warning' : 'neutral'}>{STATUS_LABELS[summary.status]}</Badge>
            <span>at {summary.locationCode ?? 'a location'}</span>
          </span>
        }
        actions={
          <>
            {allowed.has('quarantine') && <Button onClick={() => setDialog('quarantine')}>Quarantine affected stock…</Button>}
            {allowed.has('close') && <Button variant="secondary" onClick={() => setDialog('close')}>Close the report…</Button>}
            <Link href="/staff/inventory/storage-breaches" className="text-sm font-semibold text-accent-ink">All breaches</Link>
          </>
        }
      />
      {justReported !== null && (
        <Banner tone="info">
          Reported. {justReported} {justReported === 1 ? 'batch was' : 'batches were'} flagged, listed below.
        </Banner>
      )}
      <Panel title="What happened">
        <DefinitionList
          items={[
            { label: 'Observed', value: formatInZone(summary.observedOn, timeZoneId) },
            { label: 'What happened', value: summary.condition },
            { label: 'Temperature reached', value: breach.temperatureC === null ? 'Not recorded' : `${breach.temperatureC} °C` },
            { label: 'Duration', value: breach.durationMinutes === null ? 'Not recorded' : `${breach.durationMinutes} minutes` },
            ...(breach.quarantinedOn ? [{ label: 'Stock quarantined', value: formatInZone(breach.quarantinedOn, timeZoneId) }] : []),
            ...(breach.closingNote ? [{ label: 'Closing note', value: breach.closingNote }] : []),
          ]}
        />
      </Panel>
      <Panel title="Batches flagged">
        {breach.affectedBatches.length === 0 ? (
          <p className="text-sm text-ink-muted">No batch was held at that location when the breach was reported.</p>
        ) : (
          <StackedTable
            caption="Batches affected by the breach"
            rows={breach.affectedBatches}
            getKey={(b) => b.batchId}
            columns={[
              {
                id: 'item',
                header: 'Item',
                rowHeader: true,
                cell: (b: BreachAffectedBatch) => (
                  <Link href={`/staff/inventory/batches/${b.batchId}`} className="text-accent-ink">
                    {b.displayName ?? b.skuCode} · lot {b.lotNumber}
                  </Link>
                ),
              },
              { id: 'held', header: 'Held at the location', numeric: true, cell: held },
            ]}
          />
        )}
      </Panel>

      <ActionDialog
        open={dialog === 'quarantine'}
        onOpenChange={(open) => setDialog(open ? 'quarantine' : null)}
        title="Quarantine the affected stock?"
        consequence={`The stock listed below moves to ${destination} pending a decision. It is excluded from sellable stock while it is there.`}
        confirmLabel="Quarantine affected stock"
        pendingLabel="Moving…"
        onConfirm={onQuarantine}
      >
        <ul className="mt-3 max-h-60 list-disc overflow-y-auto pl-5 text-sm text-ink">
          {breach.affectedBatches.map((b) => (
            <li key={b.batchId}>
              {held(b)} of {b.displayName ?? b.skuCode}, lot {b.lotNumber} → {destination}
            </li>
          ))}
        </ul>
      </ActionDialog>
      <ReasonDialog
        open={dialog === 'close'}
        onOpenChange={(open) => setDialog(open ? 'close' : null)}
        title="Close this report?"
        consequence="Record the decision on this breach and close it. It stays on the record."
        reasonLabel="Closing note"
        confirmLabel="Close the report"
        onConfirm={onClose}
      />
    </div>
  );
}
