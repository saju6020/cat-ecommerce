import { CheckCircle2, Clock, OctagonAlert } from 'lucide-react';
import { Badge, type BadgeProps } from '@/components/ui/badge';
import type { VaccinationStatus } from '@/lib/api/cats';

/**
 * `components/cat/VaccinationStatusBadge.tsx` — US-CAT-07 (HO-CAT-002 §11.5). Icon and label,
 * never colour alone (UI-6). The label text always matches HO-CAT-001's `overallStatus`/
 * `status` value — never recomputed or reworded client-side (§11.5's "never recompute status in
 * the browser").
 */
const STATUS_CONFIG: Record<
  VaccinationStatus,
  { label: string; icon: React.ReactNode; variant: BadgeProps['variant'] }
> = {
  Current: {
    label: 'Current',
    icon: <CheckCircle2 size={12} aria-hidden="true" />,
    variant: 'success',
  },
  DueSoon: { label: 'Due soon', icon: <Clock size={12} aria-hidden="true" />, variant: 'warning' },
  Overdue: {
    label: 'Overdue',
    icon: <OctagonAlert size={12} aria-hidden="true" />,
    variant: 'danger',
  },
};

export interface VaccinationStatusBadgeProps {
  status: VaccinationStatus;
  className?: string;
}

export function VaccinationStatusBadge({ status, className }: VaccinationStatusBadgeProps) {
  const config = STATUS_CONFIG[status];
  return (
    <Badge variant={config.variant} icon={config.icon} className={className}>
      {config.label}
    </Badge>
  );
}
