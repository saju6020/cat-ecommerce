import { HttpResponse } from 'msw';
import { getStaffRoleIdsForRequest } from '../areas/accounts';
import { administrationFixtureIds } from '../admin/fixtures';
import { catProfilesFixtureIds } from '../areas/cat-profiles';
import { appointmentsFixtureIds } from '../appointments/fixtures';

/**
 * `mocks/handlers/pharmacy/fixtures.ts` — HO-PHM-002 §12. Ids, role gating and response helpers shared
 * by the pharmacy mock modules. Every shape is typed with the `lib/api/*` interfaces, which mirror the
 * REAL view models on master (`PharmacyPrescriptionViewModels.cs` and friends) — a mock may not return
 * a field the API does not. The mock plays the SERVER: the dose maths, allergy matching and FEFO plan
 * live here (and only here), never in the app.
 */
export const pharmacyFixtureIds = {
  mainBranchId: administrationFixtureIds.mainBranchId,
  londonBranchId: administrationFixtureIds.londonBranchId,

  /** 4.20 kg, weighed 1 month ago. Allergic to chicken protein (cat mock); tests add Penicillin. */
  catWhiskersId: catProfilesFixtureIds.catWithAllergiesExactDob,
  /** 5.80 kg, weighed 2 months ago — `Stale` (older than 45 days). High risk. */
  catTigerId: catProfilesFixtureIds.catHighRiskOverdue,
  /** The portal owner's cat. 3.60 kg, weighed 1 month ago. */
  catMittensId: catProfilesFixtureIds.catCurrentPlain,
  catMomoId: appointmentsFixtureIds.catMomoId,

  // formulary
  medMeloxicamId: 'e1000000-0000-4000-8000-000000000001',
  medFusidicId: 'e1000000-0000-4000-8000-000000000002',
  medAmoxicillinId: 'e1000000-0000-4000-8000-000000000003',
  medPrednisoloneId: 'e1000000-0000-4000-8000-000000000004',
  medBuprenorphineId: 'e1000000-0000-4000-8000-000000000005',
  medGabapentinId: 'e1000000-0000-4000-8000-000000000006',
  medMaropitantId: 'e1000000-0000-4000-8000-000000000007',

  // prescriptions
  rxMittensActiveId: 'e2000000-0000-4000-8000-000000000001',
  rxMittensEndedId: 'e2000000-0000-4000-8000-000000000002',
  rxMittensRequestedId: 'e2000000-0000-4000-8000-000000000003',
  rxWhiskersPrednisoloneId: 'e2000000-0000-4000-8000-000000000004',
  rxWhiskersAmoxicillinQueueId: 'e2000000-0000-4000-8000-000000000005',
  rxTigerControlledQueueId: 'e2000000-0000-4000-8000-000000000006',
  rxExpiredOnlyQueueId: 'e2000000-0000-4000-8000-000000000007',
  rxMittensRepeatQueueId: 'e2000000-0000-4000-8000-000000000008',
  rxWhiskersGabapentinId: 'e2000000-0000-4000-8000-000000000009',
  rxTigerControlledDispensedId: 'e2000000-0000-4000-8000-000000000010',

  dispensingMittensId: 'e3000000-0000-4000-8000-000000000001',
  dispensingControlledId: 'e3000000-0000-4000-8000-000000000002',
  repeatRequestMittensId: 'e4000000-0000-4000-8000-000000000001',
  dietAuthorisationActiveId: 'e5000000-0000-4000-8000-000000000001',
  dietProductRenalId: 'e6000000-0000-4000-8000-000000000001',
  dietProductUrinaryId: 'e6000000-0000-4000-8000-000000000002',

  // sign-in details the witness dialog accepts (the accounts mock has only an administrator and a nurse)
  witnessNurseIdentifier: 'witness.nurse@purrcare.test',
  witnessNursePassword: 'WitnessPass123!',
  witnessVetIdentifier: 'witness.vet@purrcare.test',
  witnessVetPassword: 'WitnessVet123!',
} as const;

// ---- who is calling --------------------------------------------------------------------------

let overrideRoles: string[] | null = null;

/** Test hook: with no mock staff session (Vitest), the mock answers as these roles (default Vet). */
export function setPharmacyCallerRoles(roles: string[] | null): void {
  overrideRoles = roles;
}

export function callerRoles(request: Request): string[] {
  return getStaffRoleIdsForRequest(request) ?? overrideRoles ?? ['Vet'];
}

/** `ActionRoles` from HO-PHM-001 §12.3 as confirmed by the owner on 2026-10-10. */
const ACTION_ROLES: Record<string, string[]> = {
  'Formulary.View': ['Reception', 'Nurse', 'Vet', 'Inventory', 'Administrator'],
  'Formulary.Manage': ['Administrator'],
  'Prescriptions.View': ['Reception', 'Nurse', 'Vet', 'Administrator'],
  'Prescriptions.Issue': ['Vet'],
  'Dispensing.View': ['Reception', 'Nurse', 'Vet', 'Inventory', 'Administrator'],
  'Dispensing.Dispense': ['Reception', 'Nurse', 'Vet', 'Administrator'],
  'Dispensing.Reverse': ['Vet', 'Administrator'],
  'ControlledDrugs.View': ['Nurse', 'Vet', 'Inventory', 'Administrator'],
  'ControlledDrugs.Correct': ['Vet'],
  'DietAuthorisations.View': ['Reception', 'Nurse', 'Vet', 'ShopManager', 'Administrator'],
  'DietAuthorisations.Issue': ['Vet'],
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

export function staffNameFor(roles: string[]): string {
  if (roles.includes('Vet')) return 'Dr Akter';
  if (roles.includes('Nurse')) return 'Nadia Nurse';
  if (roles.includes('Administrator')) return 'Ada Administrator';
  return 'Reception Desk';
}

export function branchOf(request: Request): string {
  return request.headers.get('X-Branch-Id') ?? pharmacyFixtureIds.mainBranchId;
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

/** A `400` `ValidationResponse`; `extra` adds members next to `errors` (e.g. `assessment`). */
export function violation(extra: Record<string, unknown> | null, ...items: MockViolation[]) {
  return HttpResponse.json(
    {
      errors: items.map((item) => ({ ...item, resourceName: 'Pharmacy' })),
      isValid: false,
      ...(extra ?? {}),
    },
    { status: 400 },
  );
}

/** A `409` carrying the same shape as `violation` (changed warnings, a stale version). */
export function conflictViolation(
  extra: Record<string, unknown> | null,
  ...items: MockViolation[]
) {
  return HttpResponse.json(
    {
      errors: items.map((item) => ({ ...item, resourceName: 'Pharmacy' })),
      isValid: false,
      ...(extra ?? {}),
    },
    { status: 409 },
  );
}

/** The server's number text: at least two and at most four decimals ("0.30", "0.0625"). */
export function amountText(value: number): string {
  const fixed = value.toFixed(4).replace(/0+$/, '');
  const [whole, fraction = ''] = fixed.split('.');
  return `${whole}.${fraction.padEnd(2, '0')}`;
}

export function quantity(value: number, unit: string) {
  return { amount: amountText(value), unit };
}

/** The API URL for a mock handler's own look-up `fetch` (a cat's allergies and weights). */
export function apiUrl(path: string): string {
  return `${process.env.NEXT_PUBLIC_API_BASE_URL ?? ''}${path}`;
}
