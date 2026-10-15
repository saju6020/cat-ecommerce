import {
  Ban,
  CircleCheck,
  CircleDashed,
  Clock,
  FileText,
  RotateCcw,
  ShieldX,
  Undo2,
  type LucideIcon,
} from 'lucide-react';
import { Badge, type BadgeProps } from '@/components/ui/badge';

/**
 * `components/billing/StatusPill.tsx` — HO-BIL-002 §7 UI-6. Status is **text plus colour** (and a
 * glyph): *Part-paid*, *Pending approval*, *Reversed*… The word is what the server's status means in
 * plain language; colour alone never carries it.
 */
interface PillSpec {
  label: string;
  variant: NonNullable<BadgeProps['variant']>;
  icon: LucideIcon;
}

const INVOICE_STATUSES: Record<string, PillSpec> = {
  Draft: { label: 'Draft', variant: 'neutral', icon: CircleDashed },
  Issued: { label: 'Issued', variant: 'warning', icon: FileText },
  PartPaid: { label: 'Part-paid', variant: 'warning', icon: Clock },
  Paid: { label: 'Paid', variant: 'success', icon: CircleCheck },
  Credited: { label: 'Credited', variant: 'neutral', icon: Undo2 },
  Discarded: { label: 'Discarded', variant: 'neutral', icon: Ban },
};

const PAYMENT_STATUSES: Record<string, PillSpec> = {
  PendingApproval: { label: 'Pending approval', variant: 'warning', icon: Clock },
  Accepted: { label: 'Accepted', variant: 'success', icon: CircleCheck },
  Rejected: { label: 'Rejected', variant: 'danger', icon: ShieldX },
  Reversed: { label: 'Reversed', variant: 'danger', icon: RotateCcw },
};

function Pill({ spec, fallback }: { spec: PillSpec | undefined; fallback: string }) {
  const resolved = spec ?? { label: fallback, variant: 'neutral' as const, icon: CircleDashed };
  const Icon = resolved.icon;
  return (
    <Badge className="max-w-full whitespace-normal" variant={resolved.variant} icon={<Icon size={12} aria-hidden="true" />}>
      {resolved.label}
    </Badge>
  );
}

export function InvoiceStatusPill({ status }: { status: string }) {
  return <Pill spec={INVOICE_STATUSES[status]} fallback={status} />;
}

export function PaymentStatusPill({ status }: { status: string }) {
  return <Pill spec={PAYMENT_STATUSES[status]} fallback={status} />;
}

export function invoiceStatusLabel(status: string): string {
  return INVOICE_STATUSES[status]?.label ?? status;
}

export function paymentStatusLabel(status: string): string {
  return PAYMENT_STATUSES[status]?.label ?? status;
}
