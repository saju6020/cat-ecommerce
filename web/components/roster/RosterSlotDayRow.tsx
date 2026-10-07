'use client';

import { Button } from '@/components/ui/button';
import { Plus, X } from 'lucide-react';
import { timeFieldLabel } from '@/lib/format/time';
import type { Room } from '@/lib/api/admin-branches';

export interface RosterSlot {
  starts: string;
  ends: string;
  defaultRoomId: string | null;
}

/** `components/roster/RosterSlotDayRow.tsx` — one day of `RosterPatternEditor` (HO-ADM-002
 *  §11.4). Same 0–3-slots shape as `WeeklyHoursDayRow`, plus a default-room choice per slot. */
export interface RosterSlotDayRowProps {
  day: string;
  timeZoneId: string;
  slots: RosterSlot[];
  rooms: Room[];
  onChange: (slots: RosterSlot[]) => void;
}

export function RosterSlotDayRow({
  day,
  timeZoneId,
  slots,
  rooms,
  onChange,
}: RosterSlotDayRowProps) {
  function update(index: number, field: keyof RosterSlot, value: string | null) {
    onChange(slots.map((slot, i) => (i === index ? { ...slot, [field]: value } : slot)));
  }
  function addSlot() {
    onChange([...slots, { starts: '09:00', ends: '17:00', defaultRoomId: null }]);
  }
  function removeSlot(index: number) {
    onChange(slots.filter((_, i) => i !== index));
  }

  return (
    <fieldset className="flex flex-col gap-2 rounded-[var(--radius-md)] border border-border p-3">
      <legend className="px-1 text-sm font-semibold text-ink">{day}</legend>
      {slots.length === 0 && <p className="text-sm text-ink-muted">No shift</p>}
      {slots.map((slot, index) => (
        <div key={index} className="flex flex-wrap items-end gap-2">
          <div className="flex flex-col gap-1">
            <label
              htmlFor={`${day}-starts-${index}`}
              className="text-xs font-semibold text-ink-muted"
            >
              {timeFieldLabel('Starts', timeZoneId)}
            </label>
            <input
              id={`${day}-starts-${index}`}
              type="time"
              value={slot.starts}
              onChange={(e) => update(index, 'starts', e.target.value)}
              className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-2 py-1.5 text-sm text-ink"
            />
          </div>
          <div className="flex flex-col gap-1">
            <label
              htmlFor={`${day}-ends-${index}`}
              className="text-xs font-semibold text-ink-muted"
            >
              {timeFieldLabel('Ends', timeZoneId)}
            </label>
            <input
              id={`${day}-ends-${index}`}
              type="time"
              value={slot.ends}
              onChange={(e) => update(index, 'ends', e.target.value)}
              className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-2 py-1.5 text-sm text-ink"
            />
          </div>
          <div className="flex flex-col gap-1">
            <label
              htmlFor={`${day}-room-${index}`}
              className="text-xs font-semibold text-ink-muted"
            >
              Default room
            </label>
            <select
              id={`${day}-room-${index}`}
              value={slot.defaultRoomId ?? ''}
              onChange={(e) => update(index, 'defaultRoomId', e.target.value || null)}
              className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-2 py-1.5 text-sm text-ink"
            >
              <option value="">None</option>
              {rooms.map((room) => (
                <option key={room.id} value={room.id}>
                  {room.name}
                </option>
              ))}
            </select>
          </div>
          <Button
            type="button"
            variant="ghost"
            size="icon"
            aria-label={`Remove this shift on ${day}`}
            onClick={() => removeSlot(index)}
          >
            <X size={14} aria-hidden="true" />
          </Button>
        </div>
      ))}
      {slots.length < 3 && (
        <Button type="button" variant="secondary" size="sm" className="w-fit" onClick={addSlot}>
          <Plus size={12} aria-hidden="true" />
          Add shift
        </Button>
      )}
    </fieldset>
  );
}
