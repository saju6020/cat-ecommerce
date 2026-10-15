'use client';

import { useEffect, useRef, useState } from 'react';
import { CalendarClock, Clock } from 'lucide-react';
import { describeReportError } from '@/lib/api/errors';
import type { SchedulePreview, SchedulePreviewInput } from '@/lib/api/report-schedules';
import { formatInstantInZone, formatReportDate, formatRunInstant } from '@/lib/format/report';
import { useDebouncedValue } from '@/lib/hooks/useDebouncedValue';
import { useSchedulePreview } from '@/lib/hooks/useReportSchedules';

export const PREVIEW_DEBOUNCE_MS = 500;

/**
 * `components/report-schedules/NextRunPreview.tsx` — ⭐ HO-RPT-002 §11.5, ADR-FE-136. Posts the
 * draft's TIMING to `…/preview` (debounced, one call per settled change) and shows exactly what the
 * server answers: the next three runs, the period the next one covers, and any quiet-hours hold.
 * The page never computes a recurrence, a DST-correct instant or a covered period. `timeZoneId` only
 * labels the instants the server sent.
 */
export function NextRunPreview({
  input,
  timeZoneId,
}: {
  input: SchedulePreviewInput | null;
  timeZoneId: string;
}) {
  const key = input ? JSON.stringify(input) : '';
  const settled = useDebouncedValue(key, PREVIEW_DEBOUNCE_MS);
  const preview = useSchedulePreview();
  const [last, setLast] = useState<SchedulePreview | null>(null);
  const mutate = useRef(preview.mutate);
  mutate.current = preview.mutate;

  useEffect(() => {
    if (!settled) return;
    mutate.current(JSON.parse(settled) as SchedulePreviewInput, { onSuccess: setLast });
  }, [settled]);

  const error = preview.isError ? describeReportError(preview.error) : null;

  return (
    <section
      aria-labelledby="next-runs-heading"
      aria-live="polite"
      className="rounded-[var(--radius-md)] border border-border bg-surface p-4"
    >
      <h2 id="next-runs-heading" className="mb-2 text-sm font-bold text-ink">
        Next runs ({timeZoneId})
      </h2>
      {!input && (
        <p className="m-0 text-sm text-ink-muted">
          Choose a report and a period to see the next runs.
        </p>
      )}
      {error && (
        <p role="alert" className="m-0 text-sm font-semibold text-danger-ink">
          {error}
        </p>
      )}
      {input && !error && !last && (
        <p className="m-0 text-sm text-ink-muted">{preview.isPending ? 'Working it out…' : ''}</p>
      )}
      {last && !error && (
        <div className="flex flex-col gap-2">
          <ul className="m-0 flex list-none flex-col gap-1 p-0 text-sm text-ink">
            {last.nextRuns.map((run) => (
              <li key={run} className="flex items-center gap-2">
                <CalendarClock size={14} aria-hidden="true" />
                {formatRunInstant(run, timeZoneId)}
              </li>
            ))}
          </ul>
          <p className="m-0 text-sm text-ink">
            The next one covers{' '}
            <strong>
              {formatReportDate(last.periodForNextRun.from)} to{' '}
              {formatReportDate(last.periodForNextRun.to)}
            </strong>
            .
          </p>
          {last.deliveryHeldUntil && (
            <p className="m-0 flex items-start gap-2 rounded-[var(--radius-sm)] bg-warning-bg p-2 text-sm text-warning-ink">
              <Clock size={14} className="mt-0.5 flex-none" aria-hidden="true" />
              Quiet hours mean it will be delivered at{' '}
              {formatInstantInZone(last.deliveryHeldUntil, timeZoneId)}.
            </p>
          )}
          <p className="m-0 text-xs text-ink-muted">
            Worked out by the server each time you change the form.
          </p>
        </div>
      )}
    </section>
  );
}
