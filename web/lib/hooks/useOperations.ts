import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getBackupStatus,
  getRestoreTest,
  getRestoreTests,
  getWebVitalsSummary,
  recordRestoreTest,
  type RecordRestoreTestRequest,
  type WebVitalsSummaryParams,
} from '@/lib/api/operations';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';

/** `lib/hooks/useOperations.ts` — HO-XCT-002 §8.4. Backup status, restore tests, web vitals. */
const retry = (failureCount: number, error: unknown) =>
  !(error instanceof ApiForbiddenError || error instanceof ApiNotFoundError) && failureCount < 1;

export const useBackupStatus = () =>
  useQuery({
    queryKey: ['admin', 'operations', 'backup-status'],
    queryFn: getBackupStatus,
    retry,
  });

export const useRestoreTests = (pageNumber: number, pageSize: number) =>
  useQuery({
    queryKey: ['admin', 'operations', 'restore-tests', pageNumber, pageSize],
    queryFn: () => getRestoreTests({ pageNumber, pageSize }),
    retry,
  });

export const useRestoreTest = (id: string) =>
  useQuery({
    queryKey: ['admin', 'operations', 'restore-test', id],
    queryFn: () => getRestoreTest(id),
    retry,
  });

export function useRecordRestoreTest() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: RecordRestoreTestRequest) => recordRestoreTest(request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'operations'] }),
  });
}

export const useWebVitalsSummary = (params: WebVitalsSummaryParams, enabled: boolean = true) =>
  useQuery({
    enabled,
    queryKey: ['admin', 'web-vitals', params],
    queryFn: () => getWebVitalsSummary(params),
    retry,
  });

const OVERDUE_DISMISSED_KEY = ['admin', 'operations', 'overdue-banner-dismissed'] as const;

/**
 * The overdue-restore-test banner (HO-XCT-002 §11.5). Reads the backup status only when `enabled`
 * (an Administrator on an admin route). "Dismissible per session" is kept in the query cache, not
 * in browser storage: it is cleared at sign-out with everything else and nothing persists.
 */
export function useOverdueRestoreBanner(enabled: boolean) {
  const queryClient = useQueryClient();
  const status = useQuery({
    queryKey: ['admin', 'operations', 'backup-status'],
    queryFn: getBackupStatus,
    enabled,
    retry,
    staleTime: 5 * 60_000,
  });
  const dismissedQuery = useQuery<boolean>({
    queryKey: OVERDUE_DISMISSED_KEY,
    enabled: false,
    initialData: false,
  });
  return {
    overdue: status.data?.isRestoreTestOverdue === true,
    dismissed: dismissedQuery.data === true,
    dismiss: () => queryClient.setQueryData(OVERDUE_DISMISSED_KEY, true),
  };
}
