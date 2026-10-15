'use client';

import { CheckCircle2, ShieldAlert } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Quantity } from '@/components/common/Quantity';
import { ResponsiveTable, type Column, type Pager } from '@/components/billing/ResponsiveTable';
import { Tabs } from '@/components/billing/Tabs';
import type { PrescriptionScope, PrescriptionSummary } from '@/lib/api/prescriptions';
import { formatDay, routeLabel } from '@/lib/format/pharmacy';
import { formatRemaining } from '@/lib/format/units';
import { PrescriptionStatusBadge } from './PrescriptionStatusBadge';

/**
 * `CatPrescriptionsTable` — HO-PHM-002 §11.3 (US-PHM-01, 07). *Active* / *All* tabs; each row: number
 * (the link), medicine with its strength and a *Controlled* badge, the dose (`Quantity`), directions,
 * remaining days, supplies (*1 of 3*), valid until, the status as a word, the prescriber and whether
 * warnings were acknowledged (icon and text). A table from 768 px, cards below it (UI-1).
 */
export function CatPrescriptionsTable({
  catId,
  scope,
  onScopeChange,
  rows,
  pager,
  isLoading,
  isError,
  onRetry,
}: {
  catId: string;
  scope: PrescriptionScope;
  onScopeChange: (scope: PrescriptionScope) => void;
  rows: PrescriptionSummary[] | undefined;
  pager: Pager;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
}) {
  const columns: Array<Column<PrescriptionSummary>> = [
    {
      id: 'number',
      header: 'Prescription',
      primary: true,
      sortValue: (row) => row.prescriptionNumber,
      cell: (row) => row.prescriptionNumber,
    },
    {
      id: 'medicine',
      header: 'Medicine',
      cell: (row) => (
        <span className="flex flex-col gap-1">
          <span>{row.medicine.name}</span>
          <span className="text-xs text-ink-muted" data-clinical-value>
            {row.medicine.strength}
          </span>
          {row.medicine.isControlled && (
            <Badge variant="warning" icon={<ShieldAlert size={12} aria-hidden="true" />}>
              Controlled
            </Badge>
          )}
        </span>
      ),
    },
    { id: 'dose', header: 'Dose', cell: (row) => <Quantity value={row.dose} /> },
    {
      id: 'directions',
      header: 'Directions',
      cell: (row) => `${routeLabel(row.route)}, ${row.frequency}`,
    },
    { id: 'remaining', header: 'Remaining', cell: (row) => formatRemaining(row.remainingDays) },
    {
      id: 'supplies',
      header: 'Supplies',
      cell: (row) => `${row.suppliesDispensed} of ${row.suppliesAuthorised}`,
    },
    { id: 'validUntil', header: 'Valid until', cell: (row) => formatDay(row.validUntil) },
    {
      id: 'status',
      header: 'Status',
      cell: (row) => <PrescriptionStatusBadge status={row.status} />,
    },
    { id: 'vet', header: 'Prescriber', cell: (row) => row.prescriberName },
    {
      id: 'warnings',
      header: 'Warnings',
      cell: (row) =>
        row.hasAcknowledgedWarnings ? (
          <span className="inline-flex items-center gap-1 text-warning-ink">
            <CheckCircle2 size={14} aria-hidden="true" />
            Acknowledged
          </span>
        ) : (
          <span className="text-ink-muted">None to acknowledge</span>
        ),
    },
  ];
  const table = (
    <ResponsiveTable
      caption="Prescriptions"
      columns={columns}
      rows={rows}
      getRowId={(row) => row.id}
      rowHref={(row) => `/staff/cats/${catId}/prescriptions/${row.id}`}
      isLoading={isLoading}
      isError={isError}
      onRetry={onRetry}
      pager={pager}
      dense
      emptyState={{
        title: scope === 'active' ? 'No active prescriptions' : 'No prescriptions yet',
        description:
          scope === 'active'
            ? 'Switch to All to see past and finished prescriptions.'
            : 'A prescription written from an open visit will appear here.',
      }}
    />
  );
  return (
    <Tabs
      label="Prescriptions"
      idPrefix="cat-rx"
      selected={scope}
      onSelect={(id) => onScopeChange(id as PrescriptionScope)}
      tabs={[
        { id: 'active', label: 'Active', panel: table },
        { id: 'all', label: 'All', panel: table },
      ]}
    />
  );
}
