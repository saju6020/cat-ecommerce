'use client';

import { useRef, useState } from 'react';
import { EmptyState } from '@/components/common/EmptyState';
import { MediaRecordDialog } from '@/components/boarding/care/MediaRecordDialog';
import { IncidentForm } from '@/components/boarding/incidents/IncidentForm';
import { QueryGate } from '@/components/boarding/shared/QueryGate';
import { ReasonDialog } from '@/components/boarding/shared/ReasonDialog';
import { TASK_KIND_LABEL } from '@/components/boarding/shared/labels';
import { describeBoardingError } from '@/lib/api/errors';
import type { CareTask, TaskDayList } from '@/lib/api/types';
import { useCompleteTask, useSkipTask, useTaskDayList } from '@/lib/hooks/useBoardingCare';
import { useFrozenWhile } from '@/lib/hooks/useFrozenWhile';
import { useBoardingDashboard } from '@/lib/hooks/useStaffBoarding';
import { useStaffSession } from '@/lib/hooks/useSession';
import { timeOnlyInZone, todayInZone } from '@/lib/format/time';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { ObservationSheet } from './ObservationSheet';
import { TaskBoardToolbar, type TaskStatusFilter } from './TaskBoardToolbar';
import { UnitTaskGroup } from './UnitTaskGroup';

type Sheet =
  | { kind: 'observe'; task: CareTask }
  | { kind: 'incident'; task: CareTask }
  | { kind: 'skip'; task: CareTask }
  | { kind: 'media'; task: CareTask }
  | null;

function without<T>(record: Record<string, T>, key: string): Record<string, T> {
  return Object.fromEntries(Object.entries(record).filter(([k]) => k !== key));
}

function matchesStatus(task: CareTask, filter: TaskStatusFilter): boolean {
  if (filter === '') return true;
  if (filter === 'Done') return task.status === 'Done';
  if (filter === 'Overdue') return task.status === 'Pending' && task.isOverdue;
  return task.status === 'Pending' && !task.isOverdue;
}

/**
 * `components/boarding/tasks/TaskBoard.tsx` — ⭐ HO-BRD-002 §11.7, US-BRD-09/10/11, NFR-USA-3. The
 * tablet task board: tasks grouped by unit, a **single tap on Done** completes one (optimistic,
 * announced in a live region, Undo while the request is in flight, a failure rolls back and says why
 * — ADR-FE-100). It refetches every 30 s without interrupting a tap or an open sheet
 * (ADR-FE-101): while a sheet or dialog is open the list on screen is held and catches up when it
 * closes. Overdue and “not eating” come from the server (ADR-FE-105).
 */
