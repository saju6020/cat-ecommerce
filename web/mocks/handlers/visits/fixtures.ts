import { HttpResponse } from 'msw';
import { getStaffRoleIdsForRequest } from '../areas/accounts';
import { appointmentsFixtureIds } from '../appointments/fixtures';
import { onePixelPngBytes } from '../files/fixtures';

/**
 * `mocks/handlers/visits/fixtures.ts` — HO-EMR-002 §12. Ids, role gating and response helpers shared
 * by the clinical mock modules. Every shape is generated from the REAL API on master
 * (`Read/ViewModels/*.cs`, the `Staff*Controller`s) — never invented (HO-ACC-002 §5.1): the visit has
 * `treatments: []`, a photo's `thumbnailUrl` equals its `url`, a cross-branch visit is a `404`, and
 * there is NO dispensary endpoint (Phase F is not built).
 */
export const emrFixtureIds = {
  mainBranchId: appointmentsFixtureIds.mainBranchId,
  londonBranchId: appointmentsFixtureIds.londonBranchId,

  catWhiskersId: appointmentsFixtureIds.catWhiskersId, // the 10-year history fixture
  catMittensId: appointmentsFixtureIds.catMittensId,
  catTigerId: appointmentsFixtureIds.catTigerId,

  /** Open, Whiskers, a saved note, two problems, vitals, diagnostics, a photo (appointment `InConsultation`). */
  visitOpenId: 'b1000000-0000-0000-0000-000000000001',
  /** Open, Tiger (high risk), saved note plus a NEWER draft by the caller. */
  visitDraftNewerId: 'b1000000-0000-0000-0000-000000000002',
  /** Open, Mittens, empty note, a note template for the service. */
  visitTemplateId: 'b1000000-0000-0000-0000-000000000003',
  /** Open, Biscuit, nothing recorded yet. */
  visitEmptyId: 'b1000000-0000-0000-0000-000000000004',
  /** Finalised, Mittens, addenda, a reminder follow-up, one resulted order. */
  visitFinalisedId: 'b1000000-0000-0000-0000-000000000005',
  /** Belongs to the London branch — a `404` for the main branch. */
  visitOtherBranchId: 'b1000000-0000-0000-0000-000000000006',

  orderPendingId: 'b2000000-0000-0000-0000-000000000001',
  orderResultedId: 'b2000000-0000-0000-0000-000000000002',
  orderImagingId: 'b2000000-0000-0000-0000-000000000003',
  orderFinalisedId: 'b2000000-0000-0000-0000-000000000004',

  consentRecordedId: 'b3000000-0000-0000-0000-000000000001',
  consentWithdrawnId: 'b3000000-0000-0000-0000-000000000002',
  consentUploadId: 'b3000000-0000-0000-0000-000000000003',

  /** The cat the portal owner (Sam Rahman) owns, with finalised visits. */
  portalCatId: appointmentsFixtureIds.catMittensId,
  /** A visit that is not the caller's (or still open) — neutral `404`. */
  portalNotMineVisitId: 'b1000000-0000-0000-0000-0000000000ff',

  codeOtitisId: 'b4000000-0000-0000-0000-000000000001',
  codeCkdId: 'b4000000-0000-0000-0000-000000000002',
  codeNadId: 'b4000000-0000-0000-0000-000000000003', // SYS-NAD, the built-in "routine visit"
  codeRetiredId: 'b4000000-0000-0000-0000-000000000004',

  analyteCreatinineId: 'b5000000-0000-0000-0000-000000000001',
  analyteUreaId: 'b5000000-0000-0000-0000-000000000002',
  analyteFelvId: 'b5000000-0000-0000-0000-000000000003',

  vetStaffName: 'Dr Akter',
} as const;

// ---- who is calling --------------------------------------------------------------------------

let overrideRoles: string[] | null = null;

/** Test hook: with no mock staff session (Vitest), the mock answers as these roles (default Vet). */
export function setMockCallerRoles(roles: string[] | null): void {
  overrideRoles = roles;
}

