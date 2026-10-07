import { apiFetch, getApiUrl } from './client';

/**
 * Cat Profiles domain types and endpoint functions — HO-CAT-002 §11.1/§11.2, mirroring
 * HO-CAT-001 §12.3/§12.4 exactly (verified against the actual `src/Read/.../ViewModels/*.cs`
 * and `src/Api/.../Controllers/CatsController.cs` and `VaccinesController.cs`, not just the
 * handover prose — same field names, same nullability, same casing as the real
 * System.Text.Json-serialised response). Domain types live here, not in the shared
 * `lib/api/types.ts` (web/README.md "Adding a functional area").
 *
 * Enum-shaped string fields (`sex`, `dateOfBirthPrecision`, `neuterStatus`, `lifeStatus`,
 * `severity`, `treatmentType`, `overallStatus`/`status`) are the C# `Enumeration.DisplayName`
 * values — exact member names, e.g. `"Male"` not `"male"`.
 */

export type CatSex = 'Male' | 'Female' | 'Unknown';
export type DateOfBirthPrecision = 'Exact' | 'Estimated';
export type NeuterStatus = 'Intact' | 'Neutered' | 'Unknown';
export type CatLifeStatus = 'Alive' | 'Deceased';
export type AllergySeverity = 'Mild' | 'Moderate' | 'Severe';
export type ParasiteTreatmentType = 'Deworming' | 'FleaAndTick' | 'Combined';
export type VaccinationStatus = 'Current' | 'DueSoon' | 'Overdue';

// The CAT_SEX_OPTIONS-style runtime option arrays moved to `lib/catEnumOptions.ts` (ARCHITECTURE
// gate second pass), so this module holds only the HTTP layer and its types.

// Route params are decoded by Next, so a crafted `[catId]` such as `..%2F..%2Fx` would otherwise
// rewrite the request path (SECURITY gate, LOW 1). Every cat endpoint builds on this.
function catPath(catId: string): string {
  return `/api/cats/${encodeURIComponent(catId)}`;
}

// ---------------------------------------------------------------------------------------------
// Read-side shapes (HO-CAT-001 §12.3)
// ---------------------------------------------------------------------------------------------

export interface Allergy {
  id: string;
  allergen: string;
  reaction: string;
  severity: AllergySeverity;
  recordedOn: string; // DateOnly, yyyy-MM-dd
}

export interface ChronicCondition {
  id: string;
  condition: string;
  diagnosedOn: string | null;
  notes: string | null;
}

export interface Medication {
  id: string;
  medicationName: string;
  dosage: string;
  frequency: string;
  startedOn: string;
  endsOn: string | null;
}

export interface CatPhoto {
  id: string;
  storageKey: string;
  contentType: string;
  sizeInBytes: number;
  fileName: string;
  isPrimary: boolean;
  uploadedOn: string; // DateTimeOffset, ISO
}

/** Q1 — GET /api/cats/{catId}. The full profile behind CatProfileHeader (UI-7). */
export interface CatProfile {
  id: string;
  ownerId: string;
  name: string;
  species: string;
  sex: CatSex;
  dateOfBirth: string | null;
  dateOfBirthPrecision: DateOfBirthPrecision;
  breed: string | null;
  colourAndMarkings: string | null;
  neuterStatus: NeuterStatus;
  neuteredOn: string | null;
  isHighRisk: boolean;
  handlingNote: string | null;
  lifeStatus: CatLifeStatus;
  deceasedOn: string | null;
  deceasedNote: string | null;
  dietaryNotes: string | null;
  behaviouralNotes: string | null;
  latestWeightKg: number | null;
  hasRecordedAllergies: boolean;
  allergies: Allergy[];
  chronicConditions: ChronicCondition[];
  medications: Medication[];
  photos: CatPhoto[];
}

/**
 * Q2 — GET /api/cats. Carries isHighRisk and handlingNote so no list screen needs a second call
 * (HO-CAT-001 ACCEPTANCE gate A9 fix, 2026-09-27 — handlingNote was missing from this shape even
 * though HO-CAT-001 §4.1/HO-CAT-002 §3 both require it on the list, not only the profile).
 */
export interface CatListItem {
  id: string;
  ownerId: string;
  name: string;
  species: string;
  breed: string | null;
  sex: CatSex;
  isHighRisk: boolean;
  handlingNote: string | null;
  lifeStatus: CatLifeStatus;
}

/** Q3 — GET /api/cats/{catId}/weights. */
export interface CatWeightPoint {
  recordedOn: string;
  kilograms: number;
}

/** Q4 — GET /api/cats/{catId}/vaccination-status. US-CAT-07. */
export interface CatVaccinationStatusItem {
  vaccineId: string;
  vaccineCode: string;
  vaccineName: string;
  status: VaccinationStatus;
  nextDueOn: string | null;
  reason: string | null; // e.g. "NeverAdministered" — internal, never rendered verbatim to a user
}

