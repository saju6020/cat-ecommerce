import { StaffShell } from '@/components/common/staff-shell';
import { StaffSessionGuard } from '@/components/staff/StaffSessionGuard';
import { IdleLock } from '@/components/staff/IdleLock';
import { StaffLockProvider } from '@/components/staff/IdleLockContext';
import { ActiveBranchPicker } from '@/components/branches/ActiveBranchPicker';

// Back-office shell for every /staff/** route (HO-CAT-002 §8.3). Thin by design (§8.2 rule 5) —
// the interactive shell itself lives in a client component; this layout only composes it.
//
// HO-ACC-002 §11.9, ADR-FE-004/010: `StaffSessionGuard` replaces `lib/identity.ts` for every
// existing and future staff page; `IdleLock` wraps everything below it so the page — and every
// form's react-hook-form state — stays mounted through a lock/re-authenticate cycle (US-ACC-08).
//
// ARCH gate HIGH (second pass) — `StaffLockProvider` sits ABOVE `StaffSessionGuard`, not inside
// `IdleLock`, so the guard can read whether the terminal is locked and, while it is, stop treating
// a `session_invalid`/`null` session read as "signed out" — see `IdleLockContext.tsx`'s own comment
// for why the lock state cannot live below the guard.
// HO-ADM-002 §8.1/§11.2, ADR-FE-016 — `ActiveBranchPicker` is rendered here (allowed scope: this
// file only) rather than inside `StaffShell`'s own header markup, which is out of this handover's
// §13 scope. It renders as the first thing above every staff page's own content instead of in the
// header row itself; it is still mounted on every `/staff/**` navigation (so the active-branch seed
// effect always runs), and still hidden outright with one branch or none (§11.2 unchanged).
export default function StaffLayout({ children }: { children: React.ReactNode }) {
  return (
    <StaffLockProvider>
      <StaffSessionGuard>
        <IdleLock>
          <StaffShell>
            <ActiveBranchPicker />
            {children}
          </StaffShell>
        </IdleLock>
      </StaffSessionGuard>
    </StaffLockProvider>
  );
}
