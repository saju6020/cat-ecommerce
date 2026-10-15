'use client';

import Link from 'next/link';
import { useState } from 'react';
import { PackageOpen } from 'lucide-react';
import { PageHeader, Section } from '@/components/billing/PageHeader';
import { QueryBoundary } from '@/components/billing/QueryBoundary';
import { RefundDialog } from '@/components/refunds/RefundDialog';
import { Badge } from '@/components/ui/badge';
import { Button, buttonVariants } from '@/components/ui/button';
import type { StaffReturnDetail } from '@/lib/api/staff-returns';
import {
  formatOrderWhen,
  itemConditionLabel,
  returnKindLabel,
  returnReasonLabel,
  returnStatusLabel,
} from '@/lib/format/orders';
import { useStaffReturn } from '@/lib/hooks/useStaffReturns';
import { ReceiveReturnForm } from './ReceiveReturnForm';
import { ReturnDecisionPanel } from './ReturnDecisionPanel';

/**
 * `components/returns/ReturnDetail.tsx` — ⭐ HO-ORD-002 §11.10, US-ORD-13, US-ORD-14. One return: the
 * order it belongs to, each line with the customer's reason, the item's condition, their note, the
 * batch numbers and whether a batch was recalled or a defect confirmed. By status: *Requested* →
 * decision panel; *Approved* → receive form; *Received* → the goods are in quarantine (Inventory's
 * screen restocks or writes them off — there is **no** restock / write-off control here) and a refund
 * can be issued. A refused parcel (*Undelivered parcel*) arrives already approved.
 */
function Detail({ ret }: { ret: StaffReturnDetail }) {
  const [refundOpen, setRefundOpen] = useState(false);
  const zone = ret.branchTimeZoneId;
  return (
    <div className="flex flex-col gap-5">
      <PageHeader
        back={{ href: '/staff/returns', label: 'Returns' }}
        title={`Return for order ${ret.orderNumber}`}
        description={`${returnKindLabel(ret.kind)} · requested ${formatOrderWhen(ret.requestedOn, zone, true)} · ${ret.customerName}`}
        actions={
          <>
            <Badge variant={ret.status === 'Requested' ? 'warning' : 'neutral'}>{returnStatusLabel(ret.status)}</Badge>
            <Link href={`/staff/orders/${ret.orderId}`} className={buttonVariants({ variant: 'secondary', size: 'sm' })}>
              Open the order
            </Link>
          </>
        }
      />
      <Section title="Items">
        <ul aria-label="Items in this return" className="m-0 flex list-none flex-col p-0">
          {ret.lines.map((line) => (
            <li key={line.orderLineId} className="flex flex-col gap-1 border-b border-border py-3 text-sm last:border-0">
              <p className="font-semibold text-ink [overflow-wrap:anywhere]">
                {`${line.productName} ${line.variantLabel}`.trim()} × {line.quantity}
              </p>
              <p className="text-ink-muted">
                {returnReasonLabel(line.reason)} · {itemConditionLabel(line.condition)}
                {line.batchNumbers.length > 0 ? ` · batch ${line.batchNumbers.join(', ')}` : ''}
              </p>
              {line.customerNote && <p className="[overflow-wrap:anywhere]">Customer’s note: “{line.customerNote}”</p>}
              <p className="flex flex-wrap gap-1">
                {line.anyBatchRecalled && <Badge variant="danger">Batch recalled</Badge>}
                {line.defectConfirmed && <Badge variant="warning">Defect confirmed{line.defectNote ? `: ${line.defectNote}` : ''}</Badge>}
                {line.requiresDefectConfirmation && !line.defectConfirmed && ret.status === 'Requested' && (
                  <Badge variant="warning">Needs a defect confirmation</Badge>
                )}
                {line.quantityReceived > 0 && <Badge variant="neutral">{line.quantityReceived} received</Badge>}
              </p>
            </li>
          ))}
        </ul>
        {ret.decisionNote && <p className="text-sm text-ink-muted [overflow-wrap:anywhere]">Decision note: “{ret.decisionNote}”</p>}
      </Section>
      {ret.status === 'Requested' && <ReturnDecisionPanel ret={ret} />}
      {ret.status === 'Approved' && <ReceiveReturnForm ret={ret} />}
      {ret.status === 'Received' && (
        <Section title="Received into quarantine">
          <p className="flex items-start gap-2 text-sm">
            <PackageOpen size={16} aria-hidden="true" className="mt-0.5 flex-none" />
            <span>
              The goods are in the branch’s quarantine location
              {ret.receivedOn ? `, received ${formatOrderWhen(ret.receivedOn, zone)}` : ''}. Restocking or writing them off is done on the{' '}
              <Link href="/staff/inventory/batches" className="font-semibold text-accent-ink hover:underline">Inventory batches screen</Link>.
            </span>
          </p>
          <div>
            <Button onClick={() => setRefundOpen(true)}>Issue refund</Button>
          </div>
          <RefundDialog orderId={ret.orderId} orderNumber={ret.orderNumber} open={refundOpen} onOpenChange={setRefundOpen} initialReturnId={ret.id} />
        </Section>
      )}
    </div>
  );
}

export function ReturnDetailScreen({ returnRequestId }: { returnRequestId: string }) {
  const query = useStaffReturn(returnRequestId);
  return (
    <QueryBoundary query={query} notFoundTitle="Return not found" loadingLabel="Loading the return…">
      {(ret) => <Detail ret={ret} />}
    </QueryBoundary>
  );
}
