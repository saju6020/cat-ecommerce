import { CheckCircle2, AlertTriangle } from 'lucide-react';
import { Badge } from '@/components/ui/badge';

/**
 * `components/customer/CustomerStatusBadge.tsx` — ⭐ US-ACC-10, UI-6. `Incomplete` shows text and
 * an icon, never colour alone.
 */
export function CustomerStatusBadge({ recordStatus }: { recordStatus: 'Complete' | 'Incomplete' }) {
  if (recordStatus === 'Incomplete') {
    return (
      <Badge variant="warning" icon={<AlertTriangle size={12} aria-hidden="true" />}>
        Incomplete
      </Badge>
    );
  }
  return (
    <Badge variant="success" icon={<CheckCircle2 size={12} aria-hidden="true" />}>
      Complete
    </Badge>
  );
}
