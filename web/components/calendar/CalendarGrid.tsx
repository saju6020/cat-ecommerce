'use client';

import { useEffect } from 'react';
import Link from 'next/link';
import { AlertTriangle, ShieldAlert, Plus } from 'lucide-react';
import { cn } from '@/lib/utils';
import { formatBranchDateLong } from '@/lib/format/appointment';
import { localDateOf, timeOnlyInZone } from '@/lib/format/time';
import { useCalendarDrag, type DragTarget } from '@/lib/hooks/useCalendarDrag';
import type {
  CalendarAppointment,
  CalendarPayload,
  CalendarView,
} from '@/lib/api/staff-appointments';
import {
  ROW_PX,
  blockBox,
  hhmm,
  minutesAtOffset,
  minutesOfDay,
  offShiftSegments,
  rowsIn,
  visibleRange,
} from './layout';

export interface PendingMove {
  appointmentId: string;
  target: DragTarget;
}

const STRIPES =
  'repeating-linear-gradient(135deg, var(--border-strong) 0, var(--border-strong) 1px, transparent 1px, transparent 7px)';

/**
 * ⭐ `components/calendar/CalendarGrid.tsx` — HO-APT-002 §11.6, ADR-FE-026. An in-repo CSS layout:
 * time rows on the payload's `gridMinutes`, one column per resource per day. Off-shift time is
 * shaded AND patterned AND labelled (not colour alone), closures are labelled bands, and each
 * appointment block states the cat, service, status, and — in words — needs-rescheduling and
 * high-risk. A block can be dragged with the pointer to another time or column (a drop opens the
 * move confirmation); the **Move…** button on every block is the keyboard path to the same request
 * (UI-6). Clicking an empty part of a column, or the **Book** button in its heading, starts a staff
 * booking. While a move is pending the block shows *"Moving…"* at the target.
 */
