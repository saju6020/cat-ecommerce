import Link from 'next/link';
import { buttonVariants } from '@/components/ui/button';
import type { PortalAppointments } from '@/lib/api/portal';
import { formatAppointmentRange } from '@/lib/format/appointment';
import { BOOK_APPOINTMENT, portalAppointmentHref } from '@/lib/routing/portal-links';
import { appointmentStatusLabel } from './labels';
import { StatusBadge } from './StatusBadge';
import { TileEmpty, TileRow } from './DashboardSection';

/** U-A7: the time is the BRANCH's, in the branch zone, and the zone is named (ADR-FE-014/015). */
export function AppointmentsTile({ data }: { data: PortalAppointments }) {
  if (data.items.length === 0) {
    return (
      <>
        <TileEmpty>You have no upcoming appointments.</TileEmpty>
        <Link
          href={BOOK_APPOINTMENT}
          className={buttonVariants({ variant: 'secondary', size: 'sm' })}
        >
          Book an appointment
        </Link>
        <div className="h-3" />
      </>
    );
  }
  return (
    <ul>
      {data.items.map((appointment) => (
        <TileRow
          key={appointment.appointmentId}
          main={
            <>
              <Link
                href={portalAppointmentHref(appointment.appointmentId)}
                className="text-[14.5px] font-bold text-ink no-underline hover:underline"
              >
                {appointment.serviceName} · {appointment.catName}
              </Link>
              <p className="mt-0.5 text-[13px] leading-snug text-ink-muted">
                {appointment.startsOn && appointment.endsOn
                  ? formatAppointmentRange(
                      appointment.startsOn,
                      appointment.endsOn,
                      appointment.branchTimeZoneId,
                    )
                  : 'Time to be confirmed'}
                <br />
                {appointment.performerName ? `${appointment.performerName} · ` : ''}
                {appointment.branchName}
              </p>
            </>
          }
          side={<StatusBadge status={appointmentStatusLabel(appointment.status)} />}
        />
      ))}
    </ul>
  );
}
