import Link from 'next/link';
import type { CodCollection, CodCourierReconciliation } from '@/lib/api/billing-reconciliation';
import { formatDateOnly } from '@/lib/format/time';
import { Amount } from './Amount';
import { Section, Stat } from './PageHeader';
import { ResponsiveTable, type Column } from './ResponsiveTable';

/**
 * `components/billing/CodTables.tsx` — HO-BIL-002 §11.7 (US-BIL-06 AC1). `CodCollectionsTable`: what
 * couriers collected on delivery — collected, remitted, outstanding and **shortfall** per collection,
 * its status as text (*Unremitted / Shortfall / Remitted*) and an *Overdue* flag in words.
 * `CodReconciliationSummary`: per courier — collected, remitted, shortfall, still unremitted and
 * unapplied remittance money — with the shortfall lines listed. Every figure is the server's.
 */
export function CodCollectionsTable({
  collections,
  isLoading,
  isError,
  onRetry,
}: {
  collections: CodCollection[] | undefined;
  isLoading?: boolean;
  isError?: boolean;
  onRetry?: () => void;
}) {
  const columns: Array<Column<CodCollection>> = [
    {
      id: 'receipt',
      header: 'Receipt',
      primary: true,
      sortValue: (c) => c.receiptNumber ?? '',
      cell: (c) => (
        <Link href={`/staff/billing/payments/${c.paymentId}`} className="font-semibold text-accent-ink hover:underline">
          {c.receiptNumber ?? 'Payment'}
        </Link>
      ),
    },
    { id: 'customer', header: 'Customer', sortValue: (c) => c.customerName, cell: (c) => c.customerName },
    { id: 'courier', header: 'Courier', cell: (c) => c.courierName ?? c.courierCode ?? '—' },
    { id: 'received', header: 'Received', sortValue: (c) => c.receivedOn, cell: (c) => formatDateOnly(c.receivedOn) },
    { id: 'collected', header: 'Collected', numeric: true, cell: (c) => <Amount money={c.collected} /> },
    { id: 'remitted', header: 'Remitted', numeric: true, cell: (c) => <Amount money={c.remitted} /> },
    { id: 'outstanding', header: 'Outstanding', numeric: true, cell: (c) => <Amount money={c.outstanding} /> },
    { id: 'shortfall', header: 'Shortfall', numeric: true, cell: (c) => <Amount money={c.shortfall} /> },
    {
      id: 'status',
      header: 'Status',
      cell: (c) => (
        <span>
          {c.status}
          {c.overdue && <strong className="ml-2 text-danger-ink">Overdue</strong>}
        </span>
      ),
    },
  ];
  return (
    <ResponsiveTable
      caption="COD collections"
      columns={columns}
      rows={collections}
      getRowId={(c) => c.paymentId}
      isLoading={isLoading}
      isError={isError}
      onRetry={onRetry}
      minDesktopWidth={1024}
      dense
      emptyState={{ title: 'No cash-on-delivery collections match.' }}
    />
  );
}

export function CodReconciliationSummary({ couriers }: { couriers: CodCourierReconciliation[] }) {
  if (couriers.length === 0) {
    return <p className="text-sm text-ink-muted">No courier has collected anything in this range.</p>;
  }
  return (
    <div className="flex flex-col gap-4">
      {couriers.map((courier) => (
        <Section key={courier.courierCode} title={`Courier ${courier.courierCode}`}>
          <dl className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
            <Stat label="Collected"><Amount money={courier.collected} /></Stat>
            <Stat label="Remitted"><Amount money={courier.remitted} /></Stat>
            <Stat label="Shortfall"><Amount money={courier.shortfall} /></Stat>
            <Stat label="Not yet remitted"><Amount money={courier.unremitted} /></Stat>
            <Stat label="Unapplied remittances"><Amount money={courier.unappliedRemittances} /></Stat>
          </dl>
          {courier.shortfallLines.length > 0 && (
            <div>
              <h3 className="text-sm font-semibold text-ink">Shortfall lines</h3>
              <ul className="mt-1 flex flex-col gap-1 text-sm">
                {courier.shortfallLines.map((line) => (
                  <li key={line.paymentId}>
                    {line.receiptNumber} · {line.customerName} — short <Amount money={line.shortfall} className="font-semibold text-danger-ink" />
                    {line.overdue && ' · overdue'}
                  </li>
                ))}
              </ul>
            </div>
          )}
        </Section>
      ))}
    </div>
  );
}
