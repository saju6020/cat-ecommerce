'use client';

import { useMemo, useState } from 'react';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { describeApiError } from '@/lib/api/errors';
import { formatDateOnly, todayInZone } from '@/lib/format/time';
import { DAYS_OF_WEEK, type DayOfWeekName, type Room } from '@/lib/api/admin-branches';
import type { ShiftSlot } from '@/lib/api/admin-rosters';
import { RosterSlotDayRow, type RosterSlot } from './RosterSlotDayRow';

type WeekSlots = Record<DayOfWeekName, RosterSlot[]>;

function toWeekSlots(slots: ShiftSlot[]): WeekSlots {
  const state = Object.fromEntries(DAYS_OF_WEEK.map((d) => [d, [] as RosterSlot[]])) as WeekSlots;
  for (const slot of slots) {
    state[slot.dayOfWeek] = [
      ...(state[slot.dayOfWeek] ?? []),
      { starts: slot.starts, ends: slot.ends, defaultRoomId: slot.defaultRoomId },
    ];
  }
  return state;
}

function toShiftSlots(state: WeekSlots): ShiftSlot[] {
  return DAYS_OF_WEEK.flatMap((day) =>
    (state[day] ?? []).map((slot) => ({ dayOfWeek: day, ...slot })),
  );
}

/** `components/roster/RosterPatternEditor.tsx` — HO-ADM-002 §11.4, US-ADM-03. Per staff member:
 *  current pattern and a scheduled next pattern with its effective date; "Apply now" replaces the
 *  current pattern immediately, "Schedule from {date}" sets the next one (must be after today in
 *  branch time, U-D3). */
export interface RosterPatternEditorProps {
  staffFullName: string;
  timeZoneId: string;
  currentPattern: ShiftSlot[];
  nextPattern: ShiftSlot[] | null;
  nextPatternEffectiveFrom: string | null;
  rooms: Room[];
  onSetPattern: (slots: ShiftSlot[], effectiveFrom: string | null) => Promise<unknown>;
  onCancelScheduled: () => Promise<unknown>;
  isPending: boolean;
}

export function RosterPatternEditor({
  staffFullName,
  timeZoneId,
  currentPattern,
  nextPattern,
  nextPatternEffectiveFrom,
  rooms,
  onSetPattern,
  onCancelScheduled,
  isPending,
}: RosterPatternEditorProps) {
  const today = todayInZone(timeZoneId);
  const initialWeek = useMemo(() => toWeekSlots(currentPattern), [currentPattern]);
  const [week, setWeek] = useState<WeekSlots>(initialWeek);
  const [applyChoice, setApplyChoice] = useState<'now' | 'schedule'>('now');
  const [effectiveFrom, setEffectiveFrom] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [cancelOpen, setCancelOpen] = useState(false);

  // NFR-USA-5 — this editor manages its own local state rather than react-hook-form (its "fields"
  // are a nested per-day slot structure `WeeklyHoursEditor` already models the same way), so its
  // own dirty check mirrors that component's: compare the working state to what it started from.
  const isDirty =
    JSON.stringify(week) !== JSON.stringify(initialWeek) || applyChoice === 'schedule';

  async function handleSave() {
    setError(null);
    setSaved(false);
    if (applyChoice === 'schedule' && (!effectiveFrom || effectiveFrom <= today)) {
      setError('Choose a date after today (in branch time) to schedule this pattern.');
      return;
    }
    try {
      await onSetPattern(toShiftSlots(week), applyChoice === 'schedule' ? effectiveFrom : null);
      setSaved(true);
    } catch (err) {
      setError(describeApiError(err));
    }
  }

  return (
    <div className="flex flex-col gap-4 rounded-[var(--radius-md)] border border-border p-4">
      <UnsavedChangesGuard when={isDirty && !saved} />
      <h2 className="text-sm font-semibold text-ink">{staffFullName}</h2>

      {nextPattern && nextPatternEffectiveFrom && (
        <div className="rounded-[var(--radius-sm)] border border-border bg-surface-sunken p-3">
          <p className="text-sm font-semibold text-ink">
            Scheduled next pattern from {formatDateOnly(nextPatternEffectiveFrom)}
          </p>
          <Button
            variant="secondary"
            size="sm"
            className="mt-2"
            onClick={() => setCancelOpen(true)}
          >
            Cancel scheduled pattern
          </Button>
        </div>
      )}

      {error && (
        <p role="alert" aria-live="polite" className="text-sm font-semibold text-danger-ink">
          {error}
        </p>
      )}
      {saved && (
        <p role="status" aria-live="polite" className="text-sm font-semibold text-ink">
          Saved.
        </p>
      )}

      <div className="flex flex-col gap-3">
        {DAYS_OF_WEEK.map((day) => (
          <RosterSlotDayRow
            key={day}
            day={day}
            timeZoneId={timeZoneId}
            slots={week[day] ?? []}
            rooms={rooms}
            onChange={(slots) => {
              setWeek((current) => ({ ...current, [day]: slots }));
              setSaved(false);
            }}
          />
        ))}
      </div>

      <fieldset className="flex flex-col gap-2">
        <legend className="text-sm font-semibold text-ink">Apply this pattern</legend>
        <label className="flex items-center gap-2 text-sm text-ink">
          <input
            type="radio"
            name="apply-choice"
            checked={applyChoice === 'now'}
            onChange={() => setApplyChoice('now')}
          />
          Apply now (replaces the current pattern)
        </label>
        <label className="flex items-center gap-2 text-sm text-ink">
          <input
            type="radio"
            name="apply-choice"
            checked={applyChoice === 'schedule'}
            onChange={() => setApplyChoice('schedule')}
          />
          Schedule this as next pattern from
        </label>
        {applyChoice === 'schedule' && (
          <input
            type="date"
            aria-label="Effective date"
            min={today}
            value={effectiveFrom}
            onChange={(e) => setEffectiveFrom(e.target.value)}
            className="w-fit rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
          />
        )}
      </fieldset>

      <Button
        type="button"
        onClick={handleSave}
        disabled={isPending}
        aria-busy={isPending}
        className="w-fit"
      >
        {isPending ? 'Saving…' : 'Save pattern'}
      </Button>

      <ConfirmDialog
        open={cancelOpen}
        onOpenChange={setCancelOpen}
        title="Cancel the scheduled pattern?"
        consequence={`${staffFullName} will keep their current pattern; the pattern scheduled from ${nextPatternEffectiveFrom ? formatDateOnly(nextPatternEffectiveFrom) : ''} will not take effect.`}
        confirmLabel="Cancel scheduled pattern"
        variant="destructive"
        onConfirm={async () => {
          await onCancelScheduled();
        }}
      />
    </div>
  );
}
