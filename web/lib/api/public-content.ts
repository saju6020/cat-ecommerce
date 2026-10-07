import { apiFetch } from './client';
import type { ContentKind, BannerPlacement } from './admin-content';

/** `lib/api/public-content.ts` — HO-ADM-002 §8.2. Public reads of published content/policies, and
 *  the customer-facing policy-acceptance endpoints (HO-ADM-001 §12.4). */

export interface PublishedContentPage {
  kind: ContentKind;
  slug: string;
  title: string;
  body: string;
  publishedOn: string;
}

export async function getPublishedContent(
  kind: ContentKind,
  slug: string,
): Promise<PublishedContentPage> {
  return apiFetch<PublishedContentPage>(
    `/api/content/${encodeURIComponent(kind.toLowerCase())}/${encodeURIComponent(slug)}`,
  );
}

export interface PublishedBanner {
  slug: string;
  title: string;
  body: string;
  placement: BannerPlacement;
  linkUrl: string | null;
}

export async function getPublishedBanners(placement?: BannerPlacement): Promise<PublishedBanner[]> {
  const query = placement ? `?placement=${encodeURIComponent(placement)}` : '';
  return apiFetch<PublishedBanner[]>(`/api/content/banners${query}`);
}

export interface PublishedFaq {
  slug: string;
  title: string;
  body: string;
  sortOrder: number;
}

export async function getPublishedFaqs(): Promise<PublishedFaq[]> {
  return apiFetch<PublishedFaq[]>('/api/content/faqs');
}

export interface PublicPolicy {
  key: string;
  title: string;
  versionNumber: number;
  body: string;
  publishedOn: string;
}

export async function getPublicPolicy(key: string): Promise<PublicPolicy> {
  return apiFetch<PublicPolicy>(`/api/policies/${encodeURIComponent(key)}`);
}

export interface PendingPolicyAcceptance {
  key: string;
  title: string;
  versionNumber: number;
}

export async function getPendingPolicyAcceptances(): Promise<PendingPolicyAcceptance[]> {
  return apiFetch<PendingPolicyAcceptance[]>('/api/customers/me/policy-acceptances/pending');
}

export async function acceptPolicyVersion(policyKey: string, versionNumber: number): Promise<void> {
  await apiFetch<void>('/api/customers/me/policy-acceptances', {
    method: 'POST',
    body: { policyKey, versionNumber },
  });
}
