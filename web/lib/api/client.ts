import type { ValidationResponse } from './types';
import { getActiveBranchId } from '../branch';

/**
 * The typed fetch client (HO-CAT-002 §11.2). Responsibilities, and nothing beyond them:
 *  - base URL from NEXT_PUBLIC_API_BASE_URL
 *  - identity rides in HttpOnly session cookies (`pc_session` / `pc_staff_session` — HO-ACC-001
 *    §12.5 ADR-031, rule R7) that the browser attaches on its own once `credentials: 'include'`
 *    is set below; this client never reads or sets either cookie itself
 *  - on every unsafe method (POST/PUT/PATCH/DELETE) it reads the non-HttpOnly `pc_csrf` cookie
 *    and sends it back as the `X-CSRF-Token` header, or the API answers `403 csrf_token_invalid`
 *  - every request — reads included, not only unsafe methods — sends `X-Branch-Id`
 *    (HO-ADM-001 §8.3, ADR-046) whenever `lib/branch.ts`'s `getActiveBranchId()` returns one;
 *    see that module for why it is currently a documented no-op hook point rather than a real
 *    branch source
 *  - 200/201/204 -> parsed JSON (or undefined for an empty body)
 *  - 400 -> ApiValidationError carrying the ValidationResponse, so forms can map it per field
 *  - 404 -> ApiNotFoundError, so a page can render "not found" rather than an error boundary
 *  - 409 -> ApiConflictError (a concurrent edit) — the UI must offer a reload, never silently
 *    discard the user's input
 *  - anything else / 5xx -> ApiError with a message that is ours, never the server's (NFR-USA-4)
 *
 * This is the ONLY place in the app that calls `fetch` (HO-CAT-002 §8.2 rule 2). Endpoint
 * modules in `lib/api/<area>.ts` call `apiFetch`; components and `lib/hooks/` query/mutation
 * functions never call `fetch` directly.
 */

export class ApiError extends Error {
  readonly status: number;

  constructor(message: string, status: number) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
  }
}

export class ApiValidationError extends ApiError {
  readonly response: ValidationResponse;

  constructor(response: ValidationResponse) {
    super('Validation failed', 400);
    this.name = 'ApiValidationError';
    this.response = response;
  }
}

export class ApiNotFoundError extends ApiError {
  constructor() {
    super('Not found', 404);
    this.name = 'ApiNotFoundError';
  }
}

export class ApiConflictError extends ApiError {
  /** HO-APT-002 §11.8 (additive) — a `409` from the Appointments slot-claim endpoints carries a
   *  `ValidationResponse` naming the violation (`SlotNoLongerAvailable`, a stale calendar version…).
   *  Absent for every other `409`, which still has no usable body. */
  readonly response?: ValidationResponse;

  constructor(response?: ValidationResponse) {
    super('This record was changed by someone else.', 409);
    this.name = 'ApiConflictError';
    this.response = response;
  }
}

/**
 * HO-ACC-002 §11.2. The body of a `401` is one of two shapes (HO-ACC-001 §12.4 R1/R5, verified
 * against `AuthController`/`StaffAuthController`/`SessionAuthenticationMiddleware`):
 *  - a sign-in failure: `{ error: 'invalid_credentials', message }` — the one generic message,
 *    ADR-FE-007;
 *  - a session failure: `{ error: 'session_invalid' | 'session_idle_timeout', idleTimeoutMinutes?
 *    }`. **No `message` field is ever present on this shape** — verified against the real
 *    `SessionAuthenticationMiddleware.WriteRefusalAsync`/`AuthController.GetSession`/
 *    `StaffSessionController` bodies, which send only `error` and, for an idle timeout,
 *    `idleTimeoutMinutes`. HO-ACC-002 §11.1's `SessionError` names a `message` field that the
 *    real API never sends — a §20.7 contract disagreement, reported rather than shimmed: this
 *    type omits it, and every screen that shows session-failure copy uses its own client-owned
 *    text (ADR-FE-007), never a server-supplied message that doesn't exist on the wire.
 */
export interface SignInFailedBody {
  error: 'invalid_credentials';
  message: string;
}
export interface SessionErrorBody {
  error: 'session_invalid' | 'session_idle_timeout';
  idleTimeoutMinutes?: number;
}

export class ApiUnauthorizedError extends ApiError {
  readonly body: SignInFailedBody | SessionErrorBody;

