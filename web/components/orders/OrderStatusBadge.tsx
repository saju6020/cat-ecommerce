import {
  Ban,
  CircleCheck,
  Clock,
  Package,
  PackageCheck,
  RotateCcw,
  Truck,
  Undo2,
  type LucideIcon,
} from 'lucide-react';
import { Badge, type BadgeProps } from '@/components/ui/badge';
import { orderFlagLabel, orderStatusLabel } from '@/lib/format/orders';

/**
 * `components/orders/OrderStatusBadge.tsx` — HO-ORD-002 §8.5, UI-6. Status is **text first**, with an
 * `aria-hidden` icon and colour as extras: *Waiting for payment*, *Being packed*, *Dispatched*…
 */
interface Spec {
  variant: NonNullable<BadgeProps['variant']>;
  icon: LucideIcon;
}

const SPECS: Record<string, Spec> = {
  PendingPayment: { variant: 'warning', icon: Clock },
  Confirmed: { variant: 'success', icon: CircleCheck },
  Packing: { variant: 'neutral', icon: Package },
  Dispatched: { variant: 'neutral', icon: Truck },
  Delivered: { variant: 'success', icon: PackageCheck },
  Cancelled: { variant: 'neutral', icon: Ban },
  Returned: { variant: 'warning', icon: RotateCcw },
  Refunded: { variant: 'neutral', icon: Undo2 },
};

export function OrderStatusBadge({ status }: { status: string }) {
  const spec = SPECS[status] ?? { variant: 'neutral' as const, icon: Clock };
  const Icon = spec.icon;
  return (
    <Badge variant={spec.variant} icon={<Icon size={12} aria-hidden="true" />}>
      {orderStatusLabel(status)}
    </Badge>
  );
}

const FLAG_VARIANTS: Record<string, NonNullable<BadgeProps['variant']>> = {
  AwaitingApproval: 'warning',
  TransferReported: 'success',
  CourierReportsDelivered: 'warning',
  CollectionOverdue: 'danger',
  CustomerAnonymised: 'neutral',
};

/** A queue-row flag as a text chip (never colour alone). */
export function OrderFlagBadge({ flag }: { flag: string }) {
  return (
    <Badge variant={FLAG_VARIANTS[flag] ?? 'neutral'} className="whitespace-normal">
      {orderFlagLabel(flag)}
    </Badge>
  );
}
