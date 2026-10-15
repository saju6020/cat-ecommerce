'use client';

import * as React from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ActionDialog, ReasonDialog } from './ReasonDialog';
import { Banner, DefinitionList, PageHeading, Panel } from './fields';
import { StackedTable } from './StackedTable';
import { ADJUSTMENT_STATUS_LABELS, adjustmentReasonLabel } from './labels';
import { describeInventoryError, hasViolationCode } from '@/lib/api/errors';
import { formatQuantity, formatSignedQuantity, formatUnitCost } from '@/lib/format/inventory';
import { formatMoney } from '@/lib/format/money';
import { formatInZone } from '@/lib/format/time';
import type { AdjustmentDetail as AdjustmentDetailData, AdjustmentLine } from '@/lib/api/inventory-control';

/** The recount message of U-E4: approval failed because the stock is no longer there. */
export function describeApprovalError(error: unknown): string {
  if (hasViolationCode(error, 'InsufficientStock')) {
    return 'Stock has moved since this was requested — recount and raise a new adjustment.';
  }
  return describeInventoryError(error);
}

/**
 * `components/inventory/AdjustmentDetail.tsx` — HO-IMP-002 §11.5, ADR-FE-048. Status, requester,
 * value and lines; **Approve** / **Reject (note)** / **Withdraw** appear only when the server's
 * `allowedActions` lists them. The requester's own refusal (*"Someone else has to approve your
 * request."*) and the stock-has-moved `409` are rendered from the server's answer, inside the
 * dialog, so the decision is visible and never discovered by surprise.
 */
export function AdjustmentDetail({
  adjustment,
  timeZoneId,
  onApprove,
  onReject,
  onWithdraw,
}: {
  adjustment: AdjustmentDetailData;
  timeZoneId: string;
  onApprove: (note: string | null) => Promise<unknown>;
  onReject: (note: string) => Promise<unknown>;
  onWithdraw: () => Promise<unknown>;
}) {
  const [dialog, setDialog] = React.useState<'approve' | 'reject' | 'withdraw' | null>(null);
  const allowed = new Set(adjustment.allowedActions);
  const pending = adjustment.status === 'PendingApproval';
  return (
    <div className="flex flex-col gap-5">
      <PageHeading
        title={adjustment.number}
        description={
          <span className="flex flex-wrap items-center gap-2">
            <Badge variant={pending ? 'warning' : adjustment.status === 'Posted' ? 'success' : 'neutral'}>
              {ADJUSTMENT_STATUS_LABELS[adjustment.status]}
            </Badge>
            <span>{adjustmentReasonLabel(adjustment.reason)} at {adjustment.locationCode ?? 'a location'}</span>
          </span>
        }
        actions={
          <>
            {allowed.has('approve') && <Button onClick={() => setDialog('approve')}>Approve</Button>}
            {allowed.has('reject') && <Button variant="secondary" onClick={() => setDialog('reject')}>Reject…</Button>}
            {allowed.has('withdraw') && <Button variant="secondary" onClick={() => setDialog('withdraw')}>Withdraw request</Button>}
          </>
        }
      />
      {pending && (
        <Banner tone="warning">
          Waiting for approval. It is worth {formatMoney(adjustment.valueBase)}, so a different person with approval rights must decide before any stock changes. Nothing has been written to the ledger yet.
        </Banner>
      )}
      <Panel title="Request">
        <DefinitionList
          items={[
            { label: 'Value', value: formatMoney(adjustment.valueBase) },
            { label: 'Requested', value: formatInZone(adjustment.requestedOn, timeZoneId) },
            { label: 'Note', value: adjustment.note },
            ...(adjustment.decidedOn ? [{ label: 'Decided', value: formatInZone(adjustment.decidedOn, timeZoneId) }] : []),
            ...(adjustment.decisionNote ? [{ label: 'Decision note', value: adjustment.decisionNote }] : []),
          ]}
        />
      </Panel>
      <Panel title="Lines">
        <StackedTable
          caption={`Lines of ${adjustment.number}`}
          rows={adjustment.lines}
          getKey={(l) => l.lineId}
          columns={[
            { id: 'item', header: 'Item', rowHeader: true, cell: (l: AdjustmentLine) => `${l.displayName} (${l.skuCode})` },
            { id: 'lot', header: 'Lot', cell: (l) => l.lotNumber },
            { id: 'qty', header: 'Change', numeric: true, cell: (l) => <strong>{formatSignedQuantity(l.quantityDelta, l.stockUnit)}</strong> },
            { id: 'cost', header: 'Unit cost', numeric: true, cell: (l) => formatUnitCost(l.unitCostBase) },
          ]}
        />
        <p className="mt-2 text-xs text-ink-muted">
          {adjustment.lines.map((l) => `${l.displayName}: ${formatQuantity(l.quantityDelta.replace('-', ''), l.stockUnit)} ${l.quantityDelta.startsWith('-') ? 'removed' : 'added'}`).join(' · ')}
        </p>
      </Panel>

      <ActionDialog
        open={dialog === 'approve'}
        onOpenChange={(open) => setDialog(open ? 'approve' : null)}
        title={`Approve ${adjustment.number}`}
        consequence={`This posts the adjustment, worth ${formatMoney(adjustment.valueBase)}, to the stock ledger. It cannot be undone — a correction is a new adjustment.`}
        confirmLabel="Approve and post"
        describeError={describeApprovalError}
        onConfirm={() => onApprove(null)}
      />
      <ReasonDialog
        open={dialog === 'reject'}
        onOpenChange={(open) => setDialog(open ? 'reject' : null)}
        title={`Reject ${adjustment.number}`}
        consequence="The request is closed and no stock changes. The person who asked sees your note."
        reasonLabel="Note for the requester"
        confirmLabel="Reject request"
        variant="destructive"
        onConfirm={onReject}
      />
      <ActionDialog
        open={dialog === 'withdraw'}
        onOpenChange={(open) => setDialog(open ? 'withdraw' : null)}
        title={`Withdraw ${adjustment.number}`}
        consequence="You are taking back your request. No stock changes."
        confirmLabel="Withdraw request"
        onConfirm={onWithdraw}
      />
    </div>
  );
}
