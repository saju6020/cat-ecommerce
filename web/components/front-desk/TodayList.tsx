'use client';

import Link from 'next/link';
import { Button } from '@/components/ui/button';
import { EmptyState } from '@/components/common/EmptyState';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { AppointmentStatusBadge } from '@/components/appointments/AppointmentStatusBadge';
import { HighRiskBadge } from '@/components/cat/HighRiskBadge';
import { timeOnlyInZone } from '@/lib/format/time';
import type { CalendarAppointment } from '@/lib/api/staff-appointments';
import type { AppointmentAction } from '@/lib/api/types';

export interface TodayListProps {
  appointments: CalendarAppointment[] | undefined;
  zone: string;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
  /** Vet names by performer id (the day list carries ids only). */
  performerNames: Record<string, string>;
  /** Per appointment, from the detail's `allowedTransitions` — `undefined` while still loading. */
  allowedByAppointmentId: Record<string, AppointmentAction[] | undefined>;
  onCheckIn: (appointment: CalendarAppointment) => void;
  onNoShow: (appointment: CalendarAppointment) => void;
}

/**
 * `components/front-desk/TodayList.tsx` — HO-APT-002 §11.5 (US-CLN-05, 08). Today's appointments at
 * the active branch by time. **Check in** and **No-show** appear only when the server's
 * `allowedTransitions` for that appointment includes them (ADR-FE-028); the grace period before a
 * no-show is the server's rule — an early attempt is refused with its own message, not pre-judged
 * here. Each row links to the full appointment.
 */
export function TodayList({
  appointments,
  zone,
  isLoading,
  isError,
  onRetry,
  performerNames,
  allowedByAppointmentId,
  onCheckIn,
  onNoShow,
}: TodayListProps) {
  if (isLoading)
    return <LoadingState variant="table" rows={5} label="Loading today’s appointments…" />;
  if (isError && !appointments) {
    return <ErrorState title="We couldn’t load today’s appointments." onRetry={onRetry} />;
  }
  if (!appointments || appointments.length === 0) {
    return (
      <EmptyState
        title="No appointments booked today."
        description="Walk-ins are added from the Queue tab."
      />
    );
  }

  return (
    <section aria-labelledby="today-heading" className="flex flex-col gap-3">
      <h2 id="today-heading" className="font-display text-lg font-semibold text-ink">
        Today’s appointments <span className="text-sm font-normal text-ink-muted">({zone})</span>
      </h2>
      <ul className="m-0 flex list-none flex-col gap-3 p-0">
        {appointments.map((appointment) => {
          const allowed = allowedByAppointmentId[appointment.id];
          const can = (action: AppointmentAction) => allowed?.includes(action) ?? false;
          return (
            <li
              key={appointment.id}
              className="flex flex-wrap items-center justify-between gap-3 rounded-[var(--radius-md)] border border-border bg-surface p-4"
            >
              <div className="min-w-0 flex-1 text-sm">
                <p className="flex flex-wrap items-center gap-2">
                  <span className="text-[15px] font-bold tabular-nums text-ink">
                    {appointment.startsOn ? timeOnlyInZone(appointment.startsOn, zone) : '—'}
                  </span>
                  <Link
                    href={`/staff/appointments/${appointment.id}`}
                    className="text-[15px] font-semibold text-accent-ink no-underline hover:underline"
                  >
                    {appointment.catName}
                  </Link>
                  {appointment.catIsHighRisk && (
                    <HighRiskBadge handlingNote={null} variant="compact" />
                  )}
                  <AppointmentStatusBadge status={appointment.status} />
                </p>
                <p className="mt-0.5 text-ink-muted">
                  {appointment.customerName} · {appointment.serviceName} ·{' '}
                  {(appointment.performerId && performerNames[appointment.performerId]) ??
                    'Vet to be confirmed'}
                </p>
              </div>
              <div className="flex flex-wrap gap-2">
                {can('CheckIn') && (
                  <Button
                    size="sm"
                    aria-label={`Check in ${appointment.catName}`}
                    onClick={() => onCheckIn(appointment)}
                  >
                    Check in
                  </Button>
                )}
                {can('RecordNoShow') && (
                  <Button
                    size="sm"
                    variant="secondary"
                    aria-label={`No-show ${appointment.catName}`}
                    onClick={() => onNoShow(appointment)}
                  >
                    No-show
                  </Button>
                )}
              </div>
            </li>
          );
        })}
      </ul>
    </section>
  );
}
