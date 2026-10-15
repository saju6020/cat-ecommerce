import { Ban, CheckCircle2, Clock, Hourglass, Pill } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import type { PrescriptionStatus } from '@/lib/api/prescriptions';

/**
 * `PrescriptionStatusBadge` — the EFFECTIVE status as a word with an icon (UI-6: never colour alone).
 * `Expired` is the server's call (validity passed with supplies left, ADR-142) — the client never
 * compares dates to decide it.
 */
const LABELS: Record<PrescriptionStatus, string> = {
  Issued: 'Awaiting first supply',
  Dispensed: 'Dispensed',
  Completed: 'Completed',
  Expired: 'Expired',
  Cancelled: 'Cancelled',
};

export function prescriptionStatusLabel(status: string): string {
  return LABELS[status as PrescriptionStatus] ?? status;
}

export function PrescriptionStatusBadge({ status }: { status: PrescriptionStatus }) {
  const label = prescriptionStatusLabel(status);
  switch (status) {
    case 'Issued':
      return (
        <Badge
          className="whitespace-normal text-left"
          variant="warning"
          icon={<Hourglass size={12} aria-hidden="true" />}
        >
          {label}
        </Badge>
      );
    case 'Dispensed':
      return (
        <Badge
          className="whitespace-normal text-left"
          variant="success"
          icon={<Pill size={12} aria-hidden="true" />}
        >
          {label}
        </Badge>
      );
    case 'Completed':
      return (
        <Badge
          className="whitespace-normal text-left"
          variant="neutral"
          icon={<CheckCircle2 size={12} aria-hidden="true" />}
        >
          {label}
        </Badge>
      );
    case 'Expired':
      return (
        <Badge
          className="whitespace-normal text-left"
          variant="neutral"
          icon={<Clock size={12} aria-hidden="true" />}
        >
          {label}
        </Badge>
      );
    default:
      return (
        <Badge
          className="whitespace-normal text-left"
          variant="danger"
          icon={<Ban size={12} aria-hidden="true" />}
        >
          {label}
        </Badge>
      );
  }
}
