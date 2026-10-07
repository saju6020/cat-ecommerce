import { apiFetch, ApiUnauthorizedError } from './client';
import type { SignInRequest } from './auth';

/**
 * Staff authentication and session endpoints — HO-ACC-002 §8.2's `lib/api/staff-auth.ts`. Shapes
 * verified against `src/Api/.../Controllers/{StaffAuth,StaffSession}Controller.cs` and
 * `src/Read/.../ViewModels/StaffSessionStatusViewModel.cs`.
 */

export type { SignInRequest };

export interface StaffSignInResult {
  staffAccountId: string;
  expiresOn: string;
}

export async function signInStaff(request: SignInRequest): Promise<StaffSignInResult> {
  return apiFetch<StaffSignInResult>('/api/staff/auth/sign-in', {
    method: 'POST',
    body: request,
  });
}

export async function signOutStaff(): Promise<void> {
  await apiFetch<void>('/api/staff/auth/sign-out', { method: 'POST' });
}

export async function requestStaffPasswordReset(identifier: string): Promise<void> {
  await apiFetch<{ message: string }>('/api/staff/auth/password-reset/request', {
    method: 'POST',
    body: { identifier },
  });
}

/** `StaffSessionStatusViewModel` minus its `[JsonIgnore]`d `CsrfTokenHash`. */
export interface StaffSessionStatus {
  staffAccountId: string;
  fullName: string;
  roleIds: string[];
  issuedOn: string;
  lastActivityOn: string;
  idleExpiresOn: string;
  absoluteExpiresOn: string;
  idleTimeoutMinutes: number;
  serverTime: string;
}

/**
 * ARCH gate HIGH 1 (first pass) — a `401 session_idle_timeout` is not "signed out". §8.4: "do not
 * clear and do not navigate — the idle lock takes over" (ADR-FE-010, the page must stay mounted).
 * Thrown, not swallowed to `null` like every other `401` here, precisely so `StaffSessionGuard`
 * and `IdleLock` can each tell it apart from a real "no session" (`session_invalid`, or none at
 * all) — collapsing both into the same `null` is exactly what sent an idle-timed-out staff member
 * back to `/staff/sign-in`, unmounting `IdleLock` and every open form along with it.
 */
export class StaffSessionIdleTimeoutError extends Error {
  readonly idleTimeoutMinutes: number | undefined;

  constructor(idleTimeoutMinutes: number | undefined) {
    super('Staff session idle timeout');
    this.name = 'StaffSessionIdleTimeoutError';
    this.idleTimeoutMinutes = idleTimeoutMinutes;
  }
}

/** `null` on a `401 session_invalid` — the normal "not signed in" outcome (§8.4), not a thrown
 *  error. A `401 session_idle_timeout` throws `StaffSessionIdleTimeoutError` instead (above) — the
 *  one case this function does NOT reduce to "no session". Also used by `IdleLock`'s countdown
 *  re-read, which must never itself extend the idle window — this is `GET`, matching
 *  `[PassiveSession]` on the real endpoint. */
export async function getStaffSessionStatus(): Promise<StaffSessionStatus | null> {
  try {
    return await apiFetch<StaffSessionStatus>('/api/staff/session', {
      skipSessionErrorHandling: true,
    });
  } catch (error) {
    if (error instanceof ApiUnauthorizedError) {
      if (error.body.error === 'session_idle_timeout') {
        throw new StaffSessionIdleTimeoutError(error.body.idleTimeoutMinutes);
      }
      return null;
    }
    throw error;
  }
}

/** `POST /api/staff/session/activity` — the heartbeat `IdleLock` sends at most once per 60s on
 *  real interaction (never on a timer, US-ACC-08). Returns the same status shape. */
export async function recordStaffActivity(): Promise<StaffSessionStatus | null> {
  try {
    return await apiFetch<StaffSessionStatus>('/api/staff/session/activity', { method: 'POST' });
  } catch (error) {
    if ((error as { name?: string }).name === 'ApiUnauthorizedError') return null;
    throw error;
  }
}
