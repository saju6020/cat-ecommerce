'use client';

import Link from 'next/link';
import { AlertTriangle, ShieldAlert } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { EmptyState } from '@/components/common/EmptyState';
import { AppointmentStatusBadge } from '@/components/appointments/AppointmentStatusBadge';
import { formatBranchDateLong } from '@/lib/format/appointment';
import { localDateOf, timeOnlyInZone } from '@/lib/format/time';
import type { CalendarAppointment, CalendarPayload } from '@/lib/api/staff-appointments';

/**
 * `components/calendar/CalendarAgenda.tsx` — HO-APT-002 §11.6, UI-1: below 768 px the grid becomes
 * a time-ordered list per day — no horizontal scrolling. Closures are text rows; every item has
 * **Move…** (the only move path at this size) and shows its vet or room, status, and the flag and
 * high-risk markers in words.
 */
export function CalendarAgenda({
  payload,
  dates,
  zone,
  onMoveDialog,
}: {
  payload: CalendarPayload;
  dates: string[];
  zone: string;
  onMoveDialog: (appointment: CalendarAppointment) => void;
}) {
  const nameOf = (id: string | null) => payload.resources.find((r) => r.id === id)?.name ?? null;
  const total = payload.appointments.length;
  return (
    <div className="flex flex-col gap-5">
      {dates.map((dateIso) => {
        const closure = payload.closures.find((c) => dateIso >= c.startsOn && dateIso <= c.endsOn);
        const items = payload.appointments
          .filter((a) => a.startsOn && localDateOf(a.startsOn, zone) === dateIso)
          .sort((a, b) => new Date(a.startsOn!).getTime() - new Date(b.startsOn!).getTime());
        return (
          <section
            key={dateIso}
            aria-labelledby={`agenda-${dateIso}`}
            className="flex flex-col gap-2"
          >
            <h3 id={`agenda-${dateIso}`} className="text-sm font-bold text-ink">
              {formatBranchDateLong(dateIso)}{' '}
              <span className="font-normal text-ink-muted">({zone})</span>
            </h3>
            {closure && (
              <p className="rounded-[var(--radius-sm)] bg-warning-bg px-3 py-2 text-sm font-semibold text-warning-ink">
                Closed — {closure.reason}
              </p>
            )}
            {items.length === 0 && !closure && (
              <p className="text-sm text-ink-muted">Nothing booked.</p>
            )}
            <ul className="m-0 flex list-none flex-col gap-2 p-0">
              {items.map((appointment) => (
                <li
                  key={appointment.id}
                  className="flex flex-col gap-1 rounded-[var(--radius-md)] border border-border bg-surface p-3 text-sm"
                >
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="font-bold tabular-nums text-ink">
                      {timeOnlyInZone(appointment.startsOn!, zone)}–
                      {timeOnlyInZone(appointment.endsOn!, zone)}
                    </span>
                    <Link
                      href={`/staff/appointments/${appointment.id}`}
                      className="font-semibold text-accent-ink no-underline hover:underline"
                    >
                      {appointment.catName}
                    </Link>
                    <AppointmentStatusBadge status={appointment.status} />
                  </div>
                  <p className="text-ink-muted">
                    {appointment.serviceName} ·{' '}
                    {nameOf(appointment.performerId) ?? nameOf(appointment.roomId) ?? '—'}
                  </p>
                  {appointment.needsRescheduling && (
                    <p className="inline-flex items-center gap-1 font-semibold text-warning-ink">
                      <AlertTriangle size={14} aria-hidden="true" /> Needs a new time
                    </p>
                  )}
                  {appointment.catIsHighRisk && (
                    <p className="inline-flex items-center gap-1 font-semibold text-danger-ink">
                      <ShieldAlert size={14} aria-hidden="true" /> High risk
                    </p>
                  )}
                  <div>
                    <Button
                      size="sm"
                      variant="secondary"
                      aria-label={`Move ${appointment.catName}’s appointment`}
                      onClick={() => onMoveDialog(appointment)}
                    >
                      Move…
                    </Button>
                  </div>
                </li>
              ))}
            </ul>
          </section>
        );
      })}
      {total === 0 && (
        <EmptyState
          title="No appointments in this range."
          description="Booked appointments appear here."
        />
      )}
    </div>
  );
}
