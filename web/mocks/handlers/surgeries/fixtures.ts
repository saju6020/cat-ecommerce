import { HttpResponse } from 'msw';
import { getStaffRoleIdsForRequest } from '../areas/accounts';
import { administrationFixtureIds } from '../admin/fixtures';
import { appointmentsFixtureIds } from '../appointments/fixtures';
import { pharmacyFixtureIds } from '../pharmacy/fixtures';

/**
 * `mocks/handlers/surgeries/fixtures.ts` — HO-SRG-002 §12. Ids, role gating and response helpers shared
 * by the surgery mock modules. Every payload is typed with the `lib/api/*` interfaces, which mirror the
 * REAL view models on master (`SurgeryViewModels.cs` and friends) — a mock may not return a field the
 * API does not. The mock plays the SERVER: availability, clashes, the checklist, the start gate and the
 * log live here (and only here), never in the app.
 */
export const surgeryFixtureIds = {
  mainBranchId: administrationFixtureIds.mainBranchId,
  londonBranchId: administrationFixtureIds.londonBranchId,
  mainZone: 'Asia/Dhaka',
  londonZone: 'Europe/London',

  theatre1Id: administrationFixtureIds.theatreRoomId,
  theatre2Id: 'a3000000-0000-0000-0000-0000000000a2',

  surgeonRahmanId: appointmentsFixtureIds.vetRahmanId,
  surgeonAkterId: appointmentsFixtureIds.vetAkterId,
  nurseTanvirId: appointmentsFixtureIds.nurseTanvirId,
  nurseSadiaId: 'a2000000-0000-0000-0000-0000000000a5',

  serviceSpayId: '5a000000-0000-4000-8000-000000000001',
  serviceCastrationId: '5a000000-0000-4000-8000-000000000002',
  serviceDentalId: '5a000000-0000-4000-8000-000000000003',
  serviceMassRemovalId: '5a000000-0000-4000-8000-000000000004',

  // Customers and cats the mock owns (the cat and customer mocks do not have an intact female).
  customerFarhanaId: '5b000000-0000-4000-8000-000000000001',
  customerFarhanaMobile: '01712345678',
  customerKamrulId: '5b000000-0000-4000-8000-000000000002',
  customerKamrulMobile: '01819556677',
  customerImranId: '5b000000-0000-4000-8000-000000000003',
  customerImranMobile: '01678110022',
  catPepperId: '5c000000-0000-4000-8000-000000000001', // F, intact
  catNoodleId: '5c000000-0000-4000-8000-000000000002', // M, intact
  catShadowId: '5c000000-0000-4000-8000-000000000003', // F, intact, high risk
  catBiscuitId: '5c000000-0000-4000-8000-000000000004', // M, already neutered
  catMomoId: appointmentsFixtureIds.catMomoId, // the signed-in customer's cat (F, intact)

  campaignOutreachId: '5d000000-0000-4000-8000-000000000001',
  campaignExhaustedId: '5d000000-0000-4000-8000-000000000002',

  // Seeded surgeries (relative to today in the branch zone).
  surgeryScheduledTodayId: '5e000000-0000-4000-8000-000000000001',
  surgeryPreOpConsentOnlyId: '5e000000-0000-4000-8000-000000000002',
  surgeryPreOpIncompleteId: '5e000000-0000-4000-8000-000000000003',
  surgeryInTheatreId: '5e000000-0000-4000-8000-000000000004',
  surgeryRecoveryId: '5e000000-0000-4000-8000-000000000005',
  surgeryDischargedId: '5e000000-0000-4000-8000-000000000006',
  surgeryOverriddenId: '5e000000-0000-4000-8000-000000000007',
  surgeryCancelledId: '5e000000-0000-4000-8000-000000000008',
  surgeryTomorrowId: '5e000000-0000-4000-8000-000000000009',
  surgeryLondonId: '5e000000-0000-4000-8000-00000000000a',
  surgeryOwnerBeforeId: '5e000000-0000-4000-8000-00000000000b',
  surgeryOwnerDuringId: '5e000000-0000-4000-8000-00000000000c',
  surgeryOwnerAfterId: '5e000000-0000-4000-8000-00000000000d',
  surgeryLockedPricesId: '5e000000-0000-4000-8000-00000000000e',

  // Sign-ins the witness step accepts (the pharmacy mock's — the accounts mock has two staff only).
  witnessNurseIdentifier: pharmacyFixtureIds.witnessNurseIdentifier,
  witnessNursePassword: pharmacyFixtureIds.witnessNursePassword,
  witnessVetIdentifier: pharmacyFixtureIds.witnessVetIdentifier,
  witnessVetPassword: pharmacyFixtureIds.witnessVetPassword,

  /** Booking at 16:30 gets a one-off `409` race (then the time is gone from availability). */
  raceStartTime: '16:30',
  /** A day with no common start (theatre closed for maintenance). */
  noStartDayOffset: 6,
} as const;

