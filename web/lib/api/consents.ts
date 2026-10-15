import { apiFetch } from './client';
import { resolveStoredFile, type StoredFileViewModel } from './files';

/**
 * `lib/api/consents.ts` — HO-EMR-002 §8.2 (US-CLN-21). Typed against `StaffConsentsController` and
 * `ConsentViewModels.cs` on master. A cross-branch consent is a `404`. The signature's coordinates
 * are never returned as JSON — the detail carries a `signatureUrl` to a polyline-only SVG.
 */

export type ConsentKind = 'Surgery' | 'Anaesthesia' | 'Euthanasia';
export type ConsentMethod = 'OnScreenSignature' | 'SignedUpload';
export type ConsentStatus = 'Recorded' | 'Withdrawn';
export type SignerRelationship = 'Owner' | 'AuthorisedAgent';

/** The current published wording — `body` is `LimitedMarkup` (never HTML). */
export interface ConsentForm {
  policyKey: string;
  versionNumber: number;
  title: string;
  body: string;
}

export interface Consent {
  id: string;
  version: number;
  catId: string;
  appointmentId: string;
  kind: ConsentKind;
  procedureDescription: string;
  method: ConsentMethod;
  signedByName: string;
  signerRelationship: SignerRelationship;
  signedByCustomerId: string | null;
  signedOn: string;
  policyKey: string;
  policyVersionNumber: number;
  capturedBy: string;
  witness: string | null;
  status: ConsentStatus;
  withdrawnOn: string | null;
  withdrawnBy: string | null;
  withdrawalReason: string | null;
  branchTimeZoneId: string;
  signatureUrl: string | null;
  file: StoredFileViewModel | null;
}

export interface ConsentListItem {
  id: string;
  appointmentId: string;
  kind: ConsentKind;
  method: ConsentMethod;
  status: ConsentStatus;
  signedOn: string;
  policyVersionNumber: number;
  signedByName: string;
}

/** Strokes of `[x, y]` points, each coordinate in `[0, 1]` at 4 decimals (ADR-FE-054). */
export type SignatureStrokes = number[][][];

export interface RecordConsentRequest {
  catId: string;
  appointmentId: string;
  kind: ConsentKind;
  procedureDescription: string;
  method: ConsentMethod;
  signature?: SignatureStrokes;
  fileId?: string;
  signedByName: string;
  signerRelationship: SignerRelationship;
  signedByCustomerId?: string;
  policyVersionNumber: number;
  witnessStaffId?: string;
}

export function getConsentForm(kind: ConsentKind): Promise<ConsentForm> {
  return apiFetch(`/api/staff/consent-forms/${kind}`);
}

export function recordConsent(request: RecordConsentRequest): Promise<{ consentId: string }> {
  return apiFetch('/api/staff/consents', { method: 'POST', body: request });
}

export async function getConsent(consentId: string): Promise<Consent> {
  const consent = await apiFetch<Consent>(`/api/staff/consents/${consentId}`);
  // `file.url` is signed relative to the API origin -- anchor it (FIL backlog 25).
  return { ...consent, file: resolveStoredFile(consent.file) };
}

export function getCatConsents(catId: string): Promise<ConsentListItem[]> {
  return apiFetch(`/api/staff/cats/${catId}/consents`);
}

export function withdrawConsent(consentId: string, reason: string): Promise<void> {
  return apiFetch(`/api/staff/consents/${consentId}/withdraw`, {
    method: 'POST',
    body: { reason },
  });
}
