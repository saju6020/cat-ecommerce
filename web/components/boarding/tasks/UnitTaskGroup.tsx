import { AlertTriangle, ShieldAlert } from 'lucide-react';
import { CatHeaderCompact } from '@/components/boarding/shared/CatHeaderCompact';
import type { CareTask, TaskUnitGroup } from '@/lib/api/types';
import { TaskCard } from './TaskCard';

type CardHandlers = Omit<
  React.ComponentProps<typeof TaskCard>,
  'task' | 'inFlight' | 'error' | 'zone'
>;

/**
 * `components/boarding/tasks/UnitTaskGroup.tsx` — ⭐ HO-BRD-002 §11.7, OQ-2: one section per unit,
 * expanded: the unit, its block, the cats with their compact headers (high-risk badge **first**,
 * handling note visible before any action — UI-7), the isolation banner where it applies, the
 * server's “Not eating — flagged” for a cat, and the unit's tasks as a labelled list.
 */
export function UnitTaskGroup({
  group,
  zone,
  inFlight,
  errors,
  ...handlers
}: {
  group: TaskUnitGroup;
  zone: string;
  inFlight: Record<string, boolean>;
  errors: Record<string, string>;
} & CardHandlers) {
  const headingId = `unit-${group.unitId}`;
  const flagged = group.cats.filter((c) => c.notEating);
  const sortedCats = [...group.cats].sort(
    (a, b) => Number(b.catIsHighRisk) - Number(a.catIsHighRisk),
  );
  return (
    <section
      aria-labelledby={headingId}
      className="overflow-hidden rounded-[var(--radius-md)] border border-border bg-surface"
    >
      <div className="flex items-center justify-between gap-2 border-b border-border bg-surface-sunken px-3.5 py-2.5">
        <h2 id={headingId} className="font-display text-lg font-semibold text-ink">
          {group.unitCode} · {group.unitName}
        </h2>
        <span className="text-xs text-ink-muted">
          {group.block ?? 'No block'} · {group.cats.length} cat{group.cats.length === 1 ? '' : 's'}
        </span>
      </div>
      {group.isIsolation && (
        <p className="flex items-center gap-2 border-b border-border bg-warning-bg px-3.5 py-2 text-sm font-semibold text-warning-ink">
          <ShieldAlert size={16} aria-hidden="true" />{' '}
          {group.isolationNote ?? 'Isolation — gloves and gown on entry.'}
        </p>
      )}
      {flagged.map((c) => (
        <p
          key={c.catId}
          role="status"
          className="flex items-center gap-2 border-b border-border bg-danger-bg px-3.5 py-2 text-sm font-bold text-danger-ink"
        >
          <AlertTriangle size={16} aria-hidden="true" /> Not eating — flagged: {c.catName}
        </p>
      ))}
      <ul
        aria-label={`Cats in ${group.unitName}`}
        className="flex flex-col gap-2 border-b border-border p-3"
      >
        {sortedCats.map((c) => (
          <li key={c.catId} className="rounded-[var(--radius-sm)] border border-border px-3 py-2">
            <CatHeaderCompact
              name={c.catName}
              isHighRisk={c.catIsHighRisk}
              handlingNote={c.handlingNote}
            />
          </li>
        ))}
      </ul>
      {group.tasks.length === 0 ? (
        <p className="p-3 text-sm text-ink-muted">
          No tasks match these filters for {group.unitName}.
        </p>
      ) : (
        <ul aria-label={`${group.unitName} tasks`} className="flex flex-col gap-2.5 p-3">
          {group.tasks.map((task: CareTask) => (
            <TaskCard
              key={task.id}
              task={task}
              zone={zone}
              inFlight={!!inFlight[task.id]}
              error={errors[task.id]}
              {...handlers}
            />
          ))}
        </ul>
      )}
    </section>
  );
}
