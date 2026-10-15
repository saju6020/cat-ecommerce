'use client';

import Link from 'next/link';
import { X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { DataTable } from '@/components/common/DataTable';
import { Banner } from '@/components/inventory/fields';
import { ExportButton } from '@/components/inventory/ExportButton';
import { ledgerKindLabel, referenceKindLabel } from '@/components/inventory/labels';
import { formatSignedQuantity } from '@/lib/format/inventory';
import { formatInZone } from '@/lib/format/time';
import type { LedgerEntry } from '@/lib/api/inventory-stock';

/**
 * `components/stock/LedgerSearch.tsx` — HO-IMP-002 §11.4, US-IMP-15. The immutable stock ledger,
 * read-only: time (in the branch's zone), what happened in words, the signed quantity with its unit,
 * location, batch and lot, the reference, who did it, and — when FEFO was overridden — that and the
 * reason. There is NO edit or delete control anywhere, and it says so. The date range is required
 * (the filter bar defaults it to the last 7 days). CSV comes from the server.
 */
export function LedgerSearch({
  rows,
  totalCount,
  pageSize,
  isLoading,
  isError,
  onRetry,
  rangeProblem,
  exportPath,
  timeZoneId,
  idFilters,
}: {
  rows: LedgerEntry[] | undefined;
  totalCount: number;
  pageSize: number;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
  /** The server's range rule, stated before anything is sent. */
  rangeProblem: string | null;
  exportPath: string;
  timeZoneId: string;
  /** Active id filters (item, batch, reference), each removable. */
  idFilters: Array<{ label: string; onClear: () => void }>;
}) {
  return (
    <div className="flex flex-col gap-3">
      {idFilters.length > 0 && (
        <ul className="flex flex-wrap gap-2" aria-label="Active filters">
          {idFilters.map((filter) => (
            <li key={filter.label} className="flex items-center gap-1 rounded-full bg-accent-soft px-3 py-1 text-sm text-accent-ink">
              {filter.label}
              <Button variant="ghost" size="icon" aria-label={`Remove filter: ${filter.label}`} onClick={filter.onClear}>
                <X size={12} aria-hidden="true" />
              </Button>
            </li>
          ))}
        </ul>
      )}
      <div className="flex flex-wrap items-start justify-between gap-3">
        <p className="text-sm text-ink-muted">Stock ledger entries are permanent. They can be searched and exported, never edited or deleted.</p>
        <ExportButton path={exportPath} fallbackName="stock-ledger.csv" />
      </div>
      {rangeProblem ? (
        <Banner tone="danger">{rangeProblem}</Banner>
      ) : (
        <DataTable
          caption="Stock ledger"
          rows={rows}
          getRowId={(row) => String(row.sequence)}
          isLoading={isLoading}
          isError={isError}
          onRetry={onRetry}
          pagination={{ totalCount, pageSize }}
          emptyState={{ title: 'No ledger entries in this range', description: 'Widen the dates or clear a filter.' }}
          columns={[
            { id: 'when', header: 'When', cell: (e) => formatInZone(e.occurredOn, timeZoneId) },
            { id: 'kind', header: 'What', cell: (e) => ledgerKindLabel(e.kind) },
            { id: 'qty', header: 'Quantity', cell: (e) => <strong>{formatSignedQuantity(e.quantityDelta, e.stockUnit)}</strong>, cellClassName: 'tabular-nums' },
            { id: 'loc', header: 'Location', cell: (e) => e.locationCode ?? '—' },
            {
              id: 'batch',
              header: 'Batch',
              cell: (e) => (
                <Link href={`/staff/inventory/batches/${e.batchId}`} className="text-accent-ink">
                  {e.skuCode} · lot {e.lotNumber}
                </Link>
              ),
            },
            { id: 'ref', header: 'Reference', cell: (e) => `${referenceKindLabel(e.referenceKind)}${e.referenceLabel ? ` ${e.referenceLabel}` : ''}` },
            { id: 'by', header: 'By', cell: (e) => e.performedByName ?? '—' },
            {
              id: 'fefo',
              header: 'FEFO override',
              cell: (e) => (e.fefoOverridden ? <span>Overridden — {e.overrideReason ?? 'no reason recorded'}</span> : 'No'),
            },
            { id: 'note', header: 'Note', cell: (e) => e.note ?? '' },
          ]}
        />
      )}
    </div>
  );
}
