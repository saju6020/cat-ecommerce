import { Button } from '@/components/ui/button';
import type { ClinicBranch } from '@/lib/api/booking';

/** `components/booking/BranchStep.tsx` — HO-APT-002 §11.1 step 3 (skipped with one branch).
 *  Shows address and phone so the choice is informed; the zone is named too (ADR-FE-014). */
export function BranchStep({
  branches,
  onSelect,
}: {
  branches: ClinicBranch[];
  onSelect: (branchId: string) => void;
}) {
  return (
    <section aria-labelledby="branch-step-heading" className="flex flex-col gap-3">
      <h2 id="branch-step-heading" className="text-sm font-semibold text-ink">
        Which clinic?
      </h2>
      <ul className="m-0 flex list-none flex-col gap-3 p-0">
        {branches.map((branch) => (
          <li
            key={branch.id}
            className="flex flex-wrap items-center justify-between gap-3 rounded-[var(--radius-md)] border border-border bg-surface p-4"
          >
            <div className="min-w-0 flex-1 text-sm">
              <p className="text-[15px] font-semibold text-ink">{branch.name}</p>
              {branch.address && <p className="mt-0.5 text-ink-muted">{branch.address}</p>}
              <p className="mt-0.5 text-ink-muted">
                {branch.phone ? `${branch.phone} · ` : ''}Times are in {branch.timeZoneId}
              </p>
            </div>
            <Button aria-label={`Choose ${branch.name}`} onClick={() => onSelect(branch.id)}>
              Choose
            </Button>
          </li>
        ))}
      </ul>
    </section>
  );
}
