'use client';

import { use, useMemo, useState } from 'react';
import { useAdminBranch, useBranchRooms } from '@/lib/hooks/useAdminBranches';
import {
  useBranchRosters,
  useAvailability,
  useRosterExceptions,
  useSetRosterPattern,
  useCancelScheduledRosterPattern,
  useCreateRosterException,
  useCancelRosterException,
} from '@/lib/hooks/useAdminRosters';
import { useAdminLeaveRequests } from '@/lib/hooks/useLeave';
import { useStaffAccounts } from '@/lib/hooks/useStaffAdmin';
import { RosterWeekView, type RosterDayCell } from '@/components/roster/RosterWeekView';
import { RosterPatternEditor } from '@/components/roster/RosterPatternEditor';
import { RosterExceptionForm } from '@/components/roster/RosterExceptionForm';
import { LoadingState } from '@/components/common/LoadingState';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { ApiForbiddenError } from '@/lib/api/client';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { getWeekDates, shiftWeek, todayIso } from '@/lib/format/week';
import { timeOnlyInZone, localDateOf } from '@/lib/format/time';

/** `/staff/admin/branches/[branchId]/roster` — US-ADM-03. Week view (OQ-1: defaults to this
 *  week), pattern editor per staff member, and roster exceptions. Data from `.../availability`
 *  (unrestricted — shows shifts outside opening hours too, §8.5 rule 6) and `.../rosters`. */
