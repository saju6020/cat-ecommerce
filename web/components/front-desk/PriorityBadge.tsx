import { AlertOctagon, AlertTriangle, Minus } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import type { TriagePriority } from '@/lib/api/types';

/** Triage priority as text plus an icon plus colour — never colour alone (HO-APT-002 UI-6). */
export const PRIORITY_OPTIONS: Array<{ value: TriagePriority; label: string; hint: string }> = [
  { value: 'Routine', label: 'Routine', hint: 'Seen in turn.' },
  { value: 'Urgent', label: 'Urgent', hint: 'Seen before routine cases.' },
  { value: 'Emergency', label: 'Emergency', hint: 'Seen first, straight away.' },
];

export function PriorityBadge({ priority }: { priority: TriagePriority }) {
  if (priority === 'Emergency') {
    return (
      <Badge variant="danger" icon={<AlertOctagon size={13} aria-hidden="true" />}>
        Emergency
      </Badge>
    );
  }
  if (priority === 'Urgent') {
    return (
      <Badge variant="warning" icon={<AlertTriangle size={13} aria-hidden="true" />}>
        Urgent
      </Badge>
    );
  }
  return (
    <Badge variant="neutral" icon={<Minus size={13} aria-hidden="true" />}>
      Routine
    </Badge>
  );
}