// ---- who is calling --------------------------------------------------------------------------

let overrideRoles: string[] | null = null;

/** Test hook: with no mock staff session (Vitest), the mock answers as these roles (default Vet). */
export function setSurgeryCallerRoles(roles: string[] | null): void {
  overrideRoles = roles;
}

export function callerRoles(request: Request): string[] {
  return getStaffRoleIdsForRequest(request) ?? overrideRoles ?? ['Vet'];
}

/** `ActionRoles` from HO-SRG-001 §12.3. */
const ACTION_ROLES: Record<string, string[]> = {
  'Surgery.View': ['Reception', 'Nurse', 'Vet', 'Administrator'],
  'Surgery.Schedule': ['Reception', 'Vet', 'Administrator'],
  'Surgery.Admit': ['Reception', 'Nurse', 'Vet'],
  'Surgery.ManageChecklist': ['Nurse', 'Vet'],
  'Surgery.AssessRisk': ['Vet'],
  'Surgery.Start': ['Nurse', 'Vet'],
  'Surgery.OverrideConsent': ['Vet'],
  'Surgery.LogVitals': ['Nurse', 'Vet'],
  'Surgery.LogDrug': ['Vet'],
  'Surgery.EndProcedure': ['Vet'],
  'Surgery.WriteReport': ['Vet'],
  'Surgery.ManageRecovery': ['Nurse', 'Vet'],
  'Surgery.ManageProtocols': ['Vet', 'Administrator'],
  'Surgery.ManageCampaigns': ['Administrator'],
};

export function holds(roles: string[], action: string): boolean {
  return (ACTION_ROLES[action] ?? []).some((role) => roles.includes(role));
}

/** `403` for a caller whose roles lack the action, else `null` (the real `RequireActionFilter` shape). */
export function requireAction(request: Request, action: string): Response | null {
  if (holds(callerRoles(request), action)) return null;
  return HttpResponse.json(
    {
      errors: [
        { errorCode: 'ActionNotPermitted', errorMessage: 'You do not have permission to do this.' },
      ],
    },
    { status: 403 },
  );
}

/** Who the signed-in staff member is, by role (the mock has no staff directory of its own). */
export function actorFor(roles: string[]): { id: string; name: string } {
  if (roles.includes('Vet')) return { id: surgeryFixtureIds.surgeonAkterId, name: 'Dr Akter' };
  if (roles.includes('Nurse')) return { id: surgeryFixtureIds.nurseTanvirId, name: 'Nurse Tanvir' };
  if (roles.includes('Administrator'))
    return { id: 'a2000000-0000-0000-0000-0000000000ad', name: 'Ada Administrator' };
  return { id: 'a2000000-0000-0000-0000-0000000000ec', name: 'Reception Desk' };
}

export function branchOf(request: Request): string {
  return request.headers.get('X-Branch-Id') ?? surgeryFixtureIds.mainBranchId;
}

export function zoneOfBranch(branchId: string): string {
  return branchId === surgeryFixtureIds.londonBranchId
    ? surgeryFixtureIds.londonZone
    : surgeryFixtureIds.mainZone;
}

// ---- response helpers ------------------------------------------------------------------------

export function ok(body: unknown, status = 200) {
  return HttpResponse.json(body as never, { status });
}

export function noContent() {
  return new HttpResponse(null, { status: 204 });
}

export function notFound() {
  return new HttpResponse(null, { status: 404 });
}

export interface MockViolation {
  propertyName: string;
  errorCode: string;
  errorMessage: string;
}

function body(items: MockViolation[]) {
  return {
    errors: items.map((item) => ({ ...item, resourceName: 'Surgery' })),
    isValid: false,
  };
}

/** A `400` `ValidationResponse`. */
export function violation(...items: MockViolation[]) {
  return HttpResponse.json(body(items), { status: 400 });
}

/** A `409` carrying the same shape (a stale version, a race, an immutable record). */
export function conflict(...items: MockViolation[]) {
  return HttpResponse.json(body(items), { status: 409 });
}

export function bad(propertyName: string, errorCode: string, errorMessage: string): MockViolation {
  return { propertyName, errorCode, errorMessage };
}

export function money(amount: number, currencyCode = 'BDT') {
  return { amount: amount.toFixed(2), currencyCode };
}

/** The API URL for a mock handler's own look-up `fetch`. */
export function apiUrl(path: string): string {
  return `${process.env.NEXT_PUBLIC_API_BASE_URL ?? ''}${path}`;
}

export function newId(): string {
  return crypto.randomUUID();
}
