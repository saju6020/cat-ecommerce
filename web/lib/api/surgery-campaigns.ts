import { apiFetch } from './client';
import { queryString } from './formulary';
import type { PagedResult } from './types';
import type { Money } from '@/lib/format/money';

/**
 * `lib/api/surgery-campaigns.ts` — HO-SRG-002 §8.2. Built to `StaffSurgeryCampaignsController`,
 * `SurgeryCampaignViewModels.cs` and the request records in `SurgeryRequests.cs`. The list is a
 * `PagedResult` and the cases response keeps `cases` + `totals` and adds paging members (HO-SRG-001
 * §1 Amended (18)). A campaign is deactivated, never deleted.
 */

export interface CampaignPrice {
  serviceId: string;
  serviceName: string;
  price: Money;
}

export interface SurgeryCampaign {
  id: string;
  version: number;
  code: string;
  name: string;
  description: string;
  validFrom: string;
  validTo: string;
  branchIds: string[];
  prices: CampaignPrice[];
  maxCases: number | null;
  casesBooked: number;
  casesRemaining: number | null;
  isActive: boolean;
  pricesLocked: boolean;
}

export interface ApplicableCampaign {
  id: string;
  code: string;
  name: string;
  validFrom: string;
  validTo: string;
  price: Money;
  casesRemaining: number | null;
}

export interface CampaignCase {
  surgeryId: string;
  catId: string;
  catName: string;
  procedure: string;
  startsOn: string;
  status: string;
  outcome: string | null;
  campaignPrice: Money;
  standardPrice: Money | null;
  eligibilityNote: string | null;
}

export interface CampaignCases {
  campaignId: string;
  code: string;
  name: string;
  cases: CampaignCase[];
  totals: { count: number; campaignTotal: Money; standardTotal: Money };
  pageNumber: number;
  pageSize: number;
  totalCases: number;
}

export interface CampaignInput {
  name: string;
  description?: string;
  validFrom: string;
  validTo: string;
  branchIds: string[];
  prices: Array<{ serviceId: string; amount: number }>;
  maxCases?: number;
}

const base = '/api/staff/surgery-campaigns';

export function listSurgeryCampaigns(params: {
  activeOnly?: boolean;
  pageNumber?: number;
  pageSize?: number;
}): Promise<PagedResult<SurgeryCampaign>> {
  return apiFetch(`${base}${queryString({ ...params })}`);
}

export function getApplicableCampaigns(params: {
  serviceId: string;
  date: string;
}): Promise<ApplicableCampaign[]> {
  return apiFetch(`${base}/applicable${queryString({ ...params })}`);
}

export function getCampaignCases(
  id: string,
  params: { from?: string; to?: string; pageNumber?: number; pageSize?: number },
): Promise<CampaignCases> {
  return apiFetch(`${base}/${encodeURIComponent(id)}/cases${queryString({ ...params })}`);
}

export function createSurgeryCampaign(
  body: CampaignInput & { code: string },
): Promise<{ campaignId: string }> {
  return apiFetch(base, { method: 'POST', body });
}

export function updateSurgeryCampaign(
  id: string,
  body: CampaignInput & { expectedVersion: number },
): Promise<void> {
  return apiFetch(`${base}/${encodeURIComponent(id)}`, { method: 'PUT', body });
}

export function setSurgeryCampaignActive(id: string, isActive: boolean): Promise<void> {
  return apiFetch(`${base}/${encodeURIComponent(id)}/active`, {
    method: 'POST',
    body: { isActive },
  });
}