export interface CatVaccinationStatus {
  catId: string;
  overallStatus: VaccinationStatus;
  earliestNextDueOn: string | null;
  bookingEligible: boolean;
  vaccines: CatVaccinationStatusItem[];
}

/** Q7's shape is not exposed as JSON (the endpoint returns image/png) — no type needed here. */

// ---------------------------------------------------------------------------------------------
// List filters / pagination
// ---------------------------------------------------------------------------------------------

/**
 * HO-CAT-001's list endpoints (`GET /api/cats`, `GET /api/cats/{catId}/weights`,
 * `GET /api/vaccines`) now return the same `PagedResult<T>` envelope the Account side already
 * uses (owner decision 2026-09-26, HO-CAT-001 §1 Amended): `{ items, pageNumber, pageSize,
 * totalCount }`. `pageSize` is capped at 100 server-side — a larger value is clamped, not
 * rejected; `pageSize <= 0` is a 400.
 *
 * This replaces the earlier `pageSize + 1` / `hasNextPage` workaround (CODE_REVIEW gate MEDIUM,
 * HO-CAT-001, 2026-09-26): the server now reports a real total, so the client never has to infer
 * "is there another page" from an extra trimmed row.
 */
export interface PagedResult<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
}

/** Server-side cap (CatsController/VaccinesController `MaxPageSize`) — mirrored here only so
 *  `getCatWeightSeries`'s paging loop below knows how large a single request may ask for. */
const MAX_PAGE_SIZE = 100;

// ---------------------------------------------------------------------------------------------
// GET /api/cats
// ---------------------------------------------------------------------------------------------

export interface GetCatsParams {
  ownerId?: string;
  includeDeceased?: boolean;
  pageNumber?: number;
  pageSize?: number;
}

export async function getCats(params: GetCatsParams = {}): Promise<PagedResult<CatListItem>> {
  const pageNumber = params.pageNumber ?? 1;
  const pageSize = params.pageSize ?? 10;
  const query = new URLSearchParams();
  if (params.ownerId) query.set('ownerId', params.ownerId);
  if (params.includeDeceased) query.set('includeDeceased', 'true');
  query.set('pageNumber', String(pageNumber));
  query.set('pageSize', String(pageSize));

  return apiFetch<PagedResult<CatListItem>>(`/api/cats?${query.toString()}`);
}

export async function getCat(catId: string): Promise<CatProfile> {
  return apiFetch<CatProfile>(`${catPath(catId)}`);
}

// ---------------------------------------------------------------------------------------------
// POST /api/cats — C1, US-CAT-01
// ---------------------------------------------------------------------------------------------

export interface RegisterCatRequest {
  ownerId: string;
  name: string;
  species: string;
  sex: CatSex;
  dateOfBirth: string | null;
  dateOfBirthPrecision: DateOfBirthPrecision;
  breed: string | null;
  colourAndMarkings: string | null;
}

export async function registerCat(request: RegisterCatRequest): Promise<{ catId: string }> {
  return apiFetch<{ catId: string }>('/api/cats', { method: 'POST', body: request });
}

// ---------------------------------------------------------------------------------------------
// PUT /api/cats/{catId}/profile — C2, US-CAT-01
// ---------------------------------------------------------------------------------------------

export interface UpdateCatProfileRequest {
  name: string;
  sex: CatSex;
  dateOfBirth: string | null;
  dateOfBirthPrecision: DateOfBirthPrecision;
  breed: string | null;
  colourAndMarkings: string | null;
}

export async function updateCatProfile(
  catId: string,
  request: UpdateCatProfileRequest,
): Promise<{ catId: string }> {
  return apiFetch<{ catId: string }>(`${catPath(catId)}/profile`, {
    method: 'PUT',
    body: request,
  });
}

// ---------------------------------------------------------------------------------------------
// PUT /api/cats/{catId}/neuter-status — C3, US-CAT-02
// ---------------------------------------------------------------------------------------------

export interface RecordCatNeuterStatusRequest {
  neuterStatus: NeuterStatus;
  neuteredOn: string | null;
}

export async function recordCatNeuterStatus(
  catId: string,
  request: RecordCatNeuterStatusRequest,
): Promise<{ catId: string }> {
  return apiFetch<{ catId: string }>(`${catPath(catId)}/neuter-status`, {
    method: 'PUT',
    body: request,
  });
}

// ---------------------------------------------------------------------------------------------
// PUT /api/cats/{catId}/medical-profile — C5, US-CAT-03
// ---------------------------------------------------------------------------------------------

