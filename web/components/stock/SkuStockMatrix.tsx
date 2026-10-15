'use client';

import Link from 'next/link';
import { Banner } from '@/components/inventory/fields';
import { locationKindLabel } from '@/components/inventory/labels';
import { formatQuantity } from '@/lib/format/inventory';
import { useNarrowContainer } from '@/lib/hooks/useIsNarrow';
import type { SkuStockMatrix as SkuStockMatrixData, StockMatrixBatch } from '@/lib/api/inventory-stock';
import { BatchStateBadge, describeBatchState } from './BatchStateBadge';
import { ExpiryBadge } from './ExpiryBadge';

/**
 * `components/stock/SkuStockMatrix.tsx` — HO-IMP-002 §11.4, US-IMP-06/09. Rows are batches (lot,
 * expiry in words, state in words), columns are locations, cells are on hand / reserved WITH the unit.
 * A real `<table>` with row and column headers; in a narrow area a per-batch list instead, so
 * nothing scrolls sideways. An unusable batch says WHY in text (expired, quarantined, recalled —
 * held, in a quarantine location) and offers no action that would use it — the only link is to the
 * batch page.
 */
export function SkuStockMatrix({ matrix }: { matrix: SkuStockMatrixData }) {
  const [ref, narrow] = useNarrowContainer<HTMLDivElement>();
  const unit = matrix.stockUnit;
  const locations = new Map<string, { code: string; kind: string }>();
  for (const batch of matrix.batches) {
    for (const cell of batch.locations) locations.set(cell.locationId, { code: cell.locationCode, kind: cell.locationKind });
  }
  const columns = [...locations.entries()];

  if (matrix.batches.length === 0) {
    return <Banner tone="neutral">No batch of this item is held at this branch.</Banner>;
  }

  const cellText = (batch: StockMatrixBatch, locationId: string): string => {
    const cell = batch.locations.find((c) => c.locationId === locationId);
    if (!cell) return '—';
    const quarantine = cell.locationKind === 'Quarantine' ? ' (in quarantine location)' : '';
    return `${formatQuantity(cell.onHand, unit)} on hand, ${formatQuantity(cell.reserved, unit)} reserved${quarantine}`;
  };

  const header = (batch: StockMatrixBatch) => {
    const inQuarantine = batch.locations.some((c) => c.locationKind === 'Quarantine' && c.onHand !== '0');
    const state = describeBatchState({ status: batch.status, isExpired: batch.isExpired, inQuarantineLocation: inQuarantine });
    return (
      <div className="flex min-w-0 flex-col gap-1">
        <Link href={`/staff/inventory/batches/${batch.batchId}`} className="font-semibold text-accent-ink">
          Lot {batch.lotNumber}
        </Link>
        <ExpiryBadge expiresOn={batch.expiresOn} isExpired={batch.isExpired} daysToExpiry={batch.daysToExpiry} />
        <BatchStateBadge status={batch.status} isExpired={batch.isExpired} inQuarantineLocation={inQuarantine} />
        {state.why && <span className="text-xs text-ink-muted">{state.why}</span>}
      </div>
    );
  };

  if (narrow) {
    return (
      <div ref={ref}>
        <ul className="flex flex-col gap-3" aria-label={`Batches of ${matrix.displayName}`}>
          {matrix.batches.map((batch) => (
            <li key={batch.batchId} className="rounded-[var(--radius-md)] border border-border bg-surface p-3">
              {header(batch)}
              <dl className="mt-2 grid grid-cols-[minmax(5rem,8rem)_1fr] gap-x-3 gap-y-1 text-sm">
                {batch.locations.map((cell) => (
                  <div key={cell.locationId} className="contents">
                    <dt className="text-ink-muted">{cell.locationCode}</dt>
                    <dd>{cellText(batch, cell.locationId)}</dd>
                  </div>
                ))}
              </dl>
            </li>
          ))}
        </ul>
      </div>
    );
  }

  return (
    <div ref={ref}>
      <div
        role="region"
        aria-label={`Batches of ${matrix.displayName} by location (scrolls horizontally)`}
        tabIndex={0}
        className="overflow-x-auto rounded-[var(--radius-md)] border border-border"
      >
        <table className="w-full border-collapse bg-surface text-sm">
          <caption className="sr-only">Stock of {matrix.displayName} by batch and location</caption>
          <thead>
            <tr className="bg-surface-sunken">
              <th scope="col" className="px-3 py-2 text-left text-xs font-bold uppercase tracking-wide text-ink-muted">
                Batch
              </th>
              {columns.map(([id, location]) => (
                <th key={id} scope="col" className="px-3 py-2 text-left text-xs font-bold uppercase tracking-wide text-ink-muted">
                  {location.code}
                  <span className="block font-normal normal-case tracking-normal">{locationKindLabel(location.kind)}</span>
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {matrix.batches.map((batch) => (
              <tr key={batch.batchId} className="border-t border-border align-top">
                <th scope="row" className="px-3 py-2 text-left font-normal">
                  {header(batch)}
                </th>
                {columns.map(([id]) => (
                  <td key={id} className="px-3 py-2 tabular-nums text-ink">
                    {cellText(batch, id)}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
