'use client';

import { useMemo, useState } from 'react';
import { useSearchParams } from 'next/navigation';
import { ClinicQueue } from '@/components/front-desk/ClinicQueue';
import { TodayList } from '@/components/front-desk/TodayList';
import { RecoveryBoardPanel } from '@/components/recovery/RecoveryBoardPanel';
import { CheckInDialog } from '@/components/front-desk/CheckInDialog';
import { StartDialog } from '@/components/front-desk/StartDialog';
import { ActiveBranchZoneGate } from '@/components/appointments/ActiveBranchZoneGate';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { toast } from '@/components/ui/use-toast';
import { cn } from '@/lib/utils';
import {
  useCalendar,
  useCheckInAppointment,
  useCompleteAppointment,
  useDayList,
  useRecordNoShow,
  useSetTriagePriority,
  useStaffAppointmentDetails,
  useStartAppointment,
} from '@/lib/hooks/useStaffAppointments';
import { useClinicQueue } from '@/lib/hooks/useFrontDesk';
import { useBranchRooms } from '@/lib/hooks/useAdminBranches';
import { useActiveBranchId } from '@/lib/hooks/useActiveBranch';
import { useStaffSession } from '@/lib/hooks/useSession';
import { useFilterUrlUpdate } from '@/lib/hooks/useFilterUrlUpdate';
import { WalkInSection } from './WalkInSection';
import { ApiForbiddenError } from '@/lib/api/client';
import { describeAppointmentError } from '@/lib/api/errors';
import { todayInZone } from '@/lib/format/time';
import type { CalendarAppointment } from '@/lib/api/staff-appointments';
import type { QueueEntry } from '@/lib/api/front-desk';
import type { AppointmentAction, TriagePriority } from '@/lib/api/types';

type Tab = 'today' | 'queue' | 'recovery';
const TABS: Tab[] = ['today', 'queue', 'recovery'];
const TAB_LABELS: Record<Tab, string> = { today: 'Today', queue: 'Queue', recovery: 'Recovery' };
const OPEN_STATUSES = ['Booked', 'CheckedIn', 'InConsultation'];

/** Container for `/staff/front-desk` (HO-APT-002 §11.5): polling data, dialogs and walk-in wiring. */
export function FrontDeskView() {
  // The zone is the ACTIVE branch's; with no active branch the gate asks for one (W16).
  return <ActiveBranchZoneGate>{(zone) => <FrontDeskContent zone={zone} />}</ActiveBranchZoneGate>;
}

