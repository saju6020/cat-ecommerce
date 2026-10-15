'use client';

import * as React from 'react';
import { Ban, CalendarPlus, CheckCircle2, Clock } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ResponsiveTable, type Column } from '@/components/billing/ResponsiveTable';
import { ActionDialog, ReasonDialog } from '@/components/inventory/ReasonDialog';
import { TextField } from '@/components/inventory/fields';
import type { DietAuthorisation, DietAuthorisationStatus } from '@/lib/api/diet-authorisations';
import { describePharmacyError } from '@/lib/api/errors';
import { formatDay } from '@/lib/format/pharmacy';

/**
 * `CatDietAuthorisationsTable` — HO-PHM-002 §11.5 (US-PHM-08). The food, its dates, the EFFECTIVE status
 * in words (*Active*, *Expired*, *Revoked* — the server decides expiry), and the vet. Vets get **Extend**
 * (a new last day) and **Revoke** (a reason, and a confirmation that says what it does, UI-3). An
 * authorisation is never edited; extending or revoking records a new fact.
 */
export function DietStatusBadge({ status }: { status: DietAuthorisationStatus }) {
  if (status === 'Active')
    return (
      <Badge variant="success" icon={<CheckCircle2 size={12} aria-hidden="true" />}>
        Active
      </Badge>
    );
  if (status === 'Expired')
    return (
      <Badge variant="neutral" icon={<Clock size={12} aria-hidden="true" />}>
        Expired
      </Badge>
    );
  return (
    <Badge variant="danger" icon={<Ban size={12} aria-hidden="true" />}>
      Revoked
    </Badge>
  );
}

export function CatDietAuthorisationsTable({
  catName,
  rows,
  canIssue,
  isLoading,
  isError,
  onRetry,
  onExtend,
  onRevoke,
}: {
  catName: string;
  rows: DietAuthorisation[] | undefined;
  canIssue: boolean;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
  onExtend: (id: string, validTo: string) => Promise<unknown>;
  onRevoke: (id: string, reason: string) => Promise<unknown>;
}) {
  const [extending, setExtending] = React.useState<DietAuthorisation | null>(null);
  const [revoking, setRevoking] = React.useState<DietAuthorisation | null>(null);
  const [newEnd, setNewEnd] = React.useState('');

  const columns: Array<Column<DietAuthorisation>> = [
    { id: 'product', header: 'Food', primary: true, cell: (row) => row.productName },
    {
      id: 'dates',
      header: 'Valid',
      cell: (row) => `${formatDay(row.validFrom)} to ${formatDay(row.validTo)}`,
    },
    { id: 'status', header: 'Status', cell: (row) => <DietStatusBadge status={row.status} /> },
    { id: 'vet', header: 'Authorised by', cell: (row) => row.authorisedBy },
    {
      id: 'notes',
      header: 'Notes',
      cell: (row) =>
        row.notes ? (
          <span className="whitespace-pre-wrap break-words">{row.notes}</span>
        ) : (
          <span className="text-ink-muted">None</span>
        ),
    },
    ...(canIssue
      ? [
          {
            id: 'actions',
            header: 'Actions',
            cell: (row: DietAuthorisation) =>
              row.status === 'Revoked' ? (
                <span className="text-ink-muted">No actions</span>
              ) : (
                <span className="flex flex-wrap gap-2">
                  <Button
                    variant="secondary"
                    size="sm"
                    className="h-auto min-h-8 whitespace-normal py-1 text-left"
                    onClick={() => {
                      setNewEnd('');
                      setExtending(row);
                    }}
                  >
                    <CalendarPlus size={14} aria-hidden="true" />
                    Extend {row.productName}
                  </Button>
                  {row.status === 'Active' && (
                    <Button
                      variant="secondary"
                      size="sm"
                      className="h-auto min-h-8 whitespace-normal py-1 text-left"
                      onClick={() => setRevoking(row)}
                    >
                      Revoke {row.productName}
                    </Button>
                  )}
                </span>
              ),
          },
        ]
      : []),
  ];

  return (
    <>
      <ResponsiveTable
        caption="Diet authorisations"
        columns={columns}
        rows={rows}
        getRowId={(row) => row.id}
        isLoading={isLoading}
        isError={isError}
        onRetry={onRetry}
        emptyState={{
          title: `${catName} has no diet authorisations`,
          description: 'A vet authorises a therapeutic diet from an open visit.',
        }}
      />
      <ActionDialog
        open={extending !== null}
        onOpenChange={(open) => !open && setExtending(null)}
        title={`Extend the authorisation for ${extending?.productName ?? 'this food'}?`}
        consequence={`It currently ends on ${formatDay(extending?.validTo)}. Choose a later last day.`}
        confirmLabel="Extend"
        confirmDisabled={!newEnd || (extending ? newEnd <= extending.validTo : false)}
        describeError={describePharmacyError}
        onConfirm={() => onExtend((extending as DietAuthorisation).id, newEnd)}
      >
        <TextField
          id="da-extend"
          type="date"
          label="New last day"
          required
          value={newEnd}
          onChange={(event) => setNewEnd(event.target.value)}
        />
      </ActionDialog>
      <ReasonDialog
        open={revoking !== null}
        onOpenChange={(open) => !open && setRevoking(null)}
        title={`Revoke the authorisation for ${revoking?.productName ?? 'this food'}?`}
        consequence={`${catName} will no longer be able to buy this food in the shop from today. The authorisation stays on record, marked Revoked.`}
        reasonLabel="Reason for revoking"
        confirmLabel="Revoke"
        variant="destructive"
        describeError={describePharmacyError}
        onConfirm={(reason) => onRevoke((revoking as DietAuthorisation).id, reason)}
      />
    </>
  );
}
