import { Banner, Panel } from '@/components/inventory/fields';
import { StackedTable } from '@/components/inventory/StackedTable';
import { formatQuantity } from '@/lib/format/inventory';
import type { ReconciliationRow } from '@/lib/api/inventory-reports';

/**
 * `components/stock/ReconciliationReport.tsx` — HO-IMP-002 §11.6. Positions that differ from the
 * entries they are derived from — EXPECTED EMPTY. When it is not, each mismatch shows the position
 * against what the ledger says, and a note to report it. The rows carry no unit, so each figure takes
 * its item's unit from `unitBySku` (the stock summary) — never shown bare.
 */
export function ReconciliationReport({
  items,
  unitBySku,
}: {
  items: ReconciliationRow[];
  unitBySku: Record<string, string>;
}) {
  const show = (row: ReconciliationRow, value: string) => formatQuantity(value, unitBySku[row.skuId] ?? 'units');
  if (items.length === 0) {
    return <Banner tone="info">No mismatches: every position agrees with the ledger entries it is derived from.</Banner>;
  }
  return (
    <div className="flex flex-col gap-3">
      <Banner tone="danger">
        {items.length} {items.length === 1 ? 'position does' : 'positions do'} not agree with the ledger. This should never happen — please report it.
      </Banner>
      <Panel title="Mismatches">
        <StackedTable
          caption="Positions that differ from the ledger"
          rows={items}
          getKey={(r) => `${r.batchId}-${r.locationId}`}
          columns={[
            { id: 'batch', header: 'Batch', rowHeader: true, cell: (r: ReconciliationRow) => r.batchId },
            { id: 'location', header: 'Location', cell: (r) => r.locationId },
            { id: 'onHand', header: 'On hand (position)', numeric: true, cell: (r) => show(r, r.onHand) },
            { id: 'expected', header: 'On hand (ledger sum)', numeric: true, cell: (r) => show(r, r.expectedOnHand) },
            { id: 'reserved', header: 'Reserved (position)', numeric: true, cell: (r) => show(r, r.reserved) },
            { id: 'expectedReserved', header: 'Reserved (ledger sum)', numeric: true, cell: (r) => show(r, r.expectedReserved) },
          ]}
        />
      </Panel>
    </div>
  );
}
