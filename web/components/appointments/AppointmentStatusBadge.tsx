import {
  Ban,
  CalendarCheck,
  CheckCircle2,
  Clock,
  HeartPulse,
  Hourglass,
  UserCheck,
  UserX,
  type LucideIcon,
} from 'lucide-react';
import { Badge, type BadgeProps } from '@/components/ui/badge';
import type { AppointmentStatus } from '@/lib/api/types';

/**
 * `components/appointments/AppointmentStatusBadge.tsx` — HO-APT-002 UI-6: a status is always a
 * word plus an icon, never colour alone (the colour only reinforces).
 */
const STATUS_DISPLAY: Record<
  AppointmentStatus,
  { label: string; variant: BadgeProps['variant']; icon: LucideIcon }
> = {
  Held: { label: 'Held for you', variant: 'warning', icon: Hourglass },
  HoldExpired: { label: 'Hold ended', variant: 'neutral', icon: Clock },
  HoldReleased: { label: 'Hold released', variant: 'neutral', icon: Clock },
  Booked: { label: 'Booked', variant: 'success', icon: CalendarCheck },
  CheckedIn: { label: 'Checked in', variant: 'success', icon: UserCheck },
  InConsultation: { label: 'In consultation', variant: 'success', icon: HeartPulse },
  Completed: { label: 'Completed', variant: 'neutral', icon: CheckCircle2 },
  Cancelled: { label: 'Cancelled', variant: 'neutral', icon: Ban },
  NoShow: { label: 'No-show', variant: 'danger', icon: UserX },
};

export function statusLabel(status: AppointmentStatus): string {
  return STATUS_DISPLAY[status]?.label ?? status;
}

export function AppointmentStatusBadge({ status }: { status: AppointmentStatus }) {
  const display = STATUS_DISPLAY[status] ?? { label: status, variant: 'neutral', icon: Clock };
  const Icon = display.icon;
  return (
    <Badge variant={display.variant} icon={<Icon size={13} aria-hidden="true" />}>
      {display.label}
    </Badge>
  );
}
