import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getNotificationTemplate,
  getNotificationTemplates,
  previewNotificationTemplate,
  resetNotificationTemplate,
  saveNotificationTemplate,
  type NotificationTemplateDetail,
  type PreviewTemplateRequest,
  type SaveNotificationTemplateRequest,
} from '@/lib/api/admin-notification-templates';
import { ApiForbiddenError } from '@/lib/api/client';

function noRetryOn403(failureCount: number, error: unknown) {
  return !(error instanceof ApiForbiddenError) && failureCount < 1;
}

/** Milliseconds the editor waits after the last keystroke before asking for a preview (ADR-FE-163). */
export const PREVIEW_DEBOUNCE_MS = 600;

export function useNotificationTemplates() {
  return useQuery({
    queryKey: ['admin', 'notification-templates'],
    queryFn: getNotificationTemplates,
    retry: noRetryOn403,
  });
}

export function useNotificationTemplate(key: string) {
  return useQuery({
    queryKey: ['admin', 'notification-templates', key],
    queryFn: () => getNotificationTemplate(key),
    enabled: !!key,
    // An editor holds unsaved work: a focus refetch that fails (or changes data) must not disturb it.
    refetchOnWindowFocus: false,
    retry: noRetryOn403,
  });
}

/**
 * HO-NTF-002 §8.3 — the live preview. Keyed on the exact request body (TanStack hashes the key
 * deterministically, which is the "stable hash"), `placeholderData: keepPreviousData` so the panel
 * never flashes empty between keystrokes, and the query's `AbortSignal` is passed through to
 * `client.ts` so a newer preview cancels an older in-flight one (TanStack aborts a query whose
 * observer has moved to another key once the signal has been consumed). `retry: false`: a `400`
 * is the server telling the administrator what is wrong, not something to repeat.
 */
export function useTemplatePreview(key: string, request: PreviewTemplateRequest, enabled: boolean) {
  return useQuery({
    queryKey: ['admin', 'notification-templates', key, 'preview', request],
    queryFn: ({ signal }) => previewNotificationTemplate(key, request, signal),
    enabled: enabled && !!key,
    placeholderData: keepPreviousData,
    retry: false,
    staleTime: 60_000,
  });
}

/**
 * Save and reset resolve with the FRESH template (re-read after the write), which also becomes the
 * cached detail. The editor needs the new `contentVersion` immediately to send as the next
 * `expectedContentVersion`, and a re-read is the only way to learn it (the write returns `204`).
 */
async function writeThenReload(
  queryClient: ReturnType<typeof useQueryClient>,
  key: string,
  write: () => Promise<void>,
): Promise<NotificationTemplateDetail> {
  await write();
  const fresh = await getNotificationTemplate(key);
  queryClient.setQueryData(['admin', 'notification-templates', key], fresh);
  await queryClient.invalidateQueries({
    queryKey: ['admin', 'notification-templates'],
    exact: true,
  });
  return fresh;
}

export function useSaveNotificationTemplate(key: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: SaveNotificationTemplateRequest) =>
      writeThenReload(queryClient, key, () => saveNotificationTemplate(key, request)),
  });
}

export function useResetNotificationTemplate(key: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (expectedContentVersion: number) =>
      writeThenReload(queryClient, key, () =>
        resetNotificationTemplate(key, expectedContentVersion),
      ),
  });
}

/** Re-reads the template on demand (the conflict banner's **Reload**). */
export function useReloadNotificationTemplate(key: string) {
  const queryClient = useQueryClient();
  return () =>
    queryClient.fetchQuery({
      queryKey: ['admin', 'notification-templates', key],
      queryFn: () => getNotificationTemplate(key),
      staleTime: 0,
    });
}
