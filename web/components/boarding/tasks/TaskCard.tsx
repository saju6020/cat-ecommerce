'use client';

import { CheckCircle2, Droplets, Eye, Pill, Sparkles, Utensils, Undo2 } from 'lucide-react';
import type { ReactNode } from 'react';
import { Button } from '@/components/ui/button';
import { OverdueChip } from '@/components/boarding/shared/StatusBadges';
import { TASK_KIND_LABEL } from '@/components/boarding/shared/labels';
import type { CareTask, CareTaskKind } from '@/lib/api/types';
import { timeOnlyInZone } from '@/lib/format/time';
import { cn } from '@/lib/utils';

const KIND_ICON: Record<CareTaskKind, ReactNode> = {
  Feed: <Utensils size={14} aria-hidden="true" />,
  Medication: <Pill size={14} aria-hidden="true" />,
  LitterChange: <Droplets size={14} aria-hidden="true" />,
  Enrichment: <Sparkles size={14} aria-hidden="true" />,
  ObservationCheck: <Eye size={14} aria-hidden="true" />,
};

/**
 * `components/boarding/tasks/TaskCard.tsx` — ⭐ HO-BRD-002 §11.7, NFR-USA-3, ADR-FE-100. The time, the
 * kind **with its name**, the server-written title, the cat, the assignee, an overdue chip (text,
 * from the server's `isOverdue`/`minutesLate`) and a full-width **Done** button at least 48 px tall.
 * One tap completes it: no dialog, no required note. While the request is in flight an **Undo**
 * replaces Done; a failure shows the server's reason on the card and puts it back. Skip, Observe and
 * Incident are the secondary actions. Every card is a list item inside a labelled list.
 */
export function TaskCard({
  task,
  zone,
  inFlight,
  error,
  onDone,
  onUndo,
  onSkip,
  onObserve,
  onIncident,
}: {
  task: CareTask;
  zone: string;
  inFlight: boolean;
  error?: string | null;
  onDone: (task: CareTask) => void;
  onUndo: (task: CareTask) => void;
  onSkip: (task: CareTask) => void;
  onObserve: (task: CareTask) => void;
  onIncident: (task: CareTask) => void;
}) {
  const done = task.status === 'Done';
  const skipped = task.status === 'Skipped';
  return (
    <li
      data-testid={`task-${task.id}`}
      className={cn(
        'rounded-[var(--radius-md)] border p-3',
        task.isOverdue ? 'border-danger-ink/40 bg-danger-bg/40' : 'border-border bg-surface',
        done && 'border-accent/30 bg-accent-soft',
      )}
    >
      <div className="flex items-center justify-between gap-2">
        <span className="text-[15px] font-bold tabular-nums text-ink">
          {timeOnlyInZone(task.dueOn, zone)}
        </span>
        {task.isOverdue && <OverdueChip minutesLate={task.minutesLate} />}
        {done && (
          <span className="flex items-center gap-1 text-xs font-semibold text-accent-ink">
            <CheckCircle2 size={14} aria-hidden="true" />
            Done {task.completedOn ? timeOnlyInZone(task.completedOn, zone) : ''}
            {task.completedByName ? ` · ${task.completedByName}` : ''}
            {inFlight ? ' · saving…' : ''}
          </span>
        )}
        {skipped && <span className="text-xs font-semibold text-ink-muted">Skipped</span>}
      </div>
      <p className="mt-1 flex items-center gap-1.5 text-xs font-bold text-ink-muted">
        {KIND_ICON[task.kind]} {TASK_KIND_LABEL[task.kind]}
      </p>
      <p className="mt-0.5 text-[15px] font-bold text-ink">{task.title}</p>
      <p className="text-xs text-ink-muted">
        {task.catName} · {task.assignedToName ?? 'Unassigned'}
      </p>
      {skipped && task.skipReason && (
        <p className="text-xs text-ink-muted">Reason: {task.skipReason}</p>
      )}
      {error && (
        <p
          role="alert"
          className="mt-2 rounded-[var(--radius-sm)] bg-danger-bg px-2.5 py-2 text-sm font-semibold text-danger-ink"
        >
          {error}
        </p>
      )}
      {task.status === 'Pending' && (
        <>
          <Button className="mt-2 min-h-12 w-full text-[15px]" onClick={() => onDone(task)}>
            Done
            <span className="sr-only">
              : {task.title} for {task.catName}
            </span>
          </Button>
          <div className="mt-1.5 grid grid-cols-3 gap-1.5">
            <Button variant="secondary" className="min-h-11" onClick={() => onSkip(task)}>
              Skip…
            </Button>
            <Button variant="secondary" className="min-h-11" onClick={() => onObserve(task)}>
              Observe
            </Button>
            <Button variant="secondary" className="min-h-11" onClick={() => onIncident(task)}>
              Incident
            </Button>
          </div>
        </>
      )}
      {done && inFlight && (
        <Button variant="secondary" className="mt-2 min-h-12 w-full" onClick={() => onUndo(task)}>
          <Undo2 size={16} aria-hidden="true" /> Undo
        </Button>
      )}
    </li>
  );
}