  constructor(body: SignInFailedBody | SessionErrorBody) {
    super(body.error === 'invalid_credentials' ? body.message : 'Session is not valid.', 401);
    this.name = 'ApiUnauthorizedError';
    this.body = body;
  }
}

/**
 * HO-ACC-001 R6/R7: a `403` is either the CSRF refusal (`{ error: 'csrf_token_invalid' }`,
 * verified against `SessionAuthenticationMiddleware`) or the authorization refusal
 * (`{ errors: [{ errorCode: 'ActionNotPermitted', errorMessage }] }`, verified against
 * `RequireActionFilter` — note this is **not** a full `ValidationResponse`: there is no `isValid`,
 * `propertyName` or `resourceName` field on this particular body, unlike a `400`).
 */
export interface ForbiddenBody {
  error?: 'csrf_token_invalid';
  errors?: Array<{ errorCode: string; errorMessage: string }>;
}

export class ApiForbiddenError extends ApiError {
  readonly body: ForbiddenBody;

  constructor(body: ForbiddenBody) {
    super('You are not permitted to perform this action.', 403);
    this.name = 'ApiForbiddenError';
    this.body = body;
  }
}

/** HO-ACC-001 R4 — the body is a genuine `ValidationResponse` (verified against
 *  `AccountsModule`'s `OnRejected`), carrying one error whose `errorCode` is `TooManyRequests`.
 *  `retryAfterSeconds` is parsed from the `Retry-After` header, never trusted from the body. */
export class ApiRateLimitedError extends ApiError {
  readonly retryAfterSeconds: number;
  readonly response?: ValidationResponse;

  constructor(retryAfterSeconds: number, response?: ValidationResponse) {
    super('Too many attempts. Please wait and try again.', 429);
    this.name = 'ApiRateLimitedError';
    this.retryAfterSeconds = retryAfterSeconds;
    this.response = response;
  }
}

/**
 * HO-XCT-002 ADR-FE-143 — thrown by `apiFetch` BEFORE any request is made, for an unsafe method
 * while the connectivity monitor says the server is unreachable. Offline is read-only and nothing
 * is queued, so a refused write is a typed, immediate error the form can show without discarding
 * what was typed (NFR-USA-5). `status` 0 matches the other "never reached a server" error.
 */
export class OfflineError extends ApiError {
  constructor() {
    super("You're offline, so this couldn't be saved. Your entries are still here.", 0);
    this.name = 'OfflineError';
  }
}

/**
 * HO-XCT-002 ADR-FE-142 — the seam between this module and `lib/connectivity/monitor.ts`. The
 * monitor registers itself here (the same direction as `setSessionErrorHandler`) so `client.ts`
 * imports nothing from it: the monitor's ping goes through `apiFetch`, and a direct import would be
 * a cycle. `isOffline()` is read before every unsafe request; `report()` receives every
 * network-level failure and every response that reached the server.
 */
export interface ConnectivityReporter {
  isOffline: () => boolean;
  report: (outcome: 'reachable' | 'unreachable' | 'server-error') => void;
}
let connectivityReporter: ConnectivityReporter | null = null;
export function setConnectivityReporter(reporter: ConnectivityReporter | null): void {
  connectivityReporter = reporter;
}

/**
 * ARCH gate HIGH 2 (first pass) — this used to be ONE global handler, with `isStaff` guessed from
 * `path.startsWith('/api/staff')`. That guess is wrong for staff screens calling anything outside
 * that prefix (`/api/cats`, `/api/vaccines`, HO-ADM-002's `/api/admin/**`, and every future staff
 * endpoint that doesn't happen to live under `/api/staff/`) — a role change rotating the security
 * stamp mid-session, for example, would send a member of staff to the CUSTOMER sign-in, discarding
 * the intended `?next=` along the way (the customer sanitiser doesn't allow-list `/staff/**`).
 *
 * The body itself carries no audience marker (§11.1's `SessionError` correctly doesn't invent
 * one — a `401` doesn't know or care which UI issued the request), and the request path is not a
 * reliable substitute for it. What IS reliable is which route group's layout is currently
 * mounted — only one of `(portal)`/`(staff)` is ever mounted for a given URL, so each layout's own
 * session guard registers its OWN handler (`CustomerSessionGuard`, `StaffSessionGuard`/`IdleLock`)
 * rather than this module trying to infer audience from anything about the request. `(auth)`
 * pages (sign-in, register, …) register nothing — a `401` there is either `invalid_credentials`
 * (never reaches this handler, see below) or `skipSessionErrorHandling` (the session-status
 * checks), so there is nothing for a global handler to do on an unprotected page anyway.
 */
