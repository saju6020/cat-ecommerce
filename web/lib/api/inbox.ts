import { apiFetch } from './client';
import type { PagedResult } from './types';

/**
 * `lib/api/inbox.ts` — HO-NTF-002 §8.2. The signed-in customer's in-app inbox (HO-NTF-001 §12.4,
 * verified against `CustomerInboxController.cs` and `InboxItemViewModel`). `title` is nullable on
 * the wire (an in-app copy of an SMS-only template has none); `body` and `title` are customer- or
 * administrator-influenced text and are shown as plain text only (ADR-FE-022).
 */
export interface InboxItem {
  id: string;
  title: string | null;
  body: string;
  linkPath: string | null;
  category: string;
  createdOn: string;
  readOn: string | null;
}

export interface InboxParams {
  unreadOnly?: boolean;
  pageNumber?: number;
  pageSize?: number;
}

export const INBOX_PAGE_SIZE = 20; // OQ-2

export async function getInbox(params: InboxParams = {}): Promise<PagedResult<InboxItem>> {
  const query = new URLSearchParams();
  if (params.unreadOnly) query.set('unreadOnly', 'true');
  if (params.pageNumber) query.set('pageNumber', String(params.pageNumber));
  query.set('pageSize', String(params.pageSize ?? INBOX_PAGE_SIZE));
  return apiFetch<PagedResult<InboxItem>>(`/api/customers/me/inbox?${query.toString()}`);
}

export async function getInboxUnreadCount(signal?: AbortSignal): Promise<{ count: number }> {
  return apiFetch<{ count: number }>('/api/customers/me/inbox/unread-count', { signal });
}

export async function markInboxItemRead(id: string): Promise<void> {
  await apiFetch<void>(`/api/customers/me/inbox/${encodeURIComponent(id)}/read`, {
    method: 'POST',
  });
}
