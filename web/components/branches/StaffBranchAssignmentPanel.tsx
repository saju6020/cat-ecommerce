'use client';

import { useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { LoadingState } from '@/components/common/LoadingState';
import { ErrorState } from '@/components/common/ErrorState';
import { describeApiError } from '@/lib/api/errors';
import type {
  BranchSummary,
  StaffBranchAssignment,
  AssignStaffBranchesRequest,
} from '@/lib/api/admin-branches';

/**
 * `components/branches/StaffBranchAssignmentPanel.tsx` — HO-ADM-002 §11.2, US-ADM-01. Added to
 * HO-ACC-002's staff-account detail page (§13's own scoped exception). Checkbox list of active
 * branches, plus a primary radio (must be one of the checked branches). ARCH gate precedent
 * (HO-ACC-002 §8.2 rule 4) — takes data/callbacks as props; the page wires
 * `useAdminBranches()`/`useStaffBranchAssignment()`/`useAssignStaffBranches()`.
 */
export interface StaffBranchAssignmentPanelProps {
  branches: {
    data: BranchSummary[] | undefined;
    isLoading: boolean;
    isError: boolean;
    refetch: () => void;
  };
  assignment: {
    data: StaffBranchAssignment | undefined;
    isLoading: boolean;
    isError: boolean;
    refetch: () => void;
  };
  onSave: (request: AssignStaffBranchesRequest) => Promise<unknown>;
  isPending: boolean;
}

export function StaffBranchAssignmentPanel({
  branches,
  assignment,
  onSave,
  isPending,
}: StaffBranchAssignmentPanelProps) {
  const [branchIds, setBranchIds] = useState<string[]>([]);
  const [primaryBranchId, setPrimaryBranchId] = useState<string>('');
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);

  useEffect(() => {
    if (!assignment.data) return;
    setBranchIds(assignment.data.branchIds);
    setPrimaryBranchId(assignment.data.primaryBranchId ?? assignment.data.branchIds[0] ?? '');
  }, [assignment.data]);

  if (branches.isLoading || assignment.isLoading) {
    return <LoadingState rows={3} label="Loading branch assignment…" />;
  }
  if (branches.isError || assignment.isError) {
    return (
      <ErrorState
        description="We couldn't load branch assignment just now."
        onRetry={() => {
          branches.refetch();
          assignment.refetch();
        }}
      />
    );
  }

  const activeBranches = (branches.data ?? []).filter((b) => b.isActive);
  // The chosen primary only counts while its branch is still assigned; otherwise the first one is.
  const effectivePrimaryId = branchIds.includes(primaryBranchId)
    ? primaryBranchId
    : (branchIds[0] ?? '');

  function toggle(id: string) {
    setSaved(false);
    // ADM backlog 30: a pure updater — the primary is DERIVED below (never set from inside this
    // updater, never read from a stale closure), so batched toggles can't leave it inconsistent.
    setBranchIds((current) =>
      current.includes(id) ? current.filter((b) => b !== id) : [...current, id],
    );
  }

  async function handleSave() {
    setError(null);
    setSaved(false);
    if (branchIds.length === 0 || !effectivePrimaryId) {
      setError('Select at least one branch and a primary branch.');
      return;
    }
    try {
      await onSave({ branchIds, primaryBranchId: effectivePrimaryId });
      setSaved(true);
    } catch (err) {
      setError(describeApiError(err));
    }
  }

  return (
    <div className="flex max-w-md flex-col gap-3 rounded-[var(--radius-md)] border border-border p-4">
      <h2 className="text-sm font-semibold text-ink">Branches</h2>

      {error && (
        <p role="alert" aria-live="polite" className="text-sm font-semibold text-danger-ink">
          {error}
        </p>
      )}
      {saved && (
        <p role="status" aria-live="polite" className="text-sm font-semibold text-ink">
          Branch assignment saved.
        </p>
      )}

      <fieldset className="flex flex-col gap-2">
        <legend className="sr-only">Assigned branches and primary branch</legend>
        {activeBranches.map((branch) => (
          <div key={branch.id} className="flex items-center justify-between gap-3">
            <label className="flex items-center gap-2 text-sm text-ink">
              <input
                type="checkbox"
                checked={branchIds.includes(branch.id)}
                onChange={() => toggle(branch.id)}
              />
              {branch.name}
            </label>
            <label className="flex items-center gap-1.5 text-xs text-ink-muted">
              <input
                type="radio"
                name="primary-branch"
                disabled={!branchIds.includes(branch.id)}
                checked={effectivePrimaryId === branch.id}
                onChange={() => setPrimaryBranchId(branch.id)}
              />
              Primary
            </label>
          </div>
        ))}
      </fieldset>

      <Button
        type="button"
        onClick={handleSave}
        disabled={isPending}
        aria-busy={isPending}
        className="w-fit"
      >
        {isPending ? 'Saving…' : 'Save branches'}
      </Button>
    </div>
  );
}