function FrontDeskContent({ zone }: { zone: string }) {
  const searchParams = useSearchParams();
  const setParam = useFilterUrlUpdate();
  const tabParam = searchParams.get('tab');
  const tab: Tab = tabParam === 'queue' || tabParam === 'recovery' ? tabParam : 'today';
  const mine = searchParams.get('mine') === '1';

  const activeBranchId = useActiveBranchId();
  const today = todayInZone(zone);
  const session = useStaffSession();
  const canShowMine = (session.data?.roleIds ?? []).some(
    (role) => role === 'Vet' || role === 'Administrator',
  );

  const dayList = useDayList(today);
  const queue = useClinicQueue(mine && canShowMine ? 'me' : undefined);
  const performersCalendar = useCalendar(
    { from: today, to: today, view: 'performer' },
    { poll: false },
  );
  const rooms = useBranchRooms(activeBranchId ?? '');

  const checkIn = useCheckInAppointment();
  const setTriage = useSetTriagePriority();
  const start = useStartAppointment();
  const complete = useCompleteAppointment();
  const noShow = useRecordNoShow();

  const [checkInTarget, setCheckInTarget] = useState<CalendarAppointment | null>(null);
  const [noShowTarget, setNoShowTarget] = useState<CalendarAppointment | null>(null);
  const [startTarget, setStartTarget] = useState<QueueEntry | null>(null);
  const [highlightedId, setHighlightedId] = useState<string | null>(null);

  const dayRows = useMemo(() => dayList.data ?? [], [dayList.data]);
  const dayDetailIds =
    tab === 'today' ? dayRows.filter((r) => OPEN_STATUSES.includes(r.status)).map((r) => r.id) : [];
  const queueEntries = useMemo(() => queue.data?.entries ?? [], [queue.data]);
  const queueIds = tab === 'queue' ? queueEntries.map((e) => e.appointmentId) : [];
  const dayDetails = useStaffAppointmentDetails(dayDetailIds);
  const queueDetails = useStaffAppointmentDetails(queueIds);

  const allowedFor = (ids: string[], results: typeof dayDetails) => {
    const map: Record<string, AppointmentAction[] | undefined> = {};
    ids.forEach((id, index) => {
      map[id] = results[index]?.data?.allowedTransitions;
    });
    return map;
  };
  const dayAllowed = allowedFor(dayDetailIds, dayDetails);
  const queueAllowed = allowedFor(queueIds, queueDetails);

  const performerNames = useMemo(() => {
    const names: Record<string, string> = {};
    performersCalendar.data?.resources.forEach((resource) => {
      names[resource.id] = resource.name;
    });
    return names;
  }, [performersCalendar.data]);

  const forbidden = [dayList.error, queue.error].some((e) => e instanceof ApiForbiddenError);
  if (forbidden) return <PermissionDeniedState />;

  async function run(label: string, action: () => Promise<unknown>) {
    try {
      await action();
    } catch (error) {
      toast({ title: `${label} didn’t work`, description: describeAppointmentError(error) });
      throw error;
    }
  }

  function startEntry(entry: QueueEntry) {
    if (entry.isWalkIn || !entry.performerName) {
      setStartTarget(entry);
      return;
    }
    void run('Starting the visit', () =>
      start.mutateAsync({ appointmentId: entry.appointmentId }),
    ).catch(() => undefined);
  }

  return (
    <div className="flex flex-col gap-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="font-display text-2xl font-semibold text-ink">Front desk</h1>
        {tab === 'queue' && canShowMine && (
          <label className="flex items-center gap-2 text-sm font-semibold text-ink">
            <input
              type="checkbox"
              checked={mine}
              onChange={(event) => setParam('mine', event.target.checked ? '1' : '')}
              className="h-4 w-4"
            />
            My queue
          </label>
        )}
      </div>

      <div role="tablist" aria-label="Front desk" className="flex gap-1 border-b border-border">
        {TABS.map((key) => (
          <button
            key={key}
            id={`frontdesk-tab-${key}`}
            type="button"
            role="tab"
            aria-selected={tab === key}
            aria-controls="frontdesk-panel"
            tabIndex={tab === key ? 0 : -1}
            onClick={() => setParam('tab', key === 'today' ? '' : key)}
            onKeyDown={(event) => {
              if (event.key === 'ArrowRight' || event.key === 'ArrowLeft') {
                const at = TABS.indexOf(key);
                const step = event.key === 'ArrowRight' ? 1 : -1;
                const next: Tab = TABS[(at + step + TABS.length) % TABS.length]!;
                setParam('tab', next === 'today' ? '' : next);
                requestAnimationFrame(() =>
                  document.getElementById(`frontdesk-tab-${next}`)?.focus(),
                );
              }
            }}
            className={cn(
              '-mb-px border-b-2 px-4 py-2 text-sm font-semibold focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent',
              tab === key
                ? 'border-accent text-accent-ink'
                : 'border-transparent text-ink-muted hover:text-ink',
            )}
          >
            {TAB_LABELS[key]}
          </button>
        ))}
      </div>

      <div id="frontdesk-panel" role="tabpanel" aria-labelledby={`frontdesk-tab-${tab}`}>
        {tab === 'recovery' ? (
          // HO-SRG-002 §11.8 — the surgery area's Recovery board (polled every 30 s on its own).
          <RecoveryBoardPanel zone={zone} />
        ) : tab === 'today' ? (
          <TodayList
            appointments={dayList.data}
            zone={zone}
            isLoading={dayList.isLoading}
            isError={dayList.isError}
            onRetry={() => void dayList.refetch()}
            performerNames={performerNames}
            allowedByAppointmentId={dayAllowed}
            onCheckIn={setCheckInTarget}
            onNoShow={setNoShowTarget}
          />
        ) : (
          <div className="grid grid-cols-[minmax(0,1fr)] items-start gap-6 lg:grid-cols-[minmax(0,1fr)_24rem]">
            <ClinicQueue
              queue={queue.data}
              zone={zone}
              isLoading={queue.isLoading}
              isError={queue.isError}
              onRetry={() => void queue.refetch()}
              allowedByAppointmentId={queueAllowed}
              highlightedId={highlightedId}
              isMine={mine}
              onChangePriority={(entry, priority: TriagePriority) =>
                void run('Changing the priority', () =>
                  setTriage.mutateAsync({
                    appointmentId: entry.appointmentId,
                    triagePriority: priority,
                  }),
                ).catch(() => undefined)
              }
              onStart={startEntry}
              onComplete={(entry) =>
                void run('Completing the visit', () =>
                  complete.mutateAsync(entry.appointmentId),
                ).catch(() => undefined)
              }
            />
            <WalkInSection
              onQueued={(appointmentId) => {
                setHighlightedId(appointmentId);
                setTimeout(() => setHighlightedId(null), 10_000);
              }}
            />
          </div>
        )}
      </div>

      <CheckInDialog
        open={checkInTarget !== null}
        onOpenChange={(open) => !open && setCheckInTarget(null)}
        catName={checkInTarget?.catName ?? ''}
        customerName={checkInTarget?.customerName ?? ''}
        onCheckIn={async (priority) => {
          if (!checkInTarget) return;
          await checkIn.mutateAsync({ appointmentId: checkInTarget.id, triagePriority: priority });
          toast({ title: `${checkInTarget.catName} checked in` });
        }}
      />

      <ConfirmDialog
        open={noShowTarget !== null}
        onOpenChange={(open) => !open && setNoShowTarget(null)}
        title={`Record ${noShowTarget?.catName ?? 'this cat'} as a no-show?`}
        consequence={`This counts against ${noShowTarget?.customerName ?? 'the customer'}.`}
        confirmLabel="Record no-show"
        variant="destructive"
        onConfirm={async () => {
          const target = noShowTarget;
          if (!target) return;
          try {
            await noShow.mutateAsync(target.id);
            toast({ title: 'No-show recorded' });
          } catch (error) {
            toast({ title: 'No-show not recorded', description: describeAppointmentError(error) });
          } finally {
            setNoShowTarget(null);
          }
        }}
      />

      <StartDialog
        open={startTarget !== null}
        onOpenChange={(open) => !open && setStartTarget(null)}
        catName={startTarget?.catName ?? ''}
        service={startTarget?.service ?? ''}
        performers={(performersCalendar.data?.resources ?? []).map((r) => ({
          id: r.id,
          name: r.name,
        }))}
        rooms={(rooms.data ?? [])
          .filter((r) => r.isActive)
          .map((r) => ({ id: r.id, name: r.name, detail: r.kind }))}
        onStart={async (values) => {
          if (!startTarget) return;
          await start.mutateAsync({
            appointmentId: startTarget.appointmentId,
            request: { performerId: values.performerId, roomId: values.roomId },
          });
          toast({ title: `${startTarget.catName}’s visit started` });
        }}
      />
    </div>
  );
}
