import { apiFetch } from './client';
import { queryString } from './formulary';
import type { RepeatRequest } from './prescriptions';
import type { QuantityValue } from '@/lib/format/units';

/**
 * `lib/api/my-prescriptions.ts` — HO-PHM-002 §8.2. Built to `CustomerPrescriptionsController`,
 * `CustomerDietAuthorisationsController` and `PharmacyCustomerViewModels.cs`. The owner never receives
 * a batch or lot number, a safety record or any staff detail. The list endpoints are NOT paged
 * (a plain array), so "Past" is paged on the client (HO-PHM-002 §1 Amended).
 */
export interface MyPrescriptionMedicine {
  name: string;
  strength: string;
  form: string;
  isControlled: boolean;
}

export interface MyRepeat {
  isRepeatable: boolean;
  suppliesLeft: number;
  validUntil: string;
  canRequest: boolean;
  /** The server's sentence — shown verbatim. */
  cannotRequestReason: string | null;
  /** Why not, as a code — read only to decide whether to offer "Book a consultation". */
  cannotRequestCode: string | null;
  openRequest: RepeatRequest | null;
}

export interface MyPrescription {
  id: string;
  catId: string;
  catName: string;
  medicine: MyPrescriptionMedicine;
  dose: QuantityValue;
  route: string;
  frequency: string;
  remainingDays: number;
  courseEndsOn: string;
  issuingVetName: string;
  issuedOn: string;
  status: string;
  isActive: boolean;
  repeat: MyRepeat;
}

export interface MyDispensing {
  dispensedOn: string;
  branchName: string;
  quantity: QuantityValue;
}

export interface MyPrescriptionDetail extends MyPrescription {
  instructions: string;
  dispensings: MyDispensing[];
  /** Server-built absolute link; the portal builds `/book?catId=` itself (HO-PHM-002 §1 Amended). */
  bookLink: string;
}

export interface MyDietAuthorisation {
  id: string;
  catId: string;
  catName: string;
  productId: string;
  productName: string;
  validFrom: string;
  validTo: string;
  status: 'Active' | 'Expired' | 'Revoked';
  authorisedByName: string;
}

export type MyScope = 'active' | 'past';

const me = '/api/customers/me';

export function listMyPrescriptions(params: {
  scope: MyScope;
  catId?: string;
}): Promise<MyPrescription[]> {
  return apiFetch(`${me}/prescriptions${queryString({ ...params })}`);
}

export function getMyPrescription(id: string): Promise<MyPrescriptionDetail> {
  return apiFetch(`${me}/prescriptions/${id}`);
}

export function requestRepeat(
  id: string,
  body: { preferredBranchId: string; note?: string | null },
): Promise<{ repeatRequestId: string }> {
  return apiFetch(`${me}/prescriptions/${id}/repeat-requests`, { method: 'POST', body });
}

export function withdrawRepeat(id: string, requestId: string): Promise<void> {
  return apiFetch(`${me}/prescriptions/${id}/repeat-requests/${requestId}`, { method: 'DELETE' });
}

export function listMyDietAuthorisations(params: {
  scope: MyScope;
  catId?: string;
}): Promise<MyDietAuthorisation[]> {
  return apiFetch(`${me}/diet-authorisations${queryString({ ...params })}`);
}
