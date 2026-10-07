import Link from 'next/link';
import { Badge } from '@/components/ui/badge';
import { buttonVariants } from '@/components/ui/button';
import { LoadingState } from '@/components/common/LoadingState';
import { ErrorState } from '@/components/common/ErrorState';
import { formatAppointmentWhen } from '@/lib/format/appointment';
import { formatInZone } from '@/lib/format/time';
import type { CustomerAppointmentSummary } from '@/lib/api/staff-appointments';

/**
 * `components/appointments/AppointmentSummaryPanel.tsx` — HO-APT-002 §11.7 (US-CLN-08): added to
 * HO-ACC-002's customer page. The no-show count as text, upcoming appointments as links, the last
 * visit, and **Book appointment** into the staff booking screen prefilled with this customer.
 */
export function AppointmentSummaryPanel({
  customerId,
  summary,
  isLoading,
  isError,
  onRetry,
}: {
  customerId: string;
  summary: CustomerAppointmentSummary | undefined;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
}) {
  return (
    <section
      aria-labelledby="appointments-panel-heading"
      className="flex flex-col gap-3 rounded-[var(--radius-md)] border border-border bg-surface p-4"
    >
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 id="appointments-panel-heading" className="font-display text-lg font-semibold text-ink">
          Appointments
        </h2>
        <Link
          href={`/staff/appointments/new?customerId=${encodeURIComponent(customerId)}`}
          className={buttonVariants({ size: 'sm' })}
        >
          Book appointment
        </Link>
      </div>
      {isLoading ? (
        <LoadingState variant="card" rows={3} label="Loading appointments…" />
      ) : isError || !summary ? (
        <ErrorState title="We couldn’t load this customer’s appointments." onRetry={onRetry} />
      ) : (
        <>
          <p className="flex flex-wrap items-center gap-2 text-sm text-ink">
            <span className="font-semibold">No-shows:</span>
            {summary.noShowCount > 0 ? (
              <Badge variant="danger">
                {summary.noShowCount} {summary.noShowCount === 1 ? 'no-show' : 'no-shows'}
              </Badge>
            ) : (
              <span className="text-ink-muted">None</span>
            )}
          </p>
          <p className="text-sm text-ink">
            <span className="font-semibold">Last visit:</span>{' '}
            <span className="text-ink-muted">
              {summary.lastVisitOn
                ? formatInZone(summary.lastVisitOn, 'UTC', { dateStyle: 'medium' })
                : 'No visit recorded'}
            </span>
          </p>
          <div>
            <h3 className="text-sm font-semibold text-ink">Upcoming</h3>
            {summary.upcoming.length === 0 ? (
              <p className="text-sm text-ink-muted">No upcoming appointments.</p>
            ) : (
              <ul className="m-0 mt-1 flex list-none flex-col gap-1 p-0 text-sm">
                {summary.upcoming.map((appointment) => (
                  <li key={appointment.id}>
                    <Link
                      href={`/staff/appointments/${appointment.id}`}
                      className="font-semibold text-accent-ink no-underline hover:underline"
                    >
                      {appointment.service.name} · {appointment.catName}
                    </Link>{' '}
                    <span className="text-ink-muted">
                      {appointment.startsOn
                        ? `${formatAppointmentWhen(appointment.startsOn, appointment.branchTimeZoneId)} (${appointment.branchTimeZoneId})`
                        : ''}
                    </span>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </>
      )}
    </section>
  );
}
