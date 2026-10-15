'use client';

import { Button } from '@/components/ui/button';
import type { Charge } from '@/lib/api/billing-staff';
import { formatDateOnly } from '@/lib/format/time';
import { Amount } from './Amount';
import { ResponsiveTable, type Column } from './ResponsiveTable';

/**
 * `components/billing/UnbilledChargesTable.tsx` — HO-BIL-002 §11.1. The customer's unbilled charges:
 * date, business line, kind, description, quantity, unit price, discount. The checkboxes feed the
 * composer (the account screen turns the selection into `?charges=`); **Void** (reason, confirm) is
 * offered on each — the charges endpoint carries no per-row action list, so the server decides and
 * answers `ChargeAlreadyInvoiced` / `403` where voiding is not allowed.
 */
export function UnbilledChargesTable({
  charges,
  selected,
  onToggle,
  onVoid,
  isLoading,
  isError,
  onRetry,
}: {
  charges: Charge[] | undefined;
  selected: string[];
  onToggle: (chargeId: string) => void;
  onVoid: (charge: Charge) => void;
  isLoading?: boolean;
  isError?: boolean;
  onRetry?: () => void;
}) {
  const columns: Array<Column<Charge>> = [
    {
      id: 'select',
      header: 'Select',
      primary: true,
      cell: (c) => (
        <label className="inline-flex items-center gap-2">
          <input
            type="checkbox"
            checked={selected.includes(c.chargeId)}
            onChange={() => onToggle(c.chargeId)}
            className="h-4 w-4"
            aria-label={`Select ${c.description}`}
          />
          <span className="md:sr-only">{c.description}</span>
        </label>
      ),
    },
    { id: 'date', header: 'Date', cell: (c) => formatDateOnly(c.serviceDate) },
    { id: 'line', header: 'Business line', cell: (c) => c.businessLine },
    { id: 'kind', header: 'Kind', cell: (c) => c.itemKind },
    { id: 'description', header: 'Description', cell: (c) => c.description },
    { id: 'qty', header: 'Qty', numeric: true, cell: (c) => c.quantity },
    { id: 'unit', header: 'Unit price', numeric: true, cell: (c) => <Amount money={c.unitPrice} /> },
    {
      id: 'discount',
      header: 'Discount',
      numeric: true,
      cell: (c) => <Amount money={c.lineDiscount} />,
    },
    {
      id: 'actions',
      header: 'Actions',
      cell: (c) => (
        <Button variant="ghost" size="sm" onClick={() => onVoid(c)}>
          Void<span className="sr-only"> {c.description}</span>
        </Button>
      ),
    },
  ];

  return (
    <ResponsiveTable
      caption="Unbilled charges"
      columns={columns}
      rows={charges}
      getRowId={(c) => c.chargeId}
      isLoading={isLoading}
      isError={isError}
      onRetry={onRetry}
      emptyState={{
        title: 'Nothing is waiting to be billed.',
        description: 'Charges from visits, shop orders and boarding appear here once they are recorded.',
      }}
    />
  );
}