export type SessionErrorHandler = (body: SessionErrorBody) => void;
let sessionErrorHandler: SessionErrorHandler | null = null;
export function setSessionErrorHandler(handler: SessionErrorHandler | null): void {
  sessionErrorHandler = handler;
}

/**
 * Gate-fix 2026-09-28 (HO-FIL-002 ARCHITECTURE MEDIUM) — `lib/api/files.ts`'s `uploadFile` is the
 * one other place in this app that reaches the network (ADR-FE-150's documented `XMLHttpRequest`
 * exception to "only `client.ts` calls `fetch`"). Without this export, a `401` reaching that XHR
 * had no way to reach the SAME session-error handler `apiFetch` uses below — a session that expired
 * mid-upload never surfaced through the idle-lock/sign-in redirect at all, just a per-row "did not
 * finish uploading". This is the minimal seam needed to fix that, without exposing
 * `sessionErrorHandler` itself (still module-private) or building a second session-error surface.
 */
export function notifySessionError(body: SessionErrorBody): void {
  sessionErrorHandler?.(body);
}

function resolveBaseUrl(): string {
  const url = process.env.NEXT_PUBLIC_API_BASE_URL;
  if (!url) {
    throw new Error(
      'NEXT_PUBLIC_API_BASE_URL is not set. Copy web/.env.local.example to web/.env.local.',
    );
  }
  return url;
}

/**
 * `${NEXT_PUBLIC_API_BASE_URL}${path}` — exported for the handful of call sites that need a
 * plain URL rather than a JSON round-trip: an `<a href>`/`window.open` download link for a
 * binary response (US-CAT-10's medical-history PDF, US-CAT-11's cage-card QR image). Building
 * that URL is not a `fetch` call, so it does not break the "only client.ts calls fetch" rule —
 * it is the one other thing this module is allowed to own about the base URL.
 */
export function getApiUrl(path: string): string {
  return `${resolveBaseUrl()}${path}`;
}

/**
 * HO-APT-002 §8.4, ADR-FE-025 — the server-clock offset. Every response's `Date` header is sampled
 * as `serverTime - Date.now()`; `getServerClockOffsetMs()` returns the median of the last five, so
 * one slow response cannot skew a countdown. No header (or an unreadable one — a cross-origin
 * response only exposes `Date` when the API sends `Access-Control-Expose-Headers: Date`) records
 * nothing, and the offset stays at its last value (0 until the first sample).
 */
const CLOCK_SAMPLE_COUNT = 5;
const clockOffsetSamples: number[] = [];

export function recordServerDate(dateHeader: string | null): void {
  if (!dateHeader) return;
  const serverMs = Date.parse(dateHeader);
  if (Number.isNaN(serverMs)) return;
  clockOffsetSamples.push(serverMs - Date.now());
  if (clockOffsetSamples.length > CLOCK_SAMPLE_COUNT) clockOffsetSamples.shift();
}

export function getServerClockOffsetMs(): number {
  if (clockOffsetSamples.length === 0) return 0;
  const sorted = [...clockOffsetSamples].sort((a, b) => a - b);
  return sorted[Math.floor(sorted.length / 2)] ?? 0;
}

/** Test seam — clears the samples between cases. */
export function resetServerClockSamples(): void {
  clockOffsetSamples.length = 0;
}

const UNSAFE_METHODS = new Set(['POST', 'PUT', 'PATCH', 'DELETE']);

function readCookie(name: string): string | undefined {
  if (typeof document === 'undefined') return undefined; // SSR — no cookie jar to read
  const match = document.cookie.split('; ').find((row) => row.startsWith(`${name}=`));
  return match ? decodeURIComponent(match.slice(name.length + 1)) : undefined;
}

