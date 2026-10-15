import { CheckCircle2, CircleDashed, CircleSlash, Clock, PackageCheck, PackageOpen, Truck } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import type { PurchaseOrderReceiptState, PurchaseOrderStatus } from '@/lib/api/procurement-orders';
import type { ConsignmentStatus } from '@/lib/api/procurement-consignments';
import { CONSIGNMENT_STATUS_LABELS, PO_RECEIPT_STATE_LABELS, PO_STATUS_LABELS } from '@/components/inventory/labels';

/** Status and receipt state as two badges, each with an icon AND a word (UI-6: never colour alone). */
export function PurchaseOrderStatusBadge({ status }: { status: PurchaseOrderStatus }) {
  const variant = status === 'Issued' ? 'success' : status === 'Cancelled' ? 'danger' : 'neutral';
  const icon =
    status === 'Issued' ? <CheckCircle2 size={12} aria-hidden="true" /> : status === 'Draft' ? <CircleDashed size={12} aria-hidden="true" /> : <CircleSlash size={12} aria-hidden="true" />;
  return (
    <Badge variant={variant} icon={icon}>
      {PO_STATUS_LABELS[status]}
    </Badge>
  );
}

export function ReceiptStateBadge({ state }: { state: PurchaseOrderReceiptState }) {
  const variant = state === 'FullyReceived' ? 'success' : state === 'PartiallyReceived' ? 'warning' : 'neutral';
  const icon =
    state === 'FullyReceived' ? <PackageCheck size={12} aria-hidden="true" /> : state === 'PartiallyReceived' ? <PackageOpen size={12} aria-hidden="true" /> : <Clock size={12} aria-hidden="true" />;
  return (
    <Badge variant={variant} icon={icon}>
      {PO_RECEIPT_STATE_LABELS[state]}
    </Badge>
  );
}

export function ConsignmentStatusBadge({ status }: { status: ConsignmentStatus }) {
  const variant = status === 'Cleared' ? 'success' : status === 'Cancelled' ? 'danger' : status === 'AtCustoms' ? 'warning' : 'neutral';
  const icon =
    status === 'Cleared' ? <CheckCircle2 size={12} aria-hidden="true" /> : status === 'Cancelled' ? <CircleSlash size={12} aria-hidden="true" /> : <Truck size={12} aria-hidden="true" />;
  return (
    <Badge variant={variant} icon={icon}>
      {CONSIGNMENT_STATUS_LABELS[status]}
    </Badge>
  );
}
