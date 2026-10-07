import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getNotificationDetail,
  getNotificationLog,
  getNotificationSummary,
  resendNotification,
  type NotificationLogParams,
} from '@/lib/api/notifications-log';
import { ApiForbiddenError } from '@/lib/api/client';

function noRetryOn403(failureCount: number, error: unknown) {
  return !(error instanceof ApiForbiddenError) && failureCount < 1;
}

export const SUMMARY_REFETCH_MS = 30_000;

export function useNotificationLog(params: NotificationLogParams, enabled = true) {
  return useQuery({
    queryKey: ['notifications', 'log', params],
    queryFn: () => getNotificationLog(params),
    enabled,
    placeholderData: keepPreviousData,
    retry: noRetryOn403,
  });
}

export function useNotificationDetail(id: string) {
  return useQuery({
    queryKey: ['notifications', 'log', id],
    queryFn: () => getNotificationDetail(id),
    enabled: !!id,
    retry: noRetryOn403,
  });
}

/** Refetched every 30 s (§11.5); the interval pauses while the tab is hidden. */
export function useNotificationSummary() {
  return useQuery({
    queryKey: ['notifications', 'summary'],
    queryFn: getNotificationSummary,
    refetchInterval: SUMMARY_REFETCH_MS,
    retry: noRetryOn403,
  });
}

/** A resend invalidates the detail and the log (§8.3). */
export function useResendNotification(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (channel?: string) => resendNotification(id, channel),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['notifications'] }),
  });
}