export interface ApiRequestInit extends Omit<RequestInit, 'body'> {
  /** Serialised as JSON. Omit for a request with no body. */
  body?: unknown;
  /** Set by the session-status endpoints themselves (`GET /api/auth/session`,
   *  `GET /api/staff/session`) — a `401` there is the normal way of learning "not signed in", not
   *  a session that just failed, so it must not also invoke the global session-error handler. */
  skipSessionErrorHandling?: boolean;
  /** HO-ORD-002 ADR-FE-097 — sets the `Idempotency-Key` header of a money-moving write (placement,
   *  transfer confirmation, delivery, refund, counter sale). The caller owns the key's lifetime
   *  (`useIdempotencyKey`); this client never generates, stores or replays one, and never retries. */
  idempotencyKey?: string;
  /** HO-XCT-002 — set by the connectivity monitor's own ping, so a ping failure is not reported
   *  twice (the monitor already records it) and a ping is never refused as an "unsafe" write. */
  skipConnectivityReport?: boolean;
  /** HO-XCT-002 — receives the response headers of a `200`/`304`. Used by the offline snapshot to
   *  read `ETag`. A cross-origin response only exposes the headers the API lists in
   *  `Access-Control-Expose-Headers`; an unexposed header simply reads as `null`. */
  onResponseHeaders?: (headers: Headers, status: number) => void;
}

export async function apiFetch<T>(path: string, init: ApiRequestInit = {}): Promise<T> {
  const {
    body,
    headers,
    method,
    skipSessionErrorHandling,
    idempotencyKey,
    skipConnectivityReport,
    onResponseHeaders,
    ...rest
  } = init;
  const httpMethod = (method ?? 'GET').toUpperCase();

  // HO-XCT-002 ADR-FE-143 — offline is read-only: refuse before any fetch, queue nothing.
  if (
    !skipConnectivityReport &&
    UNSAFE_METHODS.has(httpMethod) &&
    connectivityReporter?.isOffline()
  ) {
    throw new OfflineError();
  }

  const identityHeaders: Record<string, string> = {};
  if (UNSAFE_METHODS.has(httpMethod)) {
    const csrfToken = readCookie('pc_csrf');
    if (csrfToken) identityHeaders['X-CSRF-Token'] = csrfToken;
  }

  // X-Branch-Id applies to every branch-scoped request, reads included (HO-ADM-001 §8.3,
  // ADR-046) — unlike the CSRF token, it is not specific to unsafe methods.
  const branchId = getActiveBranchId();
  if (branchId) identityHeaders['X-Branch-Id'] = branchId;

  let response: Response;
  try {
    response = await fetch(`${resolveBaseUrl()}${path}`, {
      ...rest,
      method,
      credentials: 'include', // pc_session / pc_staff_session ride along automatically
      headers: {
        Accept: 'application/json',
        ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}),
        ...(idempotencyKey ? { 'Idempotency-Key': idempotencyKey } : {}),
        ...headers,
        // Last, so a caller's headers can never override the CSRF/branch identity headers.
        ...identityHeaders,
      },
      body: body !== undefined ? JSON.stringify(body) : undefined,
    });
  } catch (caught) {
    // Network failure, CORS rejection, TLS/dev-cert failure (HO-CAT-002 §12.3/§12.4) — none of
    // these have a status code, and none should ever surface as a raw browser error message.
    // A caller that aborts its own request (a navigation, a superseded search) says nothing about the
    // server, so it is not reported: two aborts must not flip the app offline.
    const aborted = (caught as { name?: string } | null)?.name === 'AbortError';
    if (!skipConnectivityReport && !aborted) connectivityReporter?.report('unreachable');
    throw new ApiError('Could not reach the server. Please try again.', 0);
  }

  if (!skipConnectivityReport) {
    connectivityReporter?.report(response.status >= 500 ? 'server-error' : 'reachable');
  }
  recordServerDate(response.headers.get('Date'));
  if (response.status === 200 || response.status === 304) {
    onResponseHeaders?.(response.headers, response.status);
  }

  // HO-XCT-002 — a conditional GET answered `304` has no body: nothing changed.
  if (response.status === 304) return undefined as T;

  if (response.status === 200 || response.status === 201) {
    const contentLength = response.headers.get('content-length');
    if (contentLength === '0') return undefined as T;
    return (await response.json()) as T;
  }

  if (response.status === 204) {
    return undefined as T;
  }

  if (response.status === 400) {
    const validation = (await response.json()) as ValidationResponse;
    throw new ApiValidationError(validation);
  }

  if (response.status === 404) {
    throw new ApiNotFoundError();
  }

  if (response.status === 409) {
    const validation = (await response.json().catch(() => undefined)) as
      ValidationResponse | undefined;
    throw new ApiConflictError(Array.isArray(validation?.errors) ? validation : undefined);
  }

  if (response.status === 401) {
    const body = (await response.json().catch(() => null)) as
      SignInFailedBody | SessionErrorBody | null;
    const parsed: SignInFailedBody | SessionErrorBody = body ?? { error: 'session_invalid' };

    if (parsed.error !== 'invalid_credentials' && !skipSessionErrorHandling) {
      notifySessionError(parsed);
    }
    throw new ApiUnauthorizedError(parsed);
  }

  if (response.status === 403) {
    const body = (await response.json().catch(() => null)) as ForbiddenBody | null;
    throw new ApiForbiddenError(body ?? {});
  }

  if (response.status === 429) {
    const retryAfterHeader = response.headers.get('Retry-After');
    const retryAfterSeconds = retryAfterHeader ? Number(retryAfterHeader) : 60;
    const validationResponse = (await response.json().catch(() => undefined)) as
      ValidationResponse | undefined;
    throw new ApiRateLimitedError(
      Number.isFinite(retryAfterSeconds) ? retryAfterSeconds : 60,
      validationResponse,
    );
  }

  throw new ApiError('Something went wrong. Please try again.', response.status);
}

