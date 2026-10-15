'use client';

import { useMyBranches, useActiveBranchId, useSetActiveBranch } from '@/lib/hooks/useActiveBranch';
import { useAdminBranches } from '@/lib/hooks/useAdminBranches';

/**
 * `components/branches/ActiveBranchPicker.tsx` — HO-ADM-002 §11.2, ADR-FE-016. Hidden when the
 * signed-in staff member has one branch (or none). Changing it resets every branch-scoped query
 * (`useSetActiveBranch`).
 *
 * Names are read from `useAdminBranches()` (the caller's own branch list) rather than duplicated
 * here — falls back to the bare id if that list hasn't loaded yet, never blocking the picker on a
 * second request.
 *
 * ADM-002-FU2: the row is `w-full max-w-md` with `min-w-0` on the select, so a long branch name
 * is truncated inside the select instead of widening the page (UI-1, no sideways scroll at 360px).
 * The label never shrinks; the full name stays in each option's text and in the select's `title`.
 */
export function ActiveBranchPicker() {
  const { data: myBranches } = useMyBranches();
  const { data: allBranches } = useAdminBranches();
  const activeBranchId = useActiveBranchId();
  const setActiveBranch = useSetActiveBranch();

  const branchIds = myBranches?.branchIds ?? [];
  if (branchIds.length <= 1) return null;

  const selectedId = activeBranchId ?? myBranches?.primaryBranchId ?? '';
  const nameFor = (id: string) => allBranches?.find((b) => b.id === id)?.name ?? id;

  return (
    <div className="mb-4 flex w-full min-w-0 max-w-md items-center gap-2 text-sm">
      <label htmlFor="active-branch-picker" className="flex-shrink-0 font-semibold text-ink-muted">
        Branch
      </label>
      <select
        id="active-branch-picker"
        value={selectedId}
        title={nameFor(selectedId)}
        onChange={(event) => setActiveBranch(event.target.value)}
        className="min-w-0 flex-1 truncate rounded-[var(--radius-sm)] border border-border-strong bg-surface px-2 py-1.5 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
      >
        {branchIds.map((id) => (
          <option key={id} value={id}>
            {nameFor(id)}
          </option>
        ))}
      </select>
    </div>
  );
}
