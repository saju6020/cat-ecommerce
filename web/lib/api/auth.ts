import { apiFetch } from './client';

/**
 * Customer-facing authentication endpoints — HO-ACC-002 §8.2's `lib/api/auth.ts` (captcha,
 * register, customer sign-in/out/session, reset request/confirm). Shapes mirror HO-ACC-001 §12.3
 * / §12.4 exactly, verified against the real `src/Api/.../Controllers/{Auth,Captcha,Customers}
 * Controller.cs`, `src/Read/.../ViewModels/CustomerSessionViewModel.cs` and
 * `src/Api/.../Contracts/Account/*.cs` — not just the handover prose. Following the
 * `web/README.md` "Adding a functional area" convention (domain types live with the endpoint that
 * returns them, not in the shared `lib/api/types.ts`), which supersedes HO-ACC-002 §11.1's literal
 * "transcribe into lib/api/types.ts" instruction — see the handover-disagreement note in this
 * slice's final report.
 */

// -------------------------------------------------------------------------------------------
// CAPTCHA — ADR-029
// -------------------------------------------------------------------------------------------

export interface CaptchaChallenge {
  captchaId: string;
  imageDataUrl: string;
  expiresInSeconds: number;
}

export async function getCaptcha(): Promise<CaptchaChallenge> {
  return apiFetch<CaptchaChallenge>('/api/captcha', { method: 'POST' });
}

// -------------------------------------------------------------------------------------------
// Registration — C1, US-ACC-01
// -------------------------------------------------------------------------------------------

export interface RegisterCustomerRequest {
  fullName: string;
  mobileNumber?: string | null;
  emailAddress?: string | null;
  password: string;
  captchaId?: string | null;
  captchaAnswer?: string | null;
}

export async function registerCustomer(
  request: RegisterCustomerRequest,
): Promise<{ customerId: string }> {
  return apiFetch<{ customerId: string }>('/api/customers', { method: 'POST', body: request });
}

// -------------------------------------------------------------------------------------------
// Sign-in / sign-out / session — C2/C3/Q-C1, US-ACC-04
// -------------------------------------------------------------------------------------------

export interface SignInRequest {
  identifier: string;
  password: string;
}

export interface CustomerSignInResult {
  customerId: string;
  expiresOn: string;
}

export async function signInCustomer(request: SignInRequest): Promise<CustomerSignInResult> {
  return apiFetch<CustomerSignInResult>('/api/auth/sign-in', { method: 'POST', body: request });
}

export async function signOutCustomer(): Promise<void> {
  await apiFetch<void>('/api/auth/sign-out', { method: 'POST' });
}

/** `CustomerSessionViewModel` minus its `[JsonIgnore]`d `CsrfTokenHash` (never serialised). */
export interface CustomerSession {
  customerId: string;
  fullName: string;
  expiresOn: string;
  recordStatus: 'Complete' | 'Incomplete';
}

/** `null` on a `401` — the normal "not signed in" outcome, not a thrown error (§8.4). */
export async function getCustomerSession(): Promise<CustomerSession | null> {
  try {
    return await apiFetch<CustomerSession>('/api/auth/session', {
      skipSessionErrorHandling: true,
    });
  } catch (error) {
    if ((error as { name?: string }).name === 'ApiUnauthorizedError') return null;
    throw error;
  }
}

// -------------------------------------------------------------------------------------------
// Password reset — C4/C5, US-ACC-05
// -------------------------------------------------------------------------------------------

export interface PasswordResetRequestPayload {
  identifier: string;
}

/** R3 — always `200`, one neutral body never rendered verbatim (ADR-FE-007). */
export async function requestCustomerPasswordReset(
  request: PasswordResetRequestPayload,
): Promise<void> {
  await apiFetch<{ message: string }>('/api/auth/password-reset/request', {
    method: 'POST',
    body: request,
  });
}

export interface ConfirmPasswordResetRequest {
  token: string;
  newPassword: string;
}

/** One endpoint for both customer and staff tokens — the token itself decides (HO-ACC-001 §12.4). */
export async function confirmPasswordReset(request: ConfirmPasswordResetRequest): Promise<void> {
  await apiFetch<void>('/api/auth/password-reset/confirm', { method: 'POST', body: request });
}