/**
 * HO-XCT-002 ADR-FE-146 — the one telemetry send. Anonymous by construction: `credentials: 'omit'`
 * (no cookie, so no CSRF check applies and nothing links a sample to a person), no `X-CSRF-Token`,
 * no `X-Branch-Id`, `keepalive` so it survives a page hide. `text/plain` keeps it a CORS "simple
 * request" (no preflight), exactly like a beacon; the API parses JSON either way (HO-XCT-001
 * section 12.4). Never throws and never reports to the connectivity monitor: telemetry must not
 * change what a person sees.
 */
export async function sendTelemetry(path: string, payload: unknown): Promise<boolean> {
  try {
    const response = await fetch(`${resolveBaseUrl()}${path}`, {
      method: 'POST',
      credentials: 'omit',
      keepalive: true,
      headers: { 'Content-Type': 'text/plain;charset=UTF-8' },
      body: JSON.stringify(payload),
    });
    return response.ok;
  } catch {
    return false;
  }
}

/**
 * HO-SHP-002 §8.2, ADR-FE-030 — the ONE server-only read of a PUBLIC endpoint, for the storefront's
 * Server Components. It runs only on the server and targets `API_ORIGIN` (falling back to
 * `NEXT_PUBLIC_API_BASE_URL` for local development), never forwards cookies or any identity header
 * (a public read is anonymous and cacheable — `revalidate` seconds, default 60; `0` means not
 * cached, for reads keyed on free-form visitor input — S12), and maps the same
 * statuses `apiFetch` does. A `503` is the maintenance refusal (US-ADM-08): callers catch an
 * `ApiError` with `status === 503` and render the maintenance notice. `client.ts` remains the only
 * file that calls `fetch`.
 */
export async function getPublic<T>(
  path: string,
  options: { revalidate?: number } = {},
): Promise<T> {
  if (typeof window !== 'undefined') {
    throw new Error('getPublic is server-only; use apiFetch in the browser.');
  }
  const origin = process.env.API_ORIGIN ?? resolveBaseUrl();
  let response: Response;
  try {
    // `__mswFetch` is set only by instrumentation.ts in mock mode (msw/node survives Next's
    // re-patching), which itself runs only in `next dev`; gated the same way here (S11) so a
    // production server never reads it.
    const run =
      (process.env.NODE_ENV === 'development'
        ? (globalThis as { __mswFetch?: typeof fetch }).__mswFetch
        : undefined) ?? fetch;
    response = await run(`${origin}${path}`, {
      method: 'GET',
      headers: { Accept: 'application/json' },
      next: { revalidate: options.revalidate ?? 60 },
    });
  } catch {
    throw new ApiError('Could not reach the server. Please try again.', 0);
  }
  if (response.status === 200) return (await response.json()) as T;
  if (response.status === 400) {
    throw new ApiValidationError((await response.json()) as ValidationResponse);
  }
  if (response.status === 404) throw new ApiNotFoundError();
  throw new ApiError('Something went wrong. Please try again.', response.status);
}
