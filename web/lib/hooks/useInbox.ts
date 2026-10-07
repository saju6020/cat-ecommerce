import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getInbox,
  getInboxUnreadCount,
  markInboxItemRead,
  type InboxParams,
} from '@/lib/api/inbox';

/** How often the unread badge re-checks (ADR-FE-019 in the handover; ADR-FE-159 in this repo). */
export const UNREAD_COUNT_POLL_MS = 60_000;

/**
 * HO-NTF-002 §8.3 / ADR-FE-159. Polls every 60 s and on window focus; TanStack Query pauses an
 * interval while the tab is hidden (`refetchIntervalInBackground: false`, its default, stated here
 * because the ADR depends on it). No WebSocket, no SSE, no push. `staleTime: 0` so a focus event
 * always refetches rather than being swallowed by the app-wide 30 s stale window.
 */
export function useUnreadCount(options: { enabled?: boolean } = {}) {
  return useQuery({
    queryKey: ['inbox', 'unread-count'],
    queryFn: ({ signal }) => getInboxUnreadCount(signal),
    refetchInterval: UNREAD_COUNT_POLL_MS,
    refetchIntervalInBackground: false,
    refetchOnWindowFocus: true,
    staleTime: 0,
    retry: false,
    enabled: options.enabled ?? true,
  });
}

export function useInboxList(params: InboxParams) {
  return useQuery({
    queryKey: ['inbox', 'list', params],
    queryFn: () => getInbox(params),
    placeholderData: keepPreviousData,
  });
}

/** Opening an item marks it read (OQ-3); both the list and the badge count are invalidated. */
export function useMarkInboxItemRead() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => markInboxItemRead(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['inbox'] }),
  });
}
