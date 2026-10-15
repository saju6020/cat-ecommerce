'use client';

import { Ban, CircleCheck } from 'lucide-react';
import { DataTable, type DataTableColumn } from '@/components/common/DataTable';
import { Badge } from '@/components/ui/badge';
import { consentKindLabel, consentMethodLabel, consentStatusLabel } from '@/lib/format/clinical';
import { formatInZone } from '@/lib/format/time';
import type { ConsentListItem } from '@/lib/api/consents';

/**
 * `ConsentList` — HO-EMR-002 §11.6 (US-CLN-21). A cat's consents: kind (links to the detail), how it was
 * signed, the date (branch zone), the signer, the form version and the status AS TEXT with an icon —
 * never colour alone (UI-6).
 */
export function ConsentList({
  rows,
  zone,
  isLoading,
  isError,
  onRetry,
}: {
  rows: ConsentListItem[] | undefined;
  zone: string;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
}) {
  const columns: Array<DataTableColumn<ConsentListItem>> = [
    { id: 'kind', header: 'Consent', cell: (row) => consentKindLabel(row.kind) },
    { id: 'method', header: 'Signed', cell: (row) => consentMethodLabel(row.method) },
    { id: 'date', header: 'Date', cell: (row) => formatInZone(row.signedOn, zone) },
    { id: 'signer', header: 'Signed by', cell: (row) => row.signedByName },
    { id: 'version', header: 'Form version', cell: (row) => `Version ${row.policyVersionNumber}` },
    {
      id: 'status',
      header: 'Status',
      cell: (row) =>
        row.status === 'Withdrawn' ? (
          <Badge variant="neutral" icon={<Ban size={12} aria-hidden="true" />}>
            {consentStatusLabel(row.status)}
          </Badge>
        ) : (
          <Badge variant="success" icon={<CircleCheck size={12} aria-hidden="true" />}>
            {consentStatusLabel(row.status)}
          </Badge>
        ),
    },
  ];
  return (
    <DataTable
      caption="Consents for this cat"
      columns={columns}
      rows={rows}
      getRowId={(row) => row.id}
      rowHref={(row) => `/staff/consents/${row.id}`}
      isLoading={isLoading}
      isError={isError}
      onRetry={onRetry}
      errorTitle="We couldn’t load the consents just now."
      emptyState={{
        title: 'No consents recorded for this cat.',
        description:
          'Consent for surgery, anaesthesia or euthanasia is recorded from the procedure’s appointment.',
      }}
    />
  );
}