export interface AllergyInput {
  allergen: string;
  reaction: string;
  severity: AllergySeverity;
  recordedOn: string;
}

export interface ChronicConditionInput {
  condition: string;
  diagnosedOn: string | null;
  notes: string | null;
}

export interface MedicationInput {
  medicationName: string;
  dosage: string;
  frequency: string;
  startedOn: string;
  endsOn: string | null;
}

export interface UpdateCatMedicalProfileRequest {
  allergies: AllergyInput[];
  chronicConditions: ChronicConditionInput[];
  medications: MedicationInput[];
  dietaryNotes: string | null;
  behaviouralNotes: string | null;
}

export async function updateCatMedicalProfile(
  catId: string,
  request: UpdateCatMedicalProfileRequest,
): Promise<{ catId: string }> {
  return apiFetch<{ catId: string }>(`${catPath(catId)}/medical-profile`, {
    method: 'PUT',
    body: request,
  });
}

// ---------------------------------------------------------------------------------------------
// POST /api/cats/{catId}/photos — C6, US-CAT-01 AC3 (HO-CAT-002 §11.6/§20.4 — metadata only)
// ---------------------------------------------------------------------------------------------

export interface AddCatPhotoRequest {
  storageKey: string;
  contentType: string;
  sizeInBytes: number;
  fileName: string;
  isPrimary: boolean;
}

export async function addCatPhoto(
  catId: string,
  request: AddCatPhotoRequest,
): Promise<{ catId: string; photoId: string }> {
  return apiFetch<{ catId: string; photoId: string }>(`${catPath(catId)}/photos`, {
    method: 'POST',
    body: request,
  });
}

// ---------------------------------------------------------------------------------------------
// PUT/DELETE /api/cats/{catId}/high-risk-flag — C7/C8, US-CAT-04
// ---------------------------------------------------------------------------------------------

export interface FlagCatHighRiskRequest {
  handlingNote: string;
  reason: string;
}

export async function flagCatHighRisk(
  catId: string,
  request: FlagCatHighRiskRequest,
): Promise<{ catId: string }> {
  return apiFetch<{ catId: string }>(`${catPath(catId)}/high-risk-flag`, {
    method: 'PUT',
    body: request,
  });
}

export interface ClearCatHighRiskFlagRequest {
  reason: string;
}

export async function clearCatHighRiskFlag(
  catId: string,
  request: ClearCatHighRiskFlagRequest,
): Promise<{ catId: string }> {
  return apiFetch<{ catId: string }>(`${catPath(catId)}/high-risk-flag`, {
    method: 'DELETE',
    body: request,
  });
}

// ---------------------------------------------------------------------------------------------
// POST/GET /api/cats/{catId}/weights — C4/Q3, US-CAT-02
// ---------------------------------------------------------------------------------------------

/**
 * SECURITY gate, MEDIUM (2026-09-26): `recordedByStaffId` is no longer part of the request — the
 * server always takes the caller's own identity (`UserContext.UserId`) and ignores any client
 * value, so the field was removed from `RecordCatWeightRequest` on the backend too.
 */
export interface RecordCatWeightRequest {
  kilograms: number;
  recordedOn: string;
  notes: string | null;
}

export async function recordCatWeight(
  catId: string,
  request: RecordCatWeightRequest,
): Promise<{ catId: string; weightRecordId: string }> {
  return apiFetch<{ catId: string; weightRecordId: string }>(`${catPath(catId)}/weights`, {
    method: 'POST',
    body: request,
  });
}

export interface GetCatWeightsParams {
  pageNumber?: number;
  pageSize?: number;
}

export async function getCatWeights(
  catId: string,
  params: GetCatWeightsParams = {},
): Promise<PagedResult<CatWeightPoint>> {
  const pageNumber = params.pageNumber ?? 1;
  const pageSize = params.pageSize ?? 10;
  const query = new URLSearchParams({
    pageNumber: String(pageNumber),
    pageSize: String(pageSize),
  });
  return apiFetch<PagedResult<CatWeightPoint>>(`${catPath(catId)}/weights?${query.toString()}`);
}

/**
 * The full series for `WeightTrendChart` (US-CAT-02 AC2) — a chart cannot page, so this asks for
 * everything a cat could plausibly have (well above what any cat accrues in a lifetime of
 * visits). `pageSize` is capped at 100 server-side (owner decision 2026-09-26), so a single
 * `pageSize=1000` request no longer returns more than 100 rows — this pages through in batches of
 * `MAX_PAGE_SIZE` instead, using the server-reported `totalCount` to know when to stop, up to the
 * same 1000-row ceiling the chart always asked for.
 */
const WEIGHT_SERIES_MAX_POINTS = 1000;