export function callerRoles(request: Request): string[] {
  return getStaffRoleIdsForRequest(request) ?? overrideRoles ?? ['Vet'];
}

/** `ActionRoles.Consultation.cs` — the table the endpoints enforce. */
const ACTION_ROLES: Record<string, string[]> = {
  'Visits.View': ['Nurse', 'Vet', 'Administrator'],
  'Visits.Open': ['Nurse', 'Vet'],
  'Visits.RecordVitals': ['Nurse', 'Vet'],
  'Visits.WriteNote': ['Vet'],
  'Visits.Finalise': ['Vet'],
  'Visits.AddAddendum': ['Vet'],
  'Visits.PrintSummary': ['Nurse', 'Vet', 'Administrator'],
  'ClinicalCatalogue.View': ['Nurse', 'Vet', 'Administrator'],
  'ClinicalCatalogue.Manage': ['Vet', 'Administrator'],
  'Consents.View': ['Reception', 'Nurse', 'Vet', 'Administrator'],
  'Consents.Record': ['Reception', 'Nurse', 'Vet', 'Administrator'],
};

export function holds(roles: string[], action: string): boolean {
  return (ACTION_ROLES[action] ?? []).some((role) => roles.includes(role));
}

export function staffNameFor(roles: string[]): string {
  if (roles.includes('Vet')) return emrFixtureIds.vetStaffName;
  if (roles.includes('Nurse')) return 'Nadia Nurse';
  if (roles.includes('Administrator')) return 'Ada Administrator';
  return 'Reception Desk';
}

/** `403` for a caller whose roles lack the action, else `null` (the real `RequireActionFilter` shape). */
export function requireAction(request: Request, action: string): Response | null {
  const roles = callerRoles(request);
  if (holds(roles, action)) return null;
  return HttpResponse.json(
    {
      errors: [
        { errorCode: 'ActionNotPermitted', errorMessage: 'You do not have permission to do this.' },
      ],
    },
    { status: 403 },
  );
}

/** The `X-Branch-Id` header, else the main branch (the mock's default active branch). */
export function branchOf(request: Request): string {
  return request.headers.get('X-Branch-Id') ?? emrFixtureIds.mainBranchId;
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

export function conflict(message: string) {
  return HttpResponse.json({ error: message }, { status: 409 });
}

export interface MockViolation {
  propertyName: string;
  errorCode: string;
  errorMessage: string;
}

/** A `400` `ValidationResponse`; `extra` adds one more member (e.g. `outOfRange`). */
export function violation(extra: Record<string, unknown> | null, ...items: MockViolation[]) {
  return HttpResponse.json(
    {
      errors: items.map((item) => ({ ...item, resourceName: 'Visit' })),
      isValid: false,
      ...(extra ?? {}),
    },
    { status: 400 },
  );
}

// ---- signed URLs -----------------------------------------------------------------------------

let signedUrlTtlSeconds = 300;

/** Test hook (§12): URLs that expire after `seconds`, to exercise the one-shot refetch. */
export function setMockSignedUrlTtlSeconds(seconds: number): void {
  signedUrlTtlSeconds = seconds;
}

export function signedFile(
  fileId: string,
  contentType: string,
  originalFileName: string,
  sizeInBytes = onePixelPngBytes().byteLength,
) {
  const exp = Math.floor(Date.now() / 1000) + signedUrlTtlSeconds;
  const base = process.env.NEXT_PUBLIC_API_BASE_URL ?? '';
  return {
    fileId,
    url: `${base}/api/files/${fileId}/content?exp=${exp}&disp=inline&sig=mock`,
    contentType,
    sizeInBytes,
    originalFileName,
    urlExpiresOn: new Date(exp * 1000).toISOString(),
  };
}

/** Today's date in the clinic's zone (the real server compares against the BRANCH's today, not UTC's). */
export function branchToday(zone = 'Asia/Dhaka'): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: zone }).format(new Date());
}