export function CalendarGrid({
  payload,
  view,
  dates,
  zone,
  pending,
  onDropAppointment,
  onMoveDialog,
  onNewAt,
  onDraggingChange,
}: {
  payload: CalendarPayload;
  view: CalendarView;
  dates: string[];
  zone: string;
  pending: PendingMove | null;
  onDropAppointment: (appointment: CalendarAppointment, target: DragTarget) => void;
  onMoveDialog: (appointment: CalendarAppointment) => void;
  onNewAt: (resourceId: string, dateIso: string, minutes: number | null) => void;
  /** Tells the container a drag is in progress, so a poll can wait until the drop (UI-5). */
  onDraggingChange?: (dragging: boolean) => void;
}) {
  const grid = payload.gridMinutes;
  const range = visibleRange(payload, zone);
  const rows = rowsIn(range, grid);
  const height = rows * ROW_PX;
  const byId = new Map(payload.appointments.map((a) => [a.id, a]));

  const { drag, begin, consumeClick } = useCalendarDrag({
    snap: (offsetY) => minutesAtOffset(offsetY, range, grid),
    onDrop: (id, target) => {
      const appointment = byId.get(id);
      if (appointment) onDropAppointment(appointment, target);
    },
  });

  const isDragging = drag !== null;
  useEffect(() => {
    onDraggingChange?.(isDragging);
  }, [isDragging, onDraggingChange]);

  const hours: number[] = [];
  for (let m = range.startMinutes; m < range.endMinutes; m += 60) hours.push(m);

  return (
    <div className="overflow-x-auto rounded-[var(--radius-md)] border border-border bg-surface">
      <div className="flex min-w-max">
        <div
          className="sticky left-0 z-10 w-14 flex-none border-r border-border bg-surface"
          aria-hidden="true"
        >
          <div className="h-14 border-b border-border" />
          <div className="relative" style={{ height }}>
            {hours.map((minutes) => (
              <span
                key={minutes}
                className="absolute right-1 -translate-y-2 text-[11px] text-ink-muted"
                style={{ top: ((minutes - range.startMinutes) / grid) * ROW_PX }}
              >
                {hhmm(minutes)}
              </span>
            ))}
          </div>
        </div>

        {dates.map((dateIso) => (
          <div
            key={dateIso}
            className="flex flex-none flex-col border-r border-border last:border-r-0"
          >
            {dates.length > 1 && (
              <p className="border-b border-border px-2 py-1 text-xs font-bold text-ink">
                {formatBranchDateLong(dateIso)}
              </p>
            )}
            <div className="flex">
              {payload.resources.map((resource) => {
                const closure = payload.closures.find(
                  (c) => dateIso >= c.startsOn && dateIso <= c.endsOn,
                );
                const appointments = payload.appointments.filter((a) => {
                  if (!a.startsOn || localDateOf(a.startsOn, zone) !== dateIso) return false;
                  return (view === 'performer' ? a.performerId : a.roomId) === resource.id;
                });
                const off =
                  view === 'performer'
                    ? offShiftSegments(payload, resource.id, dateIso, zone, range)
                    : [];
                const ghost =
                  drag?.target &&
                  drag.target.resourceId === resource.id &&
                  drag.target.dateIso === dateIso
                    ? drag.target
                    : null;
                const movingHere =
                  pending &&
                  pending.target.resourceId === resource.id &&
                  pending.target.dateIso === dateIso
                    ? pending
                    : null;
                const dragged = drag ? byId.get(drag.appointmentId) : null;

                return (
                  <div
                    key={resource.id}
                    className="w-40 flex-none border-l border-border first:border-l-0"
                  >
                    <div className="flex h-14 items-center justify-between gap-1 border-b border-border px-2">
                      <span
                        className="min-w-0 truncate text-sm font-semibold text-ink"
                        title={resource.name}
                      >
                        {resource.name}
                      </span>
                      <button
                        type="button"
                        aria-label={`Book an appointment for ${resource.name} on ${formatBranchDateLong(dateIso)}`}
                        onClick={() => onNewAt(resource.id, dateIso, null)}
                        className="flex-none rounded-[var(--radius-sm)] p-1 text-accent-ink hover:bg-surface-sunken focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
                      >
                        <Plus size={14} aria-hidden="true" />
                      </button>
                    </div>
                    <div
                      data-calendar-column=""
                      data-resource-id={resource.id}
                      data-date={dateIso}
                      className="relative cursor-cell"
                      style={{ height }}
                      onClick={(event) => {
                        if (event.target !== event.currentTarget || consumeClick()) return;
                        const rect = event.currentTarget.getBoundingClientRect();
                        onNewAt(
                          resource.id,
                          dateIso,
                          minutesAtOffset(event.clientY - rect.top, range, grid),
                        );
                      }}
                    >
                      {hours.map((minutes) => (
                        <div
                          key={minutes}
                          aria-hidden="true"
                          className="pointer-events-none absolute inset-x-0 border-t border-border/70"
                          style={{ top: ((minutes - range.startMinutes) / grid) * ROW_PX }}
                        />
                      ))}
                      {off.map((segment) => {
                        const box = blockBox(segment.startMinutes, segment.endMinutes, range, grid);
                        return (
                          <div
                            key={segment.startMinutes}
                            data-off-shift=""
                            className="pointer-events-none absolute inset-x-0 overflow-hidden bg-surface-sunken/70"
                            style={{ top: box.top, height: box.height, backgroundImage: STRIPES }}
                          >
                            <span className="m-1 inline-block rounded-sm bg-surface px-1 text-[10px] font-semibold text-ink-muted">
                              Off shift
                            </span>
                          </div>
                        );
                      })}
                      {closure && (
                        <div
                          data-closure=""
                          className="pointer-events-none absolute inset-0 z-[1] bg-warning-bg/60"
                          style={{ backgroundImage: STRIPES }}
                        >
                          <span className="m-1 inline-block rounded-sm bg-warning-bg px-1.5 py-0.5 text-xs font-semibold text-warning-ink">
                            Closed — {closure.reason}
                          </span>
                        </div>
                      )}
                      {appointments.map((appointment) => {
                        const start = minutesOfDay(appointment.startsOn!, zone);
                        const end = minutesOfDay(appointment.endsOn!, zone) || 24 * 60;
                        const box = blockBox(start, end, range, grid);
                        const isPending = pending?.appointmentId === appointment.id;
                        const isDragging = drag?.appointmentId === appointment.id;
                        return (
                          <div
                            key={appointment.id}
                            role="group"
                            aria-label={`${appointment.catName}, ${appointment.serviceName}, ${timeOnlyInZone(appointment.startsOn!, zone)}`}
                            onPointerDown={(event) => {
                              if ((event.target as HTMLElement).closest('a,button')) return;
                              begin(event, appointment.id);
                            }}
                            className={cn(
                              'absolute inset-x-0.5 z-[2] flex cursor-grab touch-none select-none flex-col overflow-hidden rounded-[var(--radius-sm)] border bg-accent-soft px-1.5 py-1 text-[11px] leading-tight',
                              appointment.needsRescheduling
                                ? 'border-warning-ink'
                                : 'border-accent',
                              (isPending || isDragging) && 'opacity-40 outline-dashed outline-1',
                            )}
                            style={{ top: box.top, height: box.height }}
                          >
                            <Link
                              href={`/staff/appointments/${appointment.id}`}
                              onClick={(event) => {
                                if (consumeClick()) event.preventDefault();
                              }}
                              draggable={false}
                              className="truncate font-semibold text-ink no-underline hover:underline focus-visible:outline-2 focus-visible:outline-accent"
                            >
                              {appointment.catName}
                            </Link>
                            <span className="truncate text-ink-muted">
                              {appointment.serviceName} · {appointment.status}
                            </span>
                            {appointment.needsRescheduling && (
                              <span className="inline-flex items-center gap-0.5 font-semibold text-warning-ink">
                                <AlertTriangle size={11} aria-hidden="true" /> Needs a new time
                              </span>
                            )}
                            {appointment.catIsHighRisk && (
                              <span className="inline-flex items-center gap-0.5 font-semibold text-danger-ink">
                                <ShieldAlert size={11} aria-hidden="true" /> High risk
                              </span>
                            )}
                            <button
                              type="button"
                              onClick={() => onMoveDialog(appointment)}
                              aria-label={`Move ${appointment.catName}’s appointment`}
                              className="mt-auto w-fit rounded-sm text-[11px] font-semibold text-accent-ink underline underline-offset-2 focus-visible:outline-2 focus-visible:outline-accent"
                            >
                              Move…
                            </button>
                          </div>
                        );
                      })}
                      {(ghost || movingHere) && (dragged || movingHere) && (
                        <GhostBlock
                          appointment={(ghost ? dragged : byId.get(movingHere!.appointmentId))!}
                          minutes={(ghost ?? movingHere!.target).minutes}
                          range={range}
                          grid={grid}
                          zone={zone}
                          pendingLabel={!ghost}
                        />
                      )}
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

function GhostBlock({
  appointment,
  minutes,
  range,
  grid,
  zone,
  pendingLabel,
}: {
  appointment: CalendarAppointment;
  minutes: number;
  range: ReturnType<typeof visibleRange>;
  grid: number;
  zone: string;
  pendingLabel: boolean;
}) {
  const duration =
    appointment.startsOn && appointment.endsOn
      ? minutesOfDay(appointment.endsOn, zone) - minutesOfDay(appointment.startsOn, zone)
      : grid * 2;
  const box = blockBox(minutes, minutes + duration, range, grid);
  return (
    <div
      data-ghost=""
      className="pointer-events-none absolute inset-x-0.5 z-[3] flex flex-col rounded-[var(--radius-sm)] border-2 border-dashed border-accent bg-accent-soft px-1.5 py-1 text-[11px] font-semibold text-accent-ink"
      style={{ top: box.top, height: box.height }}
    >
      <span className="truncate">{appointment.catName}</span>
      <span>{pendingLabel ? 'Moving…' : hhmm(minutes)}</span>
    </div>
  );
}
