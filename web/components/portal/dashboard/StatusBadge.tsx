import { Check, Clock, TriangleAlert, X } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import type { StatusLabel } from './labels';

/** UI-6: status is never colour alone — every badge carries an icon and a word. */
export function StatusBadge({ status }: { status: StatusLabel }) {
  const Icon =
    status.tone === 'success'
      ? Check
      : status.tone === 'warning'
        ? Clock
        : status.tone === 'danger'
          ? TriangleAlert
          : X;
  return (
    <Badge variant={status.tone} icon={<Icon size={13} aria-hidden="true" />}>
      {status.label}
    </Badge>
  );
}
