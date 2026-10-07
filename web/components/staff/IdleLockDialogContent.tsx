import { Button } from '@/components/ui/button';
import { DialogHeader, DialogTitle, DialogDescription } from '@/components/ui/dialog';
import { SignInForm } from '@/components/auth/SignInForm';
import type { UseMutationResult } from '@tanstack/react-query';
import type { StaffSignInResult } from '@/lib/api/staff-auth';
import type { CustomerSignInResult, SignInRequest } from '@/lib/api/auth';

/**
 * `components/staff/IdleLockDialogContent.tsx` — MEDIUM/LOW gate item 18 (first pass): split out
 * of `IdleLock.tsx` once it was still over §11's ~200-line guideline after the overlay-opacity gate
 * fix (item 5) put lines back on top of the original hook-removal shrink. Purely presentational —
 * `IdleLock` itself still owns every piece of state (`personChanged`, `idleTimeoutMinutes`, the
 * re-sign-in mutation) and passes it down; nothing here reads `useStaffSession()` or any other hook
 * directly (this is one of §11.3/§11.5/§11.9's own named exemptions from that rule, same as
 * `IdleLock` itself, but there is nothing left here that needs it once the state lives above).
 */
export function IdleLockDialogContent({
  personChanged,
  onDiscardAndReload,
  idleTimeoutMinutes,
  reSignInMutation,
  onReSignInSuccess,
}: {
  personChanged: boolean;
  onDiscardAndReload: () => void;
  idleTimeoutMinutes: number;
  reSignInMutation: Pick<
    UseMutationResult<StaffSignInResult, Error, SignInRequest>,
    'mutateAsync' | 'isPending' | 'error'
  >;
  onReSignInSuccess: (result: CustomerSignInResult | StaffSignInResult) => void;
}) {
  if (personChanged) {
    return (
      <>
        <DialogHeader>
          <DialogTitle>Signed in as a different person</DialogTitle>
          <DialogDescription>
            The previous work on this screen has been discarded.
          </DialogDescription>
        </DialogHeader>
        <Button onClick={onDiscardAndReload}>Continue</Button>
      </>
    );
  }

  return (
    <>
      <DialogHeader>
        {/* ARCH LOW (first pass) — `idleTimeoutMinutes` here is already resolved by the caller
            (the 401 that triggered THIS lock, falling back to the last successful GET, falling
            back to the real backend default) — see IdleLock.tsx's own comment for the precedence
            reasoning; this component only renders the final number. */}
        <DialogTitle>
          You were signed out after {idleTimeoutMinutes} minutes of inactivity
        </DialogTitle>
        <DialogDescription>
          Sign in again to continue — your unsaved work is still here.
        </DialogDescription>
      </DialogHeader>
      <SignInForm
        audience="staff"
        onSignIn={(values) => reSignInMutation.mutateAsync(values)}
        isPending={reSignInMutation.isPending}
        error={reSignInMutation.error}
        showForgotPasswordLink={false}
        onSuccess={onReSignInSuccess}
      />
    </>
  );
}
