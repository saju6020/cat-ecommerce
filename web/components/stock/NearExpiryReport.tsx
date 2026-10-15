'use client';

import { ExportButton } from '@/components/inventory/ExportButton';
import { Panel } from '@/components/inventory/fields';
import { StackedTable } from '@/components/inventory/StackedTable';
import { formatDateOnly } from '@/lib/format/time';
import { formatQuantity, formatUnitCost } from '@/lib/format/inventory';
import { formatMoney } from '@/lib/format/money';
import type { NearExpiryReport as NearExpiryReportData, NearExpiryRow } from '@/lib/api/inventory-reports';
import { BatchStateBadge } from './BatchStateBadge';

/** Days remaining in words: *"47 days"*, *"expires today"*, *"expired 3 days ago"* (ADR-FE-047). */
function daysWords(days: number): string {
  if (days === 0) return 'expires today';
  if (days < 0) return `expired ${-days} ${days === -1 ? 'day' : 'days'} ago`;
  return `${days} ${days === 1 ? 'day' : 'days'}`;
}

/**
 * `components/stock/NearExpiryReport.tsx` — HO-IMP-002 §11.6, US-IMP-10 AC2. Per batch: item, lot,
 * expiry, DAYS REMAINING in words, the quantity (in total and per location, with its unit), the unit
 * cost and the VALUE AT RISK — all the server's — and the server's total. The report's rows carry no
 * stock unit, so the unit comes from `unitBySku` (the stock summary); where it cannot be resolved the
 * quantity is left out rather than shown bare (UI-8). CSV is the server's.
 */
export function NearExpiryReport({
  report,
  unitBySku,
  csvPath,
}: {
  report: NearExpiryReportData;
  unitBySku: Record<string, string>;
  csvPath: string;
}) {
  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <p className="text-sm text-ink">
          As of {formatDateOnly(report.asOf)} · batches expiring within {report.withinDays} {report.withinDays === 1 ? 'day' : 'days'}
          {report.includeExpired ? ', including expired stock' : ''} · total value at risk <strong>{formatMoney(report.totalValueAtRisk)}</strong>
        </p>
        <ExportButton path={csvPath} fallbackName="near-expiry.csv" />
      </div>
      <Panel title="Batches at risk">
        {report.rows.length === 0 ? (
          <p className="text-sm text-ink-muted">Nothing expires in this window.</p>
        ) : (
          <StackedTable
            caption="Near-expiry report"
            rows={report.rows}
            getKey={(r) => r.batchId}
            rowClassName={(r) => (r.isExpired ? 'bg-danger-bg/30' : undefined)}
            columns={[
              { id: 'item', header: 'Item', rowHeader: true, cell: (r: NearExpiryRow) => `${r.displayName} (${r.skuCode})` },
              { id: 'lot', header: 'Lot', cell: (r) => r.lotNumber },
              { id: 'expiry', header: 'Expires', cell: (r) => formatDateOnly(r.expiresOn) },
              { id: 'days', header: 'Days remaining', numeric: true, cell: (r) => <strong>{daysWords(r.daysRemaining)}</strong> },
              { id: 'state', header: 'State', cell: (r) => <BatchStateBadge status={r.status as 'Available'} isExpired={r.isExpired} /> },
              {
                id: 'qty',
                header: 'Quantity',
                numeric: true,
                cell: (r) => {
                  const unit = unitBySku[r.skuId];
                  if (!unit) return '—';
                  return (
                    <span>
                      {formatQuantity(r.quantity, unit)}
                      <span className="block text-xs font-normal text-ink-muted">
                        {r.locations.map((l) => `${l.locationCode ?? 'location'}: ${formatQuantity(l.onHand, unit)}`).join(' · ')}
                      </span>
                    </span>
                  );
                },
              },
              { id: 'cost', header: 'Unit cost', numeric: true, cell: (r) => `${formatUnitCost(r.unitCostBase)} (${r.costBasis})` },
              { id: 'value', header: 'Value at risk', numeric: true, cell: (r) => <strong>{formatMoney(r.valueAtRisk)}</strong> },
            ]}
            footer={[{ label: 'Total value at risk (from the server)', value: formatMoney(report.totalValueAtRisk) }]}
          />
        )}
      </Panel>
    </div>
  );
}
