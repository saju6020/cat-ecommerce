'use client';

import { QueryBoundary } from '@/components/inventory/QueryBoundary';
import { ReconciliationReport } from '@/components/stock/ReconciliationReport';
import { useReconciliation, useUnitBySku } from '@/lib/hooks/useInventory';

/** `/staff/inventory/reports/reconciliation` — positions that differ from the ledger (expected empty). */
export default function ReconciliationPage() {
  const query = useReconciliation();
  const units = useUnitBySku();
  return <QueryBoundary query={query}>{(report) => <ReconciliationReport items={report.items} unitBySku={units} />}</QueryBoundary>;
}
