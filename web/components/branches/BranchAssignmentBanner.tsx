import Link from 'next/link';
import { TriangleAlert } from 'lucide-react';
import type { BranchAssignmentErrorCode } from '@/lib/api/errors';

/** `components/branches/BranchAssignmentBanner.tsx` — HO-ADM-002 §11.9. Shown instead of
 *  `PermissionDeniedState` when a `403` is actually `BranchNotAssigned`/`BranchAssignmentRequired`
 *  — the caller holds the action, they just need a branch. */
export function BranchAssignmentBanner({ code }: { code: BranchAssignmentErrorCode }) {
  return (
    <div
      role="alert"
      className="flex items-start gap-3 rounded-[var(--radius-md)] border border-border bg-surface p-6"
    >
      <TriangleAlert size={20} className="mt-0.5 flex-none text-warning-ink" aria-hidden="true" />
      <div>
        <p className="text-[15px] font-semibold text-ink">
          {code === 'BranchNotAssigned'
            ? "This isn't one of your assigned branches."
            : "You're not assigned to a branch yet."}
        </p>
        <p className="mt-1 text-sm text-ink-muted">
          {code === 'BranchNotAssigned' ? (
            <>Choose one of your assigned branches from the branch picker in the header.</>
          ) : (
            <>Ask an administrator to assign you to a branch.</>
          )}
        </p>
        {code === 'BranchNotAssigned' && (
          <Link
            href="/staff/admin/branches"
            className="mt-2 inline-block text-sm font-semibold text-accent-ink underline"
          >
            View branches
          </Link>
        )}
      </div>
    </div>
  );
}