export function TaskBoard() {
  const [date, setDate] = useState('');
  const [mine, setMine] = useState(true);
  const [unitId, setUnitId] = useState('');
  const [status, setStatus] = useState<TaskStatusFilter>('');
  const [sheet, setSheet] = useState<Sheet>(null);
  const [announcement, setAnnouncement] = useState('');
  const [inFlight, setInFlight] = useState<Record<string, boolean>>({});
  const [errors, setErrors] = useState<Record<string, string>>({});
  const controllers = useRef<Record<string, AbortController>>({});

  const session = useStaffSession();
  const zone = useBranchTimeZone();
  const query = useTaskDayList({
    // The built read requires `date` (HO-BRD-001 1.1): the branch's today when none is picked.
    date: date || todayInZone(zone),
    unitId: unitId || undefined,
    assignedTo: mine ? 'me' : undefined,
  });
  const complete = useCompleteTask();
  const skip = useSkipTask();
  const held = useFrozenWhile<TaskDayList | undefined>(query.data, sheet !== null);
  // The built task list carries no per-cat not-eating flag (HO-BRD-001 1.1): it comes from the dashboard's
  // `notEating[]` (the server's rule) instead - never worked out here.
  const dashboard = useBoardingDashboard();
  const flagged = new Set((dashboard.data?.notEating ?? []).map((n) => n.catId));
  const merged: TaskDayList | undefined = held && {
    ...held,
    units: held.units.map((u) => ({
      ...u,
      cats: u.cats.map((c) =>
        c.notEating === null && dashboard.data ? { ...c, notEating: flagged.has(c.catId) } : c,
      ),
    })),
  };
  const gateQuery = { ...query, data: merged };

  async function onDone(task: CareTask) {
    const controller = new AbortController();
    controllers.current[task.id] = controller;
    setInFlight((p) => ({ ...p, [task.id]: true }));
    setErrors((p) => without(p, task.id));
    setAnnouncement(
      `${TASK_KIND_LABEL[task.kind]} for ${task.catName} marked done at ${timeOnlyInZone(new Date().toISOString(), query.data?.branchTimeZoneId ?? 'Asia/Dhaka')}.`,
    );
    try {
      await complete.mutateAsync({
        taskId: task.id,
        completedByName: session.data?.fullName ?? 'You',
        signal: controller.signal,
      });
    } catch (e) {
      setAnnouncement('');
      if (!controller.signal.aborted) {
        setErrors((p) => ({ ...p, [task.id]: describeBoardingError(e) }));
      } else {
        setAnnouncement(`Undone: ${task.title} for ${task.catName} is still to do.`);
      }
    } finally {
      delete controllers.current[task.id];
      setInFlight((p) => without(p, task.id));
    }
  }

  function onUndo(task: CareTask) {
    controllers.current[task.id]?.abort();
  }

  return (
    <div className="flex flex-col gap-3">
      <div role="status" aria-live="polite" className="sr-only">
        {announcement}
      </div>
      <QueryGate
        query={gateQuery}
        loadingLabel="Loading today’s tasks…"
        errorTitle="We couldn’t load the tasks."
      >
        {(data) => {
          const all = data.units.flatMap((u) => u.tasks);
          const counts = {
            due: all.filter((t) => t.status === 'Pending' && !t.isOverdue).length,
            overdue: all.filter((t) => t.status === 'Pending' && t.isOverdue).length,
            done: all.filter((t) => t.status === 'Done').length,
          };
          const groups = data.units.map((u) => ({
            ...u,
            tasks: u.tasks.filter((t) => matchesStatus(t, status)),
          }));
          const catsOf = (stayId: string) =>
            data.units
              .filter((u) => u.tasks.some((t) => t.stayId === stayId))
              .flatMap((u) => u.cats)
              .map((c) => ({ catId: c.catId, catName: c.catName }));
          const notEatingOf = (catId: string) =>
            data.units.some((u) => u.cats.some((c) => c.catId === catId && c.notEating));
          return (
            <>
              {dashboard.isError && (
                <p role="status" className="text-sm text-warning-ink">
                  Not-eating flags are unknown right now: they could not be loaded. Check the
                  dashboard.
                </p>
              )}
              <TaskBoardToolbar
                date={date || data.date}
                onDate={setDate}
                mine={mine}
                onMine={setMine}
                unitId={unitId}
                onUnit={setUnitId}
                units={data.units.map((u) => ({ unitId: u.unitId, unitName: u.unitName }))}
                status={status}
                onStatus={setStatus}
                counts={counts}
              />
              <p className="text-xs text-ink-muted">
                Times in {data.branchTimeZoneId} · updated{' '}
                {timeOnlyInZone(data.generatedOn, data.branchTimeZoneId)} · refreshes every 30
                seconds
              </p>
              {groups.length === 0 ? (
                <EmptyState
                  title="No tasks for these filters."
                  description="Try Everyone instead of My tasks, or another date."
                />
              ) : (
                <div className="grid items-start gap-4 md:grid-cols-2">
                  {groups.map((group) => (
                    <UnitTaskGroup
                      key={group.unitId}
                      group={group}
                      zone={data.branchTimeZoneId}
                      inFlight={inFlight}
                      errors={errors}
                      onDone={(t) => void onDone(t)}
                      onUndo={onUndo}
                      onSkip={(task) => setSheet({ kind: 'skip', task })}
                      onObserve={(task) => setSheet({ kind: 'observe', task })}
                      onIncident={(task) => setSheet({ kind: 'incident', task })}
                    />
                  ))}
                </div>
              )}

              {sheet?.kind === 'observe' && (
                <ObservationSheet
                  open
                  onOpenChange={(o) => !o && setSheet(null)}
                  stayId={sheet.task.stayId}
                  catId={sheet.task.catId}
                  catName={sheet.task.catName}
                  notEating={notEatingOf(sheet.task.catId)}
                />
              )}
              {sheet?.kind === 'incident' && (
                <IncidentForm
                  open
                  onOpenChange={(o) => !o && setSheet(null)}
                  stayId={sheet.task.stayId}
                  cats={catsOf(sheet.task.stayId)}
                  initialCatId={sheet.task.catId}
                />
              )}
              {sheet?.kind === 'media' && (
                <MediaRecordDialog
                  open
                  onOpenChange={(o) => !o && setSheet(null)}
                  stayId={sheet.task.stayId}
                  catId={sheet.task.catId}
                  catName={sheet.task.catName}
                  zone={data.branchTimeZoneId}
                />
              )}
              {sheet?.kind === 'skip' && (
                <ReasonDialog
                  open
                  onOpenChange={(o) => !o && setSheet(null)}
                  title={`Skip “${sheet.task.title}”?`}
                  consequence={`${sheet.task.catName}’s task will be marked skipped, not done, with the reason you give.`}
                  reasonLabel="Why is it being skipped?"
                  requiredMessage="Enter a reason, such as “refused food”."
                  confirmLabel="Skip the task"
                  onConfirm={(reason) => skip.mutateAsync({ taskId: sheet.task.id, reason })}
                />
              )}
            </>
          );
        }}
      </QueryGate>
    </div>
  );
}
