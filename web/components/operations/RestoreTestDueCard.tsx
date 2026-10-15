import { CircleAlert, CircleCheck } from 'lucide-react';
import type { BackupStatus } from '@/lib/api/operations';
import { formatDateOnly } from '@/lib/format/time';

/**
 * `components/operations/RestoreTestDueCard.tsx` — HO-XCT-002 §11.5, U-B11. When the next quarterly
 * restore test is due, and an Overdue state that is impossible to miss: a bordered alert with an
 * icon and words, not a colour change.
 */
export function RestoreTestDueCard({ status }: { status: BackupStatus }) {
  const never = status.lastRestoreTest === null;
  if (status.isRestoreTestOverdue) {
    return (
      <section
        role="alert"
        aria-labelledby="restore-due-title"
        className="flex items-start gap-3 rounded-[var(--radius-md)] border-2 border-danger-ink bg-danger-bg p-4 text-danger-ink"
      >
        <CircleAlert size={22} className="mt-0.5 flex-none" aria-hidden="true" />
        <div>
          <h2 id="restore-due-title" className="text-[15px] font-bold">
            Restore test overdue
          </h2>
          <p className="text-sm">
            {never
              ? 'No restore test has ever been recorded. '
              : `The last passed test was on ${status.lastPassedRestoreTestOn ? formatDateOnly(status.lastPassedRestoreTestOn) : 'an earlier date'}. `}
            It was due on {formatDateOnly(status.nextRestoreTestDueOn)}. Run the restore drill and
            record it below.
          </p>
        </div>
      </section>
    );
  }
  return (
    <section
      aria-labelledby="restore-due-title"
      className="flex items-start gap-3 rounded-[var(--radius-md)] border border-border bg-surface p-4"
    >
      <CircleCheck size={20} className="mt-0.5 flex-none text-accent-ink" aria-hidden="true" />
      <div>
        <h2 id="restore-due-title" className="text-[15px] font-semibold text-ink">
          Next restore test due {formatDateOnly(status.nextRestoreTestDueOn)}
        </h2>
        <p className="text-sm text-ink-muted">A restore test is recorded every quarter.</p>
      </div>
    </section>
  );
}
