import { useMutation } from '@tanstack/react-query';
import {
  signInStaff,
  signOutStaff,
  requestStaffPasswordReset,
  recordStaffActivity,
} from '@/lib/api/staff-auth';
import type { SignInRequest } from '@/lib/api/auth';

/** Staff-facing auth mutations (US-ACC-14). */

// ARCHITECTURE gate fifth pass, MEDIUM — this mutation is also `IdleLock`'s own re-sign-in escape
// hatch (`components/staff/IdleLock.tsx`), the ONE operation that must always be able to reach the
// network regardless of what TanStack's `onlineManager` currently reports. While locked,
// `IdleLockContext.tsx` deliberately freezes `onlineManager`'s listener (so a real reconnect can no
// longer update it) — if the terminal was already offline at the instant the lock took hold,
// `onlineManager.isOnline()` stays frozen `false` for the rest of the lock, and the default
// `networkMode: 'online'` would then refuse to even start this mutation once the terminal genuinely
// reconnects (it stays paused, waiting for a notification the frozen listener can never deliver) —
// `unlock()` runs only after this mutation succeeds, so the lock could never lift. `networkMode:
// 'always'` makes it run unconditionally, exactly as if no lock were in play; a genuinely offline
// attempt still fails the ordinary way (the browser's own `fetch` rejects), it just never silently
// pauses first. This also applies to the plain staff sign-in page
// (`app/(auth)/staff/sign-in/page.tsx`, this hook's other caller) — a deliberate, harmless side
// effect: a sign-in a user just explicitly submitted should fail fast on a real network error rather
// than hang indefinitely behind a stale "offline" reading.
export function useSignInStaff() {
  return useMutation({
    mutationFn: (request: SignInRequest) => signInStaff(request),
    networkMode: 'always',
  });
}

export function useSignOutStaff() {
  return useMutation({
    mutationFn: () => signOutStaff(),
  });
}

export function useRequestStaffPasswordReset() {
  return useMutation({
    mutationFn: (identifier: string) => requestStaffPasswordReset(identifier),
  });
}

/** `IdleLock`'s activity heartbeat (US-ACC-08) — at most once per 60s, on real interaction only. */
export function useRecordStaffActivity() {
  return useMutation({
    mutationFn: () => recordStaffActivity(),
  });
}
