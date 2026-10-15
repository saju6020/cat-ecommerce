'use client';

import * as React from 'react';
import Link from 'next/link';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ActionDialog, ReasonDialog } from '@/components/inventory/ReasonDialog';
import { Banner, DefinitionList, PageHeading, Panel } from '@/components/inventory/fields';
import { StackedTable } from '@/components/inventory/StackedTable';
import { formatQuantity } from '@/lib/format/inventory';
import { formatInZone } from '@/lib/format/time';
import type { RecallDetail, RecallNotificationRun, RecallTrace } from '@/lib/api/inventory-recalls';
import { RecallTraceTable, countTraceCustomers } from './RecallTraceTable';

/**
 * `components/recall/RecallConsole.tsx` — HO-IMP-002 §11.7, US-IMP-11, ADR-FE-046. The recall as
 * three numbered steps, none automatic: **1 Stock** (what is held, where), **2 Trace** (who had it —
 * read it, export it) and **3 Notify** (an explicit button, with the number of people, and a result).
 * Declaring never messages anyone. Withdrawing sends the batch back to quarantine, not on sale;
 * closing needs the held stock dealt with first (the server says how much).
 */
export function RecallConsole({
  detail,
  trace,
  stockUnit,
  csvUrl,
  timeZoneId,
  onNotify,
  onWithdraw,
  onClose,
}: {
  detail: RecallDetail;
  trace: RecallTrace;
  stockUnit: string;
  csvUrl: string;
  timeZoneId: string;
  onNotify: () => Promise<RecallNotificationRun>;
  onWithdraw: (reason: string) => Promise<unknown>;
  onClose: (note: string) => Promise<unknown>;
}) {
  const { summary, recall, held } = detail;
  const active = summary.status === 'Active';
  const [dialog, setDialog] = React.useState<'notify' | 'withdraw' | 'close' | null>(null);
  const [lastRun, setLastRun] = React.useState<RecallNotificationRun | null>(null);
  // How many customers the trace names (a headcount); the server decides who is actually messaged.
  const named = countTraceCustomers(trace);
  const notYet = Math.max(0, named - recall.notifiedRecipientCount);
  const declaredDay = recall.declaredOn.slice(0, 10);

  return (
    <div className="flex flex-col gap-5">
      <PageHeading
        title={`Recall ${summary.recallNumber}`}
        description={
          <span className="flex flex-wrap items-center gap-2">
            <Badge variant={active ? 'danger' : 'neutral'}>{summary.status === 'Active' ? 'Active' : summary.status === 'Closed' ? 'Closed' : 'Withdrawn'}</Badge>
            <span>
              {summary.displayName} — lot {summary.lotNumber}
            </span>
            <Link href={`/staff/inventory/batches/${summary.batchId}`} className="text-accent-ink">
              Open the batch
            </Link>
          </span>
        }
        actions={
          active && (
            <>
              <Button variant="secondary" onClick={() => setDialog('withdraw')}>Withdraw the recall…</Button>
              <Button variant="secondary" onClick={() => setDialog('close')}>Close the recall…</Button>
            </>
          )
        }
      />
      <Panel title="About this recall">
        <DefinitionList
          items={[
            { label: 'Declared', value: formatInZone(recall.declaredOn, timeZoneId) },
            { label: 'Reason', value: recall.reason },
            { label: 'Advice sent to customers', value: recall.customerAdvice },
            ...(recall.closingNote ? [{ label: summary.status === 'Withdrawn' ? 'Withdrawn' : 'Closed', value: recall.closingNote }] : []),
          ]}
        />
      </Panel>

      <Panel title="1 · Stock" headingId="recall-step-1">
        {held.length === 0 ? (
          <p className="text-sm text-ink-muted">No stock of this lot is held anywhere.</p>
        ) : (
          <StackedTable
            caption="Recalled stock held, by location"
            rows={held}
            getKey={(h) => h.locationId}
            columns={[
              { id: 'loc', header: 'Location', rowHeader: true, cell: (h) => h.locationCode ?? '—' },
              { id: 'qty', header: 'Held', numeric: true, cell: (h) => formatQuantity(h.onHand, stockUnit) },
            ]}
          />
        )}
        <p className="mt-2 text-sm">
          <Link href={`/staff/inventory/batches/${summary.batchId}`} className="font-semibold text-accent-ink">
            Write off or return this stock
          </Link>{' '}
          <span className="text-ink-muted">— held stock cannot be sold, dispensed or fed, and must be dealt with before the recall can be closed.</span>
        </p>
      </Panel>

      <Panel title="2 · Trace" headingId="recall-step-2">
        <RecallTraceTable trace={trace} stockUnit={stockUnit} csvUrl={csvUrl} timeZoneId={timeZoneId} />
      </Panel>

      <Panel title="3 · Notify" headingId="recall-step-3">
        <div className="flex flex-col gap-3 text-sm">
          <p>
            <strong>{notYet}</strong> {notYet === 1 ? 'customer has' : 'customers have'} not yet been messaged ({named} named in the trace; {recall.notifiedRecipientCount} messaged so far in {recall.notificationRuns} {recall.notificationRuns === 1 ? 'run' : 'runs'}).
          </p>
          <p className="text-ink-muted">Declaring a recall messages no one. Messages go out only when you choose Notify everyone, and every message is logged.</p>
          <div>
            <Button onClick={() => setDialog('notify')} disabled={!active}>Notify everyone…</Button>
          </div>
          {lastRun && (
            <Banner tone="info">
              {lastRun.requested} messaged, {lastRun.alreadyNotified} previously, {lastRun.remaining} left{lastRun.remaining > 0 ? ' — run again' : ''}. Every message is logged.
            </Banner>
          )}
          <Link href={`/staff/notifications?from=${declaredDay}`} className="font-semibold text-accent-ink">
            Open the notification log (from the day the recall was declared)
          </Link>
        </div>
      </Panel>

      <ActionDialog
        open={dialog === 'notify'}
        onOpenChange={(open) => setDialog(open ? 'notify' : null)}
        title="Notify the affected customers?"
        consequence={`${notYet} ${notYet === 1 ? 'customer' : 'customers'} will be messaged with the recall advice. Messages cannot be recalled once sent; running it again only reaches people not yet messaged.`}
        confirmLabel={`Notify ${notYet} ${notYet === 1 ? 'customer' : 'customers'}`}
        pendingLabel="Notifying…"
        onConfirm={async () => setLastRun(await onNotify())}
      />
      <ReasonDialog
        open={dialog === 'withdraw'}
        onOpenChange={(open) => setDialog(open ? 'withdraw' : null)}
        title={`Withdraw recall ${summary.recallNumber}`}
        consequence="The batch goes back to quarantine, not on sale. Messages not yet sent will not be sent."
        reasonLabel="Why is the recall being withdrawn?"
        confirmLabel="Withdraw the recall"
        variant="destructive"
        onConfirm={onWithdraw}
      />
      <ReasonDialog
        open={dialog === 'close'}
        onOpenChange={(open) => setDialog(open ? 'close' : null)}
        title={`Close recall ${summary.recallNumber}`}
        consequence="Record that the recall is complete. This is refused while any recalled stock is still held — write it off or return it first."
        reasonLabel="Closing note"
        confirmLabel="Close the recall"
        onConfirm={onClose}
      />
    </div>
  );
}
