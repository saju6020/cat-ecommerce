import { HttpResponse } from 'msw';
import { accountsFixtureIds } from '../areas/accounts';

/**
 * `mocks/handlers/admin/fixtures.ts` — shared ids and small response helpers for every
 * `mocks/handlers/admin/*.ts` file (HO-ADM-002 §12). Reuses `accountsFixtureIds`'s already-seeded
 * staff/customer ids (read-only import — this file never mutates that module's own state) so an
 * e2e spec that signs in as the seeded administrator/nurse also has a rosterable staff member and
 * a customer with a pending policy acceptance to exercise, without inventing a second identity.
 *
 * Role/session enforcement is deliberately NOT modelled here — HO-CAT-002's own precedent
 * (`mocks/handlers/areas/cat-profiles.ts`) does not re-resolve the `pc_staff_session` cookie
 * either (only `accounts.ts` owns that cookie's identity mapping, and `mocks/handlers/areas/**`
 * is outside this handover's §13 scope to import from or extend). A `403` a specific test needs
 * (U-A1, U-X1) is produced with `server.use(...)` overriding that one endpoint for that one test,
 * exactly the pattern `accounts.ts`'s own doc comment prescribes.
 */
export const administrationFixtureIds = {
  mainBranchId: 'b0000000-0000-0000-0000-00000000a001',
  londonBranchId: 'b0000000-0000-0000-0000-00000000a002',
  consultationRoomId: 'r0000000-0000-0000-0000-00000000a001',
  theatreRoomId: 'r0000000-0000-0000-0000-00000000a002',
  closurePublicHolidayId: 'c0000000-0000-0000-0000-00000000a001',
  closureHalfDayId: 'c0000000-0000-0000-0000-00000000a002',
  vetStaffId: accountsFixtureIds.nurseStaffId, // reused as the one rosterable, nameable staff fixture
  adminStaffId: accountsFixtureIds.adminStaffId,
  leaveRequestId: 'l0000000-0000-0000-0000-00000000a001',
  serviceConsultationId: 's0000000-0000-0000-0000-00000000a001',
  serviceUnpricedId: 's0000000-0000-0000-0000-00000000a002',
  taxClassStandardId: 't0000000-0000-0000-0000-00000000a001',
  contentPageId: 'k0000000-0000-0000-0000-00000000a001',
  contentFaqId: 'k0000000-0000-0000-0000-00000000a002',
  contentBannerId: 'k0000000-0000-0000-0000-00000000a003',
  policyKey: 'terms',
  customerId: accountsFixtureIds.customerId,
  auditActorId: accountsFixtureIds.adminStaffId,
  trigger413ActorId: 'trigger-413-export',
} as const;

export interface MockValidationError {
  propertyName: string;
  errorCode: string;
  errorMessage: string;
  resourceName: string;
}

export function validationError(
  propertyName: string,
  errorCode: string,
  errorMessage: string,
  resourceName = 'Administration',
) {
  return HttpResponse.json(
    { errors: [{ propertyName, errorCode, errorMessage, resourceName }], isValid: false },
    { status: 400 },
  );
}

export function notFound() {
  return new HttpResponse(null, { status: 404 });
}

export function forbidden(errorCode = 'ActionNotPermitted', errorMessage = 'Not permitted.') {
  return HttpResponse.json({ errors: [{ errorCode, errorMessage }] }, { status: 403 });
}

export function noContent() {
  return new HttpResponse(null, { status: 204 });
}

/**
 * `mocks/handlers/areas/accounts.ts`'s own `sessionStorage`-persistence pattern, generalised for
 * every `mocks/handlers/admin/*.ts` module. Each resource module's mock data lives in this file's
 * JS module scope — fine for a component test (Vitest imports the SAME module instance across a
 * whole test file), but a real browser's `page.goto()`/full navigation re-executes every module
 * from scratch, discarding plain `let` state, even though MSW's own browser Service Worker (and
 * the real `pc_staff_session`/`pc_session` cookies it reads) survive the navigation untouched.
 * Persisting each resource's own store to `sessionStorage` — scoped to the tab, exactly like the
 * real cookies — is this mock's stand-in for the real API's Postgres-backed tables, the same
 * reasoning `accounts.ts`'s own doc comment gives for why it needs the same thing. Gated on
 * `'serviceWorker' in navigator`, absent in Vitest's jsdom, so component tests keep the simple,
 * always-fresh-per-import behaviour they already had.
 */
const canPersistMock =
  typeof window !== 'undefined' && typeof navigator !== 'undefined' && 'serviceWorker' in navigator;

export function loadPersisted<T>(key: string): T | null {
  if (!canPersistMock) return null;
  try {
    const raw = window.sessionStorage.getItem(key);
    return raw ? (JSON.parse(raw) as T) : null;
  } catch {
    return null;
  }
}

export function savePersisted(key: string, data: unknown): void {
  if (!canPersistMock) return;
  try {
    window.sessionStorage.setItem(key, JSON.stringify(data));
  } catch {
    // Best-effort — a quota error or private-mode restriction never breaks the mock response.
  }
}

export function clearPersisted(key: string): void {
  if (!canPersistMock) return;
  try {
    window.sessionStorage.removeItem(key);
  } catch {
    // Best-effort, as above.
  }
}

/**
 * Converts a branch-local wall-clock date+time into a real UTC instant (ISO string) — the
 * standard "format a candidate instant in the target zone, measure the drift, correct for it"
 * technique, used only by this mock (the real backend does the equivalent with
 * `TimeZoneInfo`/`BranchCalendar`, HO-ADM-001 ADR-045) so seeded roster/availability fixtures
 * round-trip through the FE's own `formatInZone` and show the intended wall-clock time.
 */
export function zonedTimeToUtcIso(dateIso: string, time: string, timeZoneId: string): string {
  const guess = new Date(`${dateIso}T${time}:00Z`);
  const formatter = new Intl.DateTimeFormat('en-US', {
    timeZone: timeZoneId,
    hourCycle: 'h23',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  });
  const parts = Object.fromEntries(formatter.formatToParts(guess).map((p) => [p.type, p.value]));
  const asIfZoned = Date.UTC(
    Number(parts.year),
    Number(parts.month) - 1,
    Number(parts.day),
    Number(parts.hour),
    Number(parts.minute),
    Number(parts.second),
  );
  const driftMs = guess.getTime() - asIfZoned;
  return new Date(guess.getTime() + driftMs).toISOString();
}
