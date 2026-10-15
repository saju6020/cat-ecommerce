import { apiFetch } from './client';
import { queryString } from './formulary';
import type { PagedResult } from './types';

/**
 * `lib/api/my-surgeries.ts` — HO-SRG-002 §8.2. The owner's view, built to `CustomerSurgeriesController`
 * and `CustomerSurgeryDetailViewModel`. The status is the owner-facing one (`Scheduled`, `InCare` for
 * pre-op / in theatre / recovery, `Discharged`, `Cancelled`). Recovery progress and clinical notes are
 * deliberately not here (reception relays them). Another customer's surgery is a `404`.
 */

export type MySurgeryStatus = 'Scheduled' | 'InCare' | 'Discharged' | 'Cancelled';

export interface MySurgerySummary {
  id: string;
  catName: string;
  procedure: string;
  startsOn: string;
  admitBy: string;
  status: MySurgeryStatus;
  branch: string;
  branchTimeZoneId: string;
}

export interface MySurgeryFollowUp {
  kind: string;
  dueOn: string;
  /** `Pending`, `Done` or `Cancelled`. */
  status: string;
}

export interface MySurgeryDetail extends MySurgerySummary {
  catId: string;
  fastingInstructions: string;
  postOpInstructions: string | null;
  followUps: MySurgeryFollowUp[];
  /** The server's booking link for a follow-up (`/book?...`). */
  bookFollowUpLink: string;
}

export function listMySurgeries(params: {
  scope: 'upcoming' | 'past';
  pageNumber?: number;
  pageSize?: number;
}): Promise<PagedResult<MySurgerySummary>> {
  return apiFetch(`/api/customers/me/surgeries${queryString({ ...params })}`);
}

export function getMySurgery(id: string): Promise<MySurgeryDetail> {
  return apiFetch(`/api/customers/me/surgeries/${encodeURIComponent(id)}`);
}
