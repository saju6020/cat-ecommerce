import { apiFetch } from './client';

/** `lib/api/admin-content.ts` — HO-ADM-002 §8.2. Content pages/FAQs/banners and policy documents
 *  (HO-ADM-001 §8.8/§12.4, ADR-052). Bodies are `LimitedMarkup` text — never HTML. */

export type ContentKind = 'Page' | 'Faq' | 'Banner';
export type ContentStatus = 'Draft' | 'Published' | 'Unpublished';
export type BannerPlacement = 'Home' | 'Shop' | 'Portal';
export const BANNER_PLACEMENTS: BannerPlacement[] = ['Home', 'Shop', 'Portal'];

export interface ContentItem {
  id: string;
  kind: ContentKind;
  slug: string;
  title: string;
  draftBody: string;
  publishedBody: string | null;
  publishedOn: string | null;
  status: ContentStatus;
  placement: BannerPlacement | null;
  linkUrl: string | null;
  visibleFrom: string | null;
  visibleTo: string | null;
  sortOrder: number | null;
}

export interface GetAdminContentParams {
  kind?: ContentKind;
  status?: ContentStatus;
}

export async function getAdminContentList(
  params: GetAdminContentParams = {},
): Promise<ContentItem[]> {
  const query = new URLSearchParams();
  if (params.kind) query.set('kind', params.kind);
  if (params.status) query.set('status', params.status);
  const qs = query.toString();
  return apiFetch<ContentItem[]>(`/api/admin/content${qs ? `?${qs}` : ''}`);
}

export async function getAdminContentItem(contentId: string): Promise<ContentItem> {
  return apiFetch<ContentItem>(`/api/admin/content/${encodeURIComponent(contentId)}`);
}

export interface CreateContentItemRequest {
  kind: ContentKind;
  slug: string;
  title: string;
  draftBody: string;
  placement?: BannerPlacement | null;
  linkUrl?: string | null;
  visibleFrom?: string | null;
  visibleTo?: string | null;
  sortOrder?: number | null;
}

export async function createContentItem(
  request: CreateContentItemRequest,
): Promise<{ contentId: string }> {
  return apiFetch<{ contentId: string }>('/api/admin/content', { method: 'POST', body: request });
}

export interface EditContentDraftRequest {
  title: string;
  draftBody: string;
  placement?: BannerPlacement | null;
  linkUrl?: string | null;
  visibleFrom?: string | null;
  visibleTo?: string | null;
  sortOrder?: number | null;
}

export async function editContentDraft(
  contentId: string,
  request: EditContentDraftRequest,
): Promise<void> {
  await apiFetch<void>(`/api/admin/content/${encodeURIComponent(contentId)}`, {
    method: 'PUT',
    body: request,
  });
}

export async function publishContentItem(contentId: string): Promise<void> {
  await apiFetch<void>(`/api/admin/content/${encodeURIComponent(contentId)}/publish`, {
    method: 'POST',
  });
}

export async function unpublishContentItem(contentId: string): Promise<void> {
  await apiFetch<void>(`/api/admin/content/${encodeURIComponent(contentId)}/unpublish`, {
    method: 'POST',
  });
}

export interface PolicyVersion {
  versionNumber: number;
  body: string;
  publishedOn: string;
  changeSummary: string;
}

export interface PolicyDocument {
  key: string;
  title: string;
  requiresAcceptance: boolean;
  currentVersionNumber: number | null;
  versions: PolicyVersion[];
}

export async function getAdminPolicies(): Promise<PolicyDocument[]> {
  return apiFetch<PolicyDocument[]>('/api/admin/policies');
}

export interface CreatePolicyDocumentRequest {
  key: string;
  title: string;
  requiresAcceptance: boolean;
}

export async function createPolicyDocument(
  request: CreatePolicyDocumentRequest,
): Promise<{ policyKey: string }> {
  return apiFetch<{ policyKey: string }>('/api/admin/policies', { method: 'POST', body: request });
}

export interface PublishPolicyVersionRequest {
  body: string;
  changeSummary: string;
}

export async function publishPolicyVersion(
  key: string,
  request: PublishPolicyVersionRequest,
): Promise<{ versionNumber: number }> {
  return apiFetch<{ versionNumber: number }>(
    `/api/admin/policies/${encodeURIComponent(key)}/versions`,
    { method: 'POST', body: request },
  );
}
