'use client';

import { useCallback, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { CalendarAgenda } from '@/components/calendar/CalendarAgenda';
import { CalendarGrid, type PendingMove } from '@/components/calendar/CalendarGrid';
import { CalendarToolbar, type CalendarRange } from '@/components/calendar/CalendarToolbar';
import { MoveDialog, type MoveSubject, type MoveTarget } from '@/components/calendar/MoveDialog';
import { hhmm } from '@/components/calendar/layout';
import { ActiveBranchZoneGate } from '@/components/appointments/ActiveBranchZoneGate';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { toast } from '@/components/ui/use-toast';
import { useFilterUrlUpdate } from '@/lib/hooks/useFilterUrlUpdate';
import { useFrozenWhile } from '@/lib/hooks/useFrozenWhile';
import { useMoveChoices } from '@/lib/hooks/useMoveChoices';
import {
  useCalendar,
  useFlaggedAppointments,
  useMoveAppointment,
} from '@/lib/hooks/useStaffAppointments';
import type { DragTarget } from '@/lib/hooks/useCalendarDrag';
import { ApiForbiddenError } from '@/lib/api/client';
import { describeAppointmentError, getViolationCode } from '@/lib/api/errors';
import type { CalendarAppointment, CalendarView as View } from '@/lib/api/staff-appointments';
import {
  addDaysIso,
  dateRange,
  formatBranchDateLong,
  startOfWeekIso,
  zonedLocalToInstant,
} from '@/lib/format/appointment';
import { localDateOf, todayInZone } from '@/lib/format/time';

/**
 * Container for the calendar (HO-APT-002 §11.6, ADR-FE-026/-027). View, range, date and the
 * chosen resources are URL params. The data polls every 30 s; while a drag is in progress the
 * grid keeps showing the data it had (`useFrozenWhile`), and the fresh poll is applied after the
 * drop. A drop opens the move confirmation; once confirmed the block shows *"Moving…"* at the
 * target until the SERVER answers — a refusal snaps it back with the server's own reason in a
 * toast and a live region (US-CLN-07 AC2), a stale version says so and refetches.
 */
export function CalendarView() {
  // The zone is the ACTIVE branch's; with no active branch the gate asks for one (W16).
  return <ActiveBranchZoneGate>{(zone) => <CalendarContent zone={zone} />}</ActiveBranchZoneGate>;
}

function CalendarContent({ zone }: { zone: string }) {
  const router = useRouter();
  const searchParams = useSearchParams();
  const setParam = useFilterUrlUpdate();

  const range: CalendarRange = searchParams.get('range') === 'week' ? 'week' : 'day';
  const view: View = searchParams.get('view') === 'room' ? 'room' : 'performer';
  const today = todayInZone(zone);
  const date = searchParams.get('date') || today;
  const ids = (searchParams.get('ids') ?? '').split(',').filter(Boolean);
  const dates = range === 'week' ? dateRange(startOfWeekIso(date), 7) : [date];

  const query = useCalendar({ from: dates[0]!, to: dates[dates.length - 1]!, view, ids });
  const flagged = useFlaggedAppointments();
  const move = useMoveAppointment();

  const [dragging, setDragging] = useState(false);
  const payload = useFrozenWhile(query.data, dragging);
  const [subject, setSubject] = useState<MoveSubject | null>(null);
  const [version, setVersion] = useState(0);
  // The dialog sends the version it opened with, so a change by someone else that a poll brings in
  // is still refused. Only after a StaleCalendarVersion 409 does the retry adopt the refetched one.
  const [adoptLive, setAdoptLive] = useState(false);
  const [dropTarget, setDropTarget] = useState<MoveTarget | null>(null);
  const [pending, setPending] = useState<PendingMove | null>(null);
  const [announcement, setAnnouncement] = useState('');
  const choices = useMoveChoices(
    subject?.startsOn ? localDateOf(subject.startsOn, zone) : date,
    subject !== null,
  );

  const onDraggingChange = useCallback((value: boolean) => setDragging(value), []);

  const subjectOf = (a: CalendarAppointment): MoveSubject => ({
    id: a.id,
    catName: a.catName,
    serviceName: a.serviceName,
    startsOn: a.startsOn,
    performerId: a.performerId,
    roomId: a.roomId,
  });

  function stepDate(direction: -1 | 1) {
    setParam('date', addDaysIso(date, direction * (range === 'week' ? 7 : 1)));
  }

  function handleDrop(appointment: CalendarAppointment, target: DragTarget) {
    const startsOn = zonedLocalToInstant(target.dateIso, hhmm(target.minutes), zone);
    setSubject(subjectOf(appointment));
    setVersion(appointment.version);
    setAdoptLive(false);
    setDropTarget({
      startsOn,
      performerId: view === 'performer' ? target.resourceId : (appointment.performerId ?? ''),
      roomId: view === 'room' ? target.resourceId : (appointment.roomId ?? ''),
    });
    setPending(null);
    setAnnouncement('');
  }

  async function submitMove(target: MoveTarget) {
    if (!subject) return;
    const appointmentId = subject.id;
    const sentVersion = adoptLive
      ? (query.data?.appointments.find((a) => a.id === appointmentId)?.version ?? version)
      : version;
    const request = { expectedVersion: sentVersion, ...target };
    const detached = dropTarget !== null;
    if (!detached) {
      // From the dialog: errors are shown inside it, in the server's words.
      try {
        await move.mutateAsync({ appointmentId, request });
      } catch (error) {
        if (getViolationCode(error) === 'StaleCalendarVersion') setAdoptLive(true);
        throw error;
      }
      toast({ title: 'Appointment moved' });
      return;
    }
    // From a drop: close the dialog now, show "Moving…" at the target, settle when the server answers.
    // The target the user CONFIRMED (they may have gone Back and edited the dropped one).
    setPending({
      appointmentId,
      target: {
        resourceId: view === 'performer' ? target.performerId : target.roomId,
        dateIso: localDateOf(target.startsOn, zone),
        minutes: minutesFrom(target.startsOn, zone),
      },
    });
    void move
      .mutateAsync({ appointmentId, request })
      .then(() => toast({ title: 'Appointment moved' }))
      .catch((error: unknown) => {
        const reason = describeAppointmentError(error);
        setAnnouncement(`Not moved. ${reason}`);
        toast({ title: 'Not moved', description: reason });
      })
      .finally(() => setPending(null));
  }

  if (query.error instanceof ApiForbiddenError) return <PermissionDeniedState />;

  return (
    <div className="flex flex-col gap-4">
      <h1 className="font-display text-2xl font-semibold text-ink">Appointments</h1>
      <CalendarToolbar
        range={range}
        onRangeChange={(next) => setParam('range', next === 'day' ? '' : next)}
        view={view}
        onViewChange={(next) => {
          setParam('view', next === 'performer' ? '' : next);
          setParam('ids', '');
        }}
        resources={payload?.resources ?? []}
        selectedIds={ids}
        onSelectedIdsChange={(next) => setParam('ids', next.join(','))}
        dateLabel={
          range === 'week'
            ? `${formatBranchDateLong(dates[0]!)} – ${formatBranchDateLong(dates[6]!)}`
            : formatBranchDateLong(date)
        }
        zone={zone}
        onPrevious={() => stepDate(-1)}
        onToday={() => setParam('date', '')}
        onNext={() => stepDate(1)}
        flaggedCount={flagged.data?.length ?? null}
      />

      <p role="status" aria-live="polite" className="sr-only">
        {announcement}
      </p>
      {announcement && (
        <p className="rounded-[var(--radius-sm)] bg-danger-bg px-3 py-2 text-sm font-semibold text-danger-ink">
          {announcement}
        </p>
      )}

      {query.isLoading ? (
        <LoadingState variant="card" rows={8} label="Loading the calendar…" />
      ) : query.isError && !payload ? (
        <ErrorState
          title="We couldn’t load the calendar just now."
          onRetry={() => void query.refetch()}
        />
      ) : payload ? (
        <>
          <div className="hidden md:block">
            <CalendarGrid
              payload={payload}
              view={view}
              dates={dates}
              zone={zone}
              pending={pending}
              onDropAppointment={handleDrop}
              onMoveDialog={(a) => {
                setDropTarget(null);
                setSubject(subjectOf(a));
                setVersion(a.version);
                setAdoptLive(false);
              }}
              onNewAt={(resourceId, dateIso, minutes) => {
                const params = new URLSearchParams({ date: dateIso });
                if (view === 'performer') params.set('performerId', resourceId);
                if (minutes !== null) params.set('time', hhmm(minutes));
                router.push(`/staff/appointments/new?${params.toString()}`);
              }}
              onDraggingChange={onDraggingChange}
            />
          </div>
          <div className="md:hidden">
            <CalendarAgenda
              payload={payload}
              dates={dates}
              zone={zone}
              onMoveDialog={(a) => {
                setDropTarget(null);
                setSubject(subjectOf(a));
                setVersion(a.version);
                setAdoptLive(false);
              }}
            />
          </div>
        </>
      ) : null}

      <MoveDialog
        open={subject !== null}
        onOpenChange={(open) => {
          if (!open) {
            setSubject(null);
            setDropTarget(null);
          }
        }}
        subject={subject}
        zone={zone}
        gridMinutes={payload?.gridMinutes ?? 15}
        performers={choices.performers}
        rooms={choices.rooms}
        initialTarget={dropTarget}
        onMove={submitMove}
      />
    </div>
  );
}

function minutesFrom(instant: string, zone: string): number {
  const [h, m] = new Intl.DateTimeFormat('en-GB', {
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23',
    timeZone: zone,
  })
    .format(new Date(instant))
    .split(':')
    .map(Number);
  return (h ?? 0) * 60 + (m ?? 0);
}
