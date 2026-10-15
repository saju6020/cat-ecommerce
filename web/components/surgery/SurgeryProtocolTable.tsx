'use client';

import { useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ResponsiveTable, type Column } from '@/components/billing/ResponsiveTable';
import { followUpLabel } from '@/lib/format/surgery';
import type { SurgeryProtocol } from '@/lib/api/surgery-protocols';
import { SurgeryProtocolForm } from './SurgeryProtocolForm';

/**
 * `SurgeryProtocolTable` — HO-SRG-002 §11.7, US-SRG-07. One row per surgical service, *"Using defaults"*
 * where none has been saved. Vets and administrators (the server's `Surgery.ManageProtocols`) can edit;
 * everyone else reads.
 */
export function SurgeryProtocolTable({
  protocols,
  canEdit,
  isLoading,
  isError,
  onRetry,
}: {
  protocols: SurgeryProtocol[] | undefined;
  canEdit: boolean;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
}) {
  const [editing, setEditing] = useState<SurgeryProtocol | null>(null);
  const columns: Array<Column<SurgeryProtocol>> = [
    {
      id: 'service',
      header: 'Procedure',
      primary: true,
      cell: (p) => <strong>{p.serviceName}</strong>,
    },
    {
      id: 'status',
      header: 'Protocol',
      cell: (p) =>
        p.isDefault ? <Badge>Using defaults</Badge> : <Badge variant="success">Customised</Badge>,
    },
    { id: 'fasting', header: 'Fasting', cell: (p) => `${p.fastingHours} hours` },
    {
      id: 'followups',
      header: 'Follow-ups',
      cell: (p) =>
        p.followUps.length === 0
          ? 'None'
          : p.followUps.map((f) => `${followUpLabel(f.kind)} (day ${f.dayOffset})`).join(', '),
    },
    {
      id: 'actions',
      header: 'Actions',
      cell: (p) =>
        canEdit ? (
          <Button
            variant="secondary"
            size="sm"
            aria-label={`Edit ${p.serviceName}`}
            onClick={() => setEditing(p)}
          >
            Edit
          </Button>
        ) : null,
    },
  ];
  return (
    <>
      <ResponsiveTable
        caption="Surgery protocols"
        columns={columns}
        rows={protocols}
        getRowId={(p) => p.serviceId}
        isLoading={isLoading}
        isError={isError}
        onRetry={onRetry}
        emptyState={{ title: 'No surgical services yet.' }}
      />
      {editing && <SurgeryProtocolForm protocol={editing} onClose={() => setEditing(null)} />}
    </>
  );
}
