'use client';

import { Badge } from '@/components/ui/badge';
import { FormFieldRow } from '@/components/cat/FormFieldRow';
import { SelectRow } from '@/components/boarding/shared/FormBits';
import { cn } from '@/lib/utils';

export type TaskStatusFilter = '' | 'Due' | 'Overdue' | 'Done';

/** `components/boarding/tasks/TaskBoardToolbar.tsx` — HO-BRD-002 §11.7: the date (today by
 *  default), the **My tasks** toggle, the unit filter, the status filter and the counts. All
 *  controls are at least 44 px and every toggle states its state in words (`aria-pressed`). */
export function TaskBoardToolbar({
  date,
  onDate,
  mine,
  onMine,
  unitId,
  onUnit,
  units,
  status,
  onStatus,
  counts,
}: {
  date: string;
  onDate: (date: string) => void;
  mine: boolean;
  onMine: (mine: boolean) => void;
  unitId: string;
  onUnit: (unitId: string) => void;
  units: Array<{ unitId: string; unitName: string }>;
  status: TaskStatusFilter;
  onStatus: (status: TaskStatusFilter) => void;
  counts: { due: number; overdue: number; done: number };
}) {
  const seg = (on: boolean) =>
    cn(
      'min-h-11 px-3 text-sm font-semibold first:rounded-l-[var(--radius-sm)] last:rounded-r-[var(--radius-sm)] border border-border-strong',
      on ? 'bg-accent text-white' : 'bg-surface text-ink',
    );
  return (
    <div
      role="toolbar"
      aria-label="Task filters"
      className="flex flex-wrap items-end gap-3 rounded-[var(--radius-md)] border border-border bg-surface p-3"
    >
      <FormFieldRow
        id="task-date"
        label="Date"
        type="date"
        value={date}
        onChange={(e) => onDate(e.target.value)}
        className="[&_input]:min-h-11"
      />
      <div role="group" aria-label="Whose tasks" className="flex">
        <button
          type="button"
          className={seg(mine)}
          aria-pressed={mine}
          onClick={() => onMine(true)}
        >
          My tasks
        </button>
        <button
          type="button"
          className={seg(!mine)}
          aria-pressed={!mine}
          onClick={() => onMine(false)}
        >
          Everyone
        </button>
      </div>
      <SelectRow
        id="task-unit"
        label="Unit"
        value={unitId}
        onChange={(e) => onUnit(e.target.value)}
        className="[&_select]:min-h-11"
      >
        <option value="">All units</option>
        {units.map((u) => (
          <option key={u.unitId} value={u.unitId}>
            {u.unitName}
          </option>
        ))}
      </SelectRow>
      <div role="group" aria-label="Status" className="flex">
        {(['', 'Due', 'Overdue', 'Done'] as const).map((s) => (
          <button
            key={s || 'all'}
            type="button"
            className={seg(status === s)}
            aria-pressed={status === s}
            onClick={() => onStatus(s)}
          >
            {s || 'All'}
          </button>
        ))}
      </div>
      <div className="ml-auto flex gap-2" aria-label="Counts">
        <Badge variant="neutral">{counts.due} due</Badge>
        <Badge variant="danger">{counts.overdue} overdue</Badge>
        <Badge variant="success">{counts.done} done</Badge>
      </div>
    </div>
  );
}
