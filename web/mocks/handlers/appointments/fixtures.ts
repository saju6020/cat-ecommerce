import { HttpResponse } from 'msw';
import { accountsFixtureIds } from '../areas/accounts';
import { catProfilesFixtureIds } from '../areas/cat-profiles';
import { administrationFixtureIds } from '../admin/fixtures';

/**
 * `mocks/handlers/appointments/fixtures.ts` — HO-APT-002 §12. Ids and response helpers shared by
 * the Appointments mock modules. Shapes are generated from HO-APT-001 §12.4 as the real
 * controllers send them (`ClinicViewModels.cs`).
 *
 * The mock does not resolve sessions (HO-ADM-002's precedent in `admin/fixtures.ts`): every
 * customer endpoint answers for `appointmentsFixtureIds.signedInCustomerId`; a `403` a test needs
 * is produced with `server.use(...)`.
 */
export const appointmentsFixtureIds = {
  mainBranchId: administrationFixtureIds.mainBranchId,
  londonBranchId: administrationFixtureIds.londonBranchId,
  consultationServiceId: administrationFixtureIds.serviceConsultationId,

  signedInCustomerId: accountsFixtureIds.customerId, // "Sam Rahman" — has 2 no-shows
  signedInCustomerName: 'Sam Rahman',
  otherCustomerId: 'a1000000-0000-0000-0000-000000000002', // another customer's rows (404 for Sam)
  rahimCustomerId: 'a1000000-0000-0000-0000-000000000003',
  farhanaCustomerId: 'a1000000-0000-0000-0000-000000000004',

  catMomoId: 'f0000000-0000-0000-0000-000000000001', // Sam's cat in the accounts mock
  catWhiskersId: catProfilesFixtureIds.catWithAllergiesExactDob,
  catTigerId: catProfilesFixtureIds.catHighRiskOverdue,
  catMittensId: catProfilesFixtureIds.catCurrentPlain,

  vetRahmanId: 'a2000000-0000-0000-0000-000000000001',
  vetAkterId: 'a2000000-0000-0000-0000-000000000002',
  nurseTanvirId: 'a2000000-0000-0000-0000-000000000003',
  vetWilsonId: 'a2000000-0000-0000-0000-000000000004', // London

  roomAId: administrationFixtureIds.consultationRoomId,
  roomBId: 'a3000000-0000-0000-0000-000000000002',
  theatreRoomId: administrationFixtureIds.theatreRoomId,
  londonRoomId: 'a3000000-0000-0000-0000-000000000004',

  /** A booked appointment past its cut-off (start is ~2 h away): no change buttons. */
  lockedAppointmentId: 'a4000000-0000-0000-0000-000000000001',
  /** A booked appointment far enough ahead to reschedule or cancel. */
  changeableAppointmentId: 'a4000000-0000-0000-0000-000000000002',
  pastCompletedAppointmentId: 'a4000000-0000-0000-0000-000000000003',
  pastCancelledAppointmentId: 'a4000000-0000-0000-0000-000000000004',
  /** A waitlist offer: a `Held` appointment the customer can claim. */
  offerAppointmentId: 'a4000000-0000-0000-0000-000000000005',
  /** An offer that has already ended (the held appointment expired). */
  expiredOfferAppointmentId: 'a4000000-0000-0000-0000-000000000006',
  /** Flagged for rescheduling (leave). */
  flaggedAppointmentId: 'a4000000-0000-0000-0000-000000000007',
  /** Booked today, grace elapsed: No-show is accepted. */
  noShowEligibleAppointmentId: 'a4000000-0000-0000-0000-000000000008',
  /** Booked today, too early for a no-show (`NoShowTooEarly`). */
  todayBookedAppointmentId: 'a4000000-0000-0000-0000-000000000009',
  /** Booked today, already checked in and on the queue. */
  queuedBookedAppointmentId: 'a4000000-0000-0000-0000-00000000000a',
  /** A walk-in already waiting (Urgent, no vet yet). */
  waitingWalkInId: 'a4000000-0000-0000-0000-00000000000b',
  /** In consultation with Dr Akter. */
  inConsultationAppointmentId: 'a4000000-0000-0000-0000-00000000000c',
  /** Booked a few days out with Dr Akter — the move-conflict target. */
  akterMidMorningAppointmentId: 'a4000000-0000-0000-0000-00000000000d',
  /** The one the e2e drags/moves (Dr Rahman, a free neighbourhood). */
  movableAppointmentId: 'a4000000-0000-0000-0000-00000000000e',

  waitlistWaitingId: 'a5000000-0000-0000-0000-000000000001',
  waitlistOfferedId: 'a5000000-0000-0000-0000-000000000002',
  waitlistLapsedId: 'a5000000-0000-0000-0000-000000000003',
  expiredWaitlistOfferedId: 'a5000000-0000-0000-0000-000000000004',
} as const;

