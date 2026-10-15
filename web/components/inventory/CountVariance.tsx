'use client';

import * as React from 'react';
import { Button } from '@/components/ui/button';
import { ActionDialog } from './ReasonDialog';
import { Banner, Panel } from './fields';
import { StackedTable } from './StackedTable';
import { COUNT_STATUS_LABELS } from './labels';
import { describeInventoryError } from '@/lib/api/errors';
import { formatQuantity, formatSignedQuantity } from '@/lib/format/inventory';
import { formatMoney } from '@/lib/format/money';
import type { CountStatus, CountVariance as CountVarianceData, CountVarianceLine } from '@/lib/api/inventory-control';

/**
 * `components/inventory/CountVariance.tsx` — HO-IMP-002 §11.5, US-IMP-14 AC1. After submitting: the
 * book quantity, the counted quantity, the variance and its value for every line, with the totals the
 * server worked out — and, until it is committed, the sentence **"Nothing has been written to stock
 * yet."** Commit is confirmed, naming the number of variances and their value, and ends as Committed
 * or Awaiting approval (explained). Approve / Reject appear only when the server allows them.
 */
export function CountVariance({
  variance,
  status,
  allowedActions,
  onCommit,
  onApprove,
  onReject,
}: {
  variance: CountVarianceData;
  status: CountStatus;
  allowedActions: string[];
  onCommit: () => Promise<{ status: CountStatus }>;
  onApprove: () => Promise<unknown>;
  onReject: () => Promise<unknown>;
}) {
  const [dialog, setDialog] = React.useState<'commit' | 'approve' | 'reject' | null>(null);
  const [outcome, setOutcome] = React.useState<CountStatus | null>(null);
  const allowed = new Set(allowedActions);
  const written = status === 'Committed';
  const total = formatMoney(variance.totalVarianceValueBase);
  const n = variance.varianceLineCount;

  return (
    <div className="flex flex-col gap-5">
      {!written && status !== 'Cancelled' && <Banner tone="info">Nothing has been written to stock yet.</Banner>}
      {written && <Banner tone="info">This count was committed: exactly these variances were written to the ledger.</Banner>}
      {outcome === 'AwaitingApproval' && (
        <Banner tone="warning">
          The variances are worth {total}, which is above the approval limit, so a different person with approval rights must approve before anything is written to stock.
        </Banner>
      )}
      {status === 'AwaitingApproval' && outcome !== 'AwaitingApproval' && (
        <Banner tone="warning">
          Waiting for approval. The variances are above the approval limit, so someone other than the person who counted must approve before anything is written to stock.
        </Banner>
      )}
      <Panel
        title="Variance report"
        actions={
          <span className="flex flex-wrap gap-2">
            {allowed.has('commit') && <Button onClick={() => setDialog('commit')}>Commit the count…</Button>}
            {allowed.has('approve') && <Button onClick={() => setDialog('approve')}>Approve</Button>}
            {allowed.has('reject') && <Button variant="secondary" onClick={() => setDialog('reject')}>Reject</Button>}
          </span>
        }
      >
        <p className="mb-3 text-sm text-ink">
          Status: <strong>{COUNT_STATUS_LABELS[status]}</strong> · {n} {n === 1 ? 'variance' : 'variances'} · total {total}
          {variance.willNeedApproval && ' · will need approval'}
        </p>
        {variance.lines.length === 0 ? (
          <p className="text-sm text-ink-muted">The count matches the books exactly — there are no variances.</p>
        ) : (
          <StackedTable
            caption="Count variances"
            rows={variance.lines}
            getKey={(l) => `${l.skuId}-${l.batchId}`}
            columns={[
              { id: 'item', header: 'Item', rowHeader: true, cell: (l: CountVarianceLine) => `${l.displayName} (${l.skuCode}) · lot ${l.lotNumber}` },
              { id: 'book', header: 'Book', numeric: true, cell: (l) => formatQuantity(l.bookQuantity, l.stockUnit) },
              { id: 'counted', header: 'Counted', numeric: true, cell: (l) => formatQuantity(l.countedQuantity, l.stockUnit) },
              { id: 'variance', header: 'Variance', numeric: true, cell: (l) => <strong>{formatSignedQuantity(l.variance, l.stockUnit)}</strong> },
              { id: 'value', header: 'Value', numeric: true, cell: (l) => formatMoney(l.value) },
            ]}
            footer={[{ label: 'Total variance value (from the server)', value: total }]}
          />
        )}
      </Panel>

      <ActionDialog
        open={dialog === 'commit'}
        onOpenChange={(open) => setDialog(open ? 'commit' : null)}
        title="Commit this count?"
        consequence={`${n} ${n === 1 ? 'variance' : 'variances'}, ${total}. Committing writes exactly these variances to the stock ledger${variance.willNeedApproval ? ', after a different person approves them' : ''}. It cannot be undone — a correction is a new adjustment.`}
        confirmLabel="Commit the count"
        pendingLabel="Committing…"
        describeError={describeInventoryError}
        onConfirm={async () => setOutcome((await onCommit()).status)}
      />
      <ActionDialog
        open={dialog === 'approve'}
        onOpenChange={(open) => setDialog(open ? 'approve' : null)}
        title="Approve this count?"
        consequence={`This writes the ${n} ${n === 1 ? 'variance' : 'variances'}, ${total}, to the stock ledger. It cannot be undone.`}
        confirmLabel="Approve and write to stock"
        onConfirm={onApprove}
      />
      <ActionDialog
        open={dialog === 'reject'}
        onOpenChange={(open) => setDialog(open ? 'reject' : null)}
        title="Reject this count?"
        consequence="The count is closed and nothing is written to stock. A new count can be opened."
        confirmLabel="Reject the count"
        variant="destructive"
        onConfirm={onReject}
      />
    </div>
  );
}
