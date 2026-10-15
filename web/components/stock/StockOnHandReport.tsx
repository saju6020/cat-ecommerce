'use client';

import { ExportButton } from '@/components/inventory/ExportButton';
import { Panel } from '@/components/inventory/fields';
import { StackedTable } from '@/components/inventory/StackedTable';
import { formatDateOrDash, formatQuantity, formatUnitCost } from '@/lib/format/inventory';
import { formatMoney } from '@/lib/format/money';
import { formatDateOnly } from '@/lib/format/time';
import type { StockOnHandReport as StockOnHandReportData, StockOnHandRow } from '@/lib/api/inventory-reports';
import { BatchStateBadge } from './BatchStateBadge';

/**
 * `components/stock/StockOnHandReport.tsx` — HO-IMP-002 §11.6. Item × batch × location with the unit
 * cost, its basis (*landed* / *purchase*) and the value, and the server's total. Rows carry no stock
 * unit, so it comes from `unitBySku`; without it the quantity is left out, never shown bare (UI-8).
 */
export function StockOnHandReport({
  report,
  unitBySku,
  csvPath,
}: {
  report: StockOnHandReportData;
  unitBySku: Record<string, string>;
  csvPath: string;
}) {
  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <p className="text-sm text-ink">
          As of {formatDateOnly(report.asOf)} · total value <strong>{formatMoney(report.totalValue)}</strong>
        </p>
        <ExportButton path={csvPath} fallbackName="stock-on-hand.csv" />
      </div>
      <Panel title="Stock on hand">
        {report.rows.length === 0 ? (
          <p className="text-sm text-ink-muted">No stock matches these filters.</p>
        ) : (
          <StackedTable
            caption="Stock on hand by batch and location"
            rows={report.rows}
            getKey={(r) => `${r.batchId}-${r.locationId}`}
            columns={[
              { id: 'item', header: 'Item', rowHeader: true, cell: (r: StockOnHandRow) => `${r.displayName} (${r.skuCode})` },
              { id: 'lot', header: 'Lot', cell: (r) => `${r.lotNumber} · ${formatDateOrDash(r.expiresOn, 'no expiry date')}` },
              { id: 'state', header: 'State', cell: (r) => <BatchStateBadge status={r.batchStatus as 'Available'} /> },
              { id: 'loc', header: 'Location', cell: (r) => r.locationCode ?? '—' },
              {
                id: 'qty',
                header: 'On hand',
                numeric: true,
                cell: (r) => (unitBySku[r.skuId] ? `${formatQuantity(r.onHand, unitBySku[r.skuId]!)} (${formatQuantity(r.reserved, unitBySku[r.skuId]!)} reserved)` : '—'),
              },
              { id: 'cost', header: 'Unit cost', numeric: true, cell: (r) => `${formatUnitCost(r.unitCostBase)} (${r.costBasis})` },
              { id: 'value', header: 'Value', numeric: true, cell: (r) => formatMoney(r.value) },
            ]}
            footer={[{ label: 'Total value (from the server)', value: formatMoney(report.totalValue) }]}
          />
        )}
      </Panel>
    </div>
  );
}