export default function BranchRosterPage({ params }: { params: Promise<{ branchId: string }> }) {
  const { branchId } = use(params);
  const { data: branch } = useAdminBranch(branchId);
  const rosters = useBranchRosters(branchId);
  const rooms = useBranchRooms(branchId);
  const staffAccounts = useStaffAccounts({ pageSize: 100 });

  const [anchor, setAnchor] = useState(todayIso());
  const weekDates = useMemo(() => getWeekDates(anchor), [anchor]);
  const from = weekDates[0] ?? anchor;
  const to = weekDates[6] ?? anchor;

  const availability = useAvailability(branchId, { from, to, restrictToOpeningHours: false });
  const exceptions = useRosterExceptions(branchId, { from, to });
  const leave = useAdminLeaveRequests({ status: 'Approved', from, to, pageSize: 100 });

  const [selectedStaffId, setSelectedStaffId] = useState<string | null>(null);
  const [pendingCancelException, setPendingCancelException] = useState<string | null>(null);

  const setPatternMutation = useSetRosterPattern(branchId, selectedStaffId ?? '');
  const cancelPatternMutation = useCancelScheduledRosterPattern(branchId, selectedStaffId ?? '');
  const createExceptionMutation = useCreateRosterException(branchId);
  const cancelExceptionMutation = useCancelRosterException(branchId);

  const nameById = new Map((staffAccounts.data?.items ?? []).map((s) => [s.id, s.fullName]));
  const rows = (rosters.data ?? []).map((r) => ({
    staffAccountId: r.staffAccountId,
    staffFullName: nameById.get(r.staffAccountId) ?? r.staffAccountId,
  }));
  const selectedRoster = (rosters.data ?? []).find((r) => r.staffAccountId === selectedStaffId);

  function cellFor(staffAccountId: string, date: string): RosterDayCell {
    const zone = branch?.timeZoneId ?? 'UTC';
    const onLeave = (leave.data?.items ?? []).some(
      (l) => l.staffAccountId === staffAccountId && date >= l.startsOn && date <= l.endsOn,
    );
    const dayExceptions = (exceptions.data ?? []).filter(
      (e) => e.staffAccountId === staffAccountId && e.date === date,
    );
    const intervals = (availability.data ?? [])
      .filter((a) => a.staffAccountId === staffAccountId && localDateOf(a.start, zone) === date)
      .map((a) => ({
        label: `${timeOnlyInZone(a.start, zone)}–${timeOnlyInZone(a.end, zone)}`,
        outsideOpeningHours: a.outsideOpeningHours,
      }));
    return {
      intervals,
      isOnLeave: onLeave,
      exceptionLabels: dayExceptions.map((e) =>
        e.kind === 'ExtraShift' ? 'Extra shift' : 'Shift cancelled',
      ),
    };
  }

  // §18 A3: Rosters.View excludes Accountant and ShopManager, who can still reach the branch
  // pages (Branches.View covers all staff) — a rosters/availability/exceptions 403 is a
  // permission refusal, not a generic load failure.
  const refusal = [rosters, availability, exceptions]
    .map((query) => query.error)
    .find((error) => error instanceof ApiForbiddenError);
  if (refusal) return <AccessRefusedState error={refusal} />;

  if (!branch) return <LoadingState rows={6} />;

  return (
    <div className="flex flex-col gap-8">
      <h2 className="font-display text-lg font-semibold text-ink">Roster</h2>
      <p className="text-sm text-ink-muted">Times shown in {branch.timeZoneId}.</p>

      <RosterWeekView
        weekDates={weekDates}
        rows={rows}
        cellFor={cellFor}
        onPrevWeek={() => setAnchor((a) => shiftWeek(a, -1))}
        onNextWeek={() => setAnchor((a) => shiftWeek(a, 1))}
        onThisWeek={() => setAnchor(todayIso())}
        isLoading={rosters.isLoading || availability.isLoading}
        isError={rosters.isError || availability.isError}
        onRetry={() => {
          void rosters.refetch();
          void availability.refetch();
        }}
      />

      <div className="flex flex-col gap-3">
        <h2 className="font-display text-lg font-semibold text-ink">Shift patterns</h2>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="pattern-staff" className="text-sm font-semibold text-ink">
            Staff member
          </label>
          <select
            id="pattern-staff"
            value={selectedStaffId ?? ''}
            onChange={(e) => setSelectedStaffId(e.target.value || null)}
            className="w-fit rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
          >
            <option value="">Choose…</option>
            {rows.map((r) => (
              <option key={r.staffAccountId} value={r.staffAccountId}>
                {r.staffFullName}
              </option>
            ))}
          </select>
        </div>

        {selectedRoster && (
          <RosterPatternEditor
            staffFullName={
              nameById.get(selectedRoster.staffAccountId) ?? selectedRoster.staffAccountId
            }
            timeZoneId={branch.timeZoneId}
            currentPattern={selectedRoster.currentPattern}
            nextPattern={selectedRoster.nextPattern}
            nextPatternEffectiveFrom={selectedRoster.nextPatternEffectiveFrom}
            rooms={rooms.data ?? []}
            isPending={setPatternMutation.isPending || cancelPatternMutation.isPending}
            onSetPattern={(slots, effectiveFrom) =>
              setPatternMutation.mutateAsync({ slots, effectiveFrom })
            }
            onCancelScheduled={() => cancelPatternMutation.mutateAsync()}
          />
        )}
      </div>

      <div className="flex flex-col gap-3">
        <h2 className="font-display text-lg font-semibold text-ink">Roster exceptions</h2>
        <RosterExceptionForm
          timeZoneId={branch.timeZoneId}
          staffOptions={rows}
          isPending={createExceptionMutation.isPending}
          onCreate={(request) => createExceptionMutation.mutateAsync(request)}
        />
        <ul className="flex flex-col gap-2">
          {(exceptions.data ?? []).map((exception) => (
            <li
              key={exception.id}
              className="flex flex-wrap items-center justify-between gap-2 rounded-[var(--radius-md)] border border-border p-3 text-sm"
            >
              <span>
                {nameById.get(exception.staffAccountId) ?? exception.staffAccountId} ·{' '}
                {exception.date} ·{' '}
                {exception.kind === 'ExtraShift' ? 'Extra shift' : 'Shift cancelled'} (
                {exception.starts}–{exception.ends})
              </span>
              <button
                type="button"
                className="text-sm font-semibold text-danger-ink underline"
                onClick={() => setPendingCancelException(exception.id)}
              >
                Cancel
              </button>
            </li>
          ))}
        </ul>
      </div>

      <ConfirmDialog
        open={!!pendingCancelException}
        onOpenChange={(open) => !open && setPendingCancelException(null)}
        title="Cancel this exception?"
        consequence="This roster exception will be removed."
        confirmLabel="Cancel exception"
        variant="destructive"
        onConfirm={async () => {
          if (pendingCancelException)
            await cancelExceptionMutation.mutateAsync(pendingCancelException);
          setPendingCancelException(null);
        }}
      />
    </div>
  );
}
