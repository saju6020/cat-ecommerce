import { CheckCircle2, CircleDashed, Clock3, HeartPulse, Scissors, XCircle } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { surgeryStatusLabel } from '@/lib/format/surgery';

const ICON = { size: 12, 'aria-hidden': true } as const;

/** Status as text AND an icon (UI-6) — never colour alone. */
export function SurgeryStatusBadge({ status }: { status: string }) {
  const label = surgeryStatusLabel(status);
  switch (status) {
    case 'Scheduled':
      return <Badge icon={<Clock3 {...ICON} />}>{label}</Badge>;
    case 'PreOp':
      return (
        <Badge variant="warning" icon={<CircleDashed {...ICON} />}>
          {label}
        </Badge>
      );
    case 'InTheatre':
      return (
        <Badge variant="success" icon={<Scissors {...ICON} />}>
          {label}
        </Badge>
      );
    case 'Recovery':
    case 'InCare':
      return (
        <Badge variant="warning" icon={<HeartPulse {...ICON} />}>
          {label}
        </Badge>
      );
    case 'Discharged':
      return (
        <Badge variant="success" icon={<CheckCircle2 {...ICON} />}>
          {label}
        </Badge>
      );
    case 'Cancelled':
      return (
        <Badge variant="danger" icon={<XCircle {...ICON} />}>
          {label}
        </Badge>
      );
    default:
      return <Badge>{label}</Badge>;
  }
}