/** The local `HH:mm` that always loses the race (someone else takes it) the first time it is
 *  claimed — `E-A2`, `U-A5`, `U-B2`. After the race the slot is genuinely taken. */
export const RACE_LOCAL_TIME = '10:30';

/** Seconds a mock hold lasts (§12: "a hold that expires in 70 s"). */
export const MOCK_HOLD_SECONDS = 70;
/** Seconds a waitlist offer's claim window lasts in the mock. */
export const MOCK_CLAIM_WINDOW_MINUTES = 30;

/** The mock server's clock skew in ms (a test sets it in sessionStorage to exercise a clock-skewed
 *  Date header). Every "server" time the mock produces — hold expiry included — is
 *  serverNow(), so a skewed server is internally consistent. */
export const CLOCK_SKEW_STORAGE_KEY = '__purrcare_apt_mock_clock_skew_ms__';

export function clockSkewMs(): number {
  try {
    return Number(window.sessionStorage.getItem(CLOCK_SKEW_STORAGE_KEY)) || 0;
  } catch {
    return 0;
  }
}

export function serverNow(): number {
  return Date.now() + clockSkewMs();
}

/** The Date header is what client.ts samples for the server clock offset; a cross-origin browser
 *  only exposes it when the response lists it in Access-Control-Expose-Headers. */
export function serverHeaders(): Record<string, string> {
  return { Date: new Date(serverNow()).toUTCString(), 'Access-Control-Expose-Headers': 'Date' };
}

export function json(body: unknown, status = 200) {
  return HttpResponse.json(body as never, { status, headers: serverHeaders() });
}

export interface MockViolation {
  propertyName: string;
  errorCode: string;
  errorMessage: string;
}

export function violation(status: 400 | 409, ...items: MockViolation[]) {
  return HttpResponse.json(
    {
      errors: items.map((item) => ({ ...item, resourceName: 'Clinic' })),
      isValid: false,
    },
    { status, headers: serverHeaders() },
  );
}

export function notFound() {
  return new HttpResponse(null, { status: 404, headers: serverHeaders() });
}

export function noContent() {
  return new HttpResponse(null, { status: 204, headers: serverHeaders() });
}

export function forbidden() {
  return HttpResponse.json(
    { errors: [{ errorCode: 'ActionNotPermitted', errorMessage: 'Not permitted.' }] },
    { status: 403, headers: serverHeaders() },
  );
}

/**
 * The API URL for a mock handler's own look-up `fetch` (a cat's or customer's name). Deliberately
 * NOT `getApiUrl` from `lib/api/client`: importing the client from a mock module loads it while
 * `mocks/server` is still being imported, before a test's own `vi.mock('@/lib/branch')` can apply
 * to it (it broke `api-client-with-branch.test.ts`).
 */
export function apiUrl(path: string): string {
  return `${process.env.NEXT_PUBLIC_API_BASE_URL ?? ''}${path}`;
}
