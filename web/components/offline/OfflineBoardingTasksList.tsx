import type { SnapshotBoardingTaskItem } from '@/lib/api/offline';
import { timeOnlyInZone } from '@/lib/format/time';
import { humanizeCode } from '@/lib/offline/labels';
import { HighRiskNote } from './HighRiskNote';

function sortByDue(items: SnapshotBoardingTaskItem[]): SnapshotBoardingTaskItem[] {
  return [...items].sort((a, b) => Date.parse(a.dueOn ?? '') - Date.parse(b.dueOn ?? '') || 0);
}

/**
 * `components/offline/OfflineBoardingTasksList.tsx` — HO-XCT-002 §11.4. Today's boarding tasks from
 * the device copy: due time, task type and label, unit, cat, instructions, status and assignee.
 * Same read-only treatment as the appointments: no button of any kind.
 */
export function OfflineBoardingTasksList({
  items,
  timeZoneId,
}: {
  items: SnapshotBoardingTaskItem[];
  timeZoneId: string;
}) {
  const sorted = sortByDue(items);
  return (
    <section aria-labelledby="offline-boarding-heading" className="flex flex-col gap-3">
      <h2 id="offline-boarding-heading" className="font-display text-lg font-semibold text-ink">
        Today&rsquo;s boarding tasks{' '}
        <span className="text-sm font-normal text-ink-muted">
          {sorted.length} · sorted by due time
        </span>
      </h2>
      {sorted.length === 0 ? (
        <p className="rounded-[var(--radius-md)] border border-border bg-surface p-4 text-sm text-ink-muted">
          There were no boarding tasks for today when this copy was saved.
        </p>
      ) : (
        <ul className="flex flex-col gap-2">
          {sorted.map((item) => (
            <li
              key={item.taskId}
              className="grid gap-x-4 gap-y-1 rounded-[var(--radius-md)] border border-border bg-surface p-4 text-sm sm:grid-cols-[8rem_1fr_1fr]"
            >
              <p className="font-semibold text-ink">
                {item.dueOn ? timeOnlyInZone(item.dueOn, timeZoneId) : 'No time'}
              </p>
              <div>
                <p className="font-semibold text-ink">
                  {humanizeCode(item.taskType)} · {item.taskLabel}
                </p>
                <p className="text-ink-muted">
                  {item.catName}
                  {item.unitName ? ` · ${item.unitName}` : ''}
                </p>
                {item.catIsHighRisk && <HighRiskNote note={item.handlingNote} />}
                {item.instructions && <p className="text-ink">{item.instructions}</p>}
              </div>
              <div>
                <p className="text-ink">
                  <span className="sr-only">Status: </span>
                  {item.completedOn
                    ? `Done at ${timeOnlyInZone(item.completedOn, timeZoneId)}`
                    : humanizeCode(item.status)}
                </p>
                <p className="text-ink-muted">{item.assigneeName ?? 'Not assigned'}</p>
              </div>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
