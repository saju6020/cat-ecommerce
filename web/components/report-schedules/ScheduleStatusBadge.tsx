import { Ban, Check, CircleAlert, Pause, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';

/** Schedule status as an icon AND a word (UI-6). */
export function ScheduleStatusBadge({ status }: { status: string }) {
  switch (status) {
    case 'Active':
      return (
        <Badge variant="success" icon={<Check size={12} aria-hidden="true" />}>
          Active
        </Badge>
      );
    case 'Paused':
      return (
        <Badge variant="warning" icon={<Pause size={12} aria-hidden="true" />}>
          Paused
        </Badge>
      );
    case 'Cancelled':
      return (
        <Badge variant="neutral" icon={<Ban size={12} aria-hidden="true" />}>
          Cancelled
        </Badge>
      );
    default:
      return <Badge icon={<CircleAlert size={12} aria-hidden="true" />}>{status}</Badge>;
  }
}

/** Run status (Generated / Failed / Purged) as an icon AND a word. */
export function RunStatusBadge({ status }: { status: string }) {
  switch (status) {
    case 'Generated':
      return (
        <Badge variant="success" icon={<Check size={12} aria-hidden="true" />}>
          Generated
        </Badge>
      );
    case 'Failed':
      return (
        <Badge variant="danger" icon={<CircleAlert size={12} aria-hidden="true" />}>
          Failed
        </Badge>
      );
    case 'Purged':
      return (
        <Badge variant="neutral" icon={<Trash2 size={12} aria-hidden="true" />}>
          Purged
        </Badge>
      );
    default:
      return <Badge>{status}</Badge>;
  }
}
