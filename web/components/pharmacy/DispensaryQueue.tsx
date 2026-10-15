'use client';

import { AlertTriangle, ShieldAlert } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { ResponsiveTable, type Column } from '@/components/billing/ResponsiveTable';
import { Panel } from '@/components/inventory/fields';
import type { PharmacyQueue, PharmacyQueueItem, PharmacyQueueRepeat } from '@/lib/api/pharmacy';
import { formatInstantInZone, formatTimeInZone } from '@/lib/format/pharmacy';

/**
 * `DispensaryQueue` — HO-PHM-002 §11.4 (US-PHM-05, 06 AC1). Two sections for the active branch:
 * **Awaiting first supply** (issued prescriptions) and **Repeat requests** (owners asking for another
 * supply). Every row opens the dispense screen. The cat appears in compact form — name and a high-risk
 * flag written as text with an icon (UI-7) — and the controlled badge is a word. The queue polls every
 * 30 s (the page owns the query); *"Updated 10:42"* shows when it was last read, in branch time.
 */
export function CatLine({ name, isHighRisk }: { name: string; isHighRisk: boolean }) {
  return (
    <span className="inline-flex flex-wrap items-center gap-2">
      <b>{name}</b>
      {isHighRisk && (
        <Badge variant="danger" icon={<AlertTriangle size={12} aria-hidden="true" />}>
          High risk
        </Badge>
      )}
    </span>
  );
}

export function DispensaryQueue({
  queue,
  zone,
  isLoading,
  isError,
  onRetry,
}: {
  queue: PharmacyQueue | undefined;
  zone: string;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
}) {
  const awaiting: Array<Column<PharmacyQueueItem>> = [
    {
      id: 'number',
      header: 'Prescription',
      primary: true,
      cell: (row) => row.prescription.prescriptionNumber,
    },
    {
      id: 'cat',
      header: 'Cat',
      cell: (row) => <CatLine name={row.catName} isHighRisk={row.catIsHighRisk} />,
    },
    { id: 'customer', header: 'Owner', cell: (row) => row.customerName },
    {
      id: 'medicine',
      header: 'Medicine',
      cell: (row) => (
        <span className="flex flex-col gap-1">
          <span>
            {row.prescription.medicine.name}{' '}
            <span className="text-xs text-ink-muted" data-clinical-value>
              {row.prescription.medicine.strength}
            </span>
          </span>
          {row.prescription.medicine.isControlled && (
            <Badge variant="warning" icon={<ShieldAlert size={12} aria-hidden="true" />}>
              Controlled
            </Badge>
          )}
        </span>
      ),
    },
    {
      id: 'issued',
      header: 'Issued',
      cell: (row) => formatInstantInZone(row.prescription.issuedOn, zone),
    },
  ];
  const repeats: Array<Column<PharmacyQueueRepeat>> = [
    { id: 'number', header: 'Prescription', primary: true, cell: (row) => row.prescriptionNumber },
    { id: 'cat', header: 'Cat', cell: (row) => <b>{row.catName}</b> },
    { id: 'customer', header: 'Owner', cell: (row) => row.customerName },
    {
      id: 'medicine',
      header: 'Medicine',
      cell: (row) => (
        <span>
          {row.medicine.name}{' '}
          <span className="text-xs text-ink-muted" data-clinical-value>
            {row.medicine.strength}
          </span>
        </span>
      ),
    },
    {
      id: 'requested',
      header: 'Requested',
      cell: (row) => formatInstantInZone(row.requestedOn, zone),
    },
    {
      id: 'note',
      header: 'Owner’s note',
      cell: (row) =>
        row.note ? (
          <span className="whitespace-pre-wrap break-words">{row.note}</span>
        ) : (
          <span className="text-ink-muted">No note</span>
        ),
    },
  ];

  return (
    <div className="flex flex-col gap-4">
      <p className="text-sm text-ink-muted" aria-live="off">
        {queue ? `Updated ${formatTimeInZone(queue.generatedOn, zone)} (${zone})` : ''}
      </p>
      <Panel title="Awaiting first supply" headingId="q-first">
        <ResponsiveTable
          caption="Prescriptions awaiting first supply"
          columns={awaiting}
          rows={queue?.awaitingFirstSupply}
          getRowId={(row) => row.prescription.id}
          rowHref={(row) => `/staff/pharmacy/dispense/${row.prescription.id}`}
          isLoading={isLoading}
          isError={isError}
          onRetry={onRetry}
          dense
          emptyState={{
            title: 'Nothing is waiting',
            description: 'New prescriptions appear here when a vet issues them.',
          }}
        />
      </Panel>
      <Panel title="Repeat requests" headingId="q-repeats">
        <ResponsiveTable
          caption="Repeat requests from owners"
          columns={repeats}
          rows={queue?.repeatRequests}
          getRowId={(row) => row.requestId}
          rowHref={(row) =>
            `/staff/pharmacy/dispense/${row.prescriptionId}?repeatRequestId=${row.requestId}`
          }
          isLoading={isLoading}
          isError={isError}
          onRetry={onRetry}
          dense
          emptyState={{
            title: 'No repeat requests',
            description: 'When an owner asks for a repeat it appears here.',
          }}
        />
      </Panel>
    </div>
  );
}