export async function getCatWeightSeries(catId: string): Promise<CatWeightPoint[]> {
  const points: CatWeightPoint[] = [];
  let pageNumber = 1;

  while (points.length < WEIGHT_SERIES_MAX_POINTS) {
    const page = await getCatWeights(catId, { pageNumber, pageSize: MAX_PAGE_SIZE });
    points.push(...page.items);

    const fetchedSoFar = pageNumber * MAX_PAGE_SIZE;
    if (fetchedSoFar >= page.totalCount || page.items.length === 0) break;
    pageNumber += 1;
  }

  return points.slice(0, WEIGHT_SERIES_MAX_POINTS);
}

// ---------------------------------------------------------------------------------------------
// POST/GET vaccinations — C9/Q4, US-CAT-05, US-CAT-07
// ---------------------------------------------------------------------------------------------

export interface RecordCatVaccinationRequest {
  vaccineId: string;
  batchReference: string;
  batchExpiresOn: string;
  administeredOn: string;
  administeredByVetId: string;
  notes: string | null;
}

export async function recordCatVaccination(
  catId: string,
  request: RecordCatVaccinationRequest,
): Promise<{ catId: string; vaccinationId: string }> {
  return apiFetch<{ catId: string; vaccinationId: string }>(`${catPath(catId)}/vaccinations`, {
    method: 'POST',
    body: request,
  });
}

export async function getCatVaccinationStatus(catId: string): Promise<CatVaccinationStatus> {
  return apiFetch<CatVaccinationStatus>(`${catPath(catId)}/vaccination-status`);
}

// ---------------------------------------------------------------------------------------------
// POST /api/cats/{catId}/parasite-treatments — C10, US-CAT-06
// ---------------------------------------------------------------------------------------------

export interface RecordCatParasiteTreatmentRequest {
  treatmentType: ParasiteTreatmentType;
  productName: string;
  batchReference: string | null;
  administeredOn: string;
  administeredByStaffId: string;
  intervalDays: number;
  notes: string | null;
}

/**
 * The body is exactly `{ treatmentId, nextDueOn }` — HO-CAT-001 §12.4's one exception to "the
 * body is just `{ <resource>Id }`" (owner decision 2026-09-25). No `catId`: the caller already
 * has it, it's the URL parameter. `nextDueOn` is the next-due date the server just computed,
 * rather than the client ever recomputing `administeredOn + intervalDays` itself (ARCHITECTURE
 * rejected that duplication of the aggregate rule, fix `1a4cac4`). Required, not nullable (owner
 * confirmed 2026-09-25) — `intervalDays` is always required on the request, so the aggregate
 * always computes a value.
 */
export interface RecordCatParasiteTreatmentResponse {
  treatmentId: string;
  nextDueOn: string;
}

export async function recordCatParasiteTreatment(
  catId: string,
  request: RecordCatParasiteTreatmentRequest,
): Promise<RecordCatParasiteTreatmentResponse> {
  return apiFetch<RecordCatParasiteTreatmentResponse>(`${catPath(catId)}/parasite-treatments`, {
    method: 'POST',
    body: request,
  });
}

// ---------------------------------------------------------------------------------------------
// POST /api/cats/{catId}/deceased — C11, US-CAT-09
// ---------------------------------------------------------------------------------------------

export interface MarkCatDeceasedRequest {
  deceasedOn: string;
  note: string | null;
}

export async function markCatDeceased(
  catId: string,
  request: MarkCatDeceasedRequest,
): Promise<{ catId: string }> {
  return apiFetch<{ catId: string }>(`${catPath(catId)}/deceased`, {
    method: 'POST',
    body: request,
  });
}

// ---------------------------------------------------------------------------------------------
// POST /api/cats/{catId}/ownership-transfer — C12, US-CAT-08
// ---------------------------------------------------------------------------------------------

export interface TransferCatOwnershipRequest {
  newOwnerId: string;
  reason: string;
}

export async function transferCatOwnership(
  catId: string,
  request: TransferCatOwnershipRequest,
): Promise<{ catId: string }> {
  return apiFetch<{ catId: string }>(`${catPath(catId)}/ownership-transfer`, {
    method: 'POST',
    body: request,
  });
}

// ---------------------------------------------------------------------------------------------
// Binary downloads — GET .../medical-history.pdf (Q5, US-CAT-10), GET .../cage-card (Q7,
// US-CAT-11). Neither is JSON; both are plain URLs an <a>/<img> can point at directly rather
// than a fetch+blob round-trip (identity rides on the session cookie either way).
// ---------------------------------------------------------------------------------------------

export function getCatMedicalHistoryPdfUrl(catId: string): string {
  return getApiUrl(`${catPath(catId)}/medical-history.pdf`);
}

export function getCatCageCardImageUrl(catId: string): string {
  return getApiUrl(`${catPath(catId)}/cage-card`);
}
