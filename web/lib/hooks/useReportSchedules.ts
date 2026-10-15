import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ApiError } from '@/lib/api/client';
import {
  createSchedule,
  getSchedule,
  listEligibleRecipients,
  listScheduleRuns,
  listSchedules,
  previewSchedule,
  runScheduleNow,
  transitionSchedule,
  updateSchedule,
  type ReportScheduleInput,
  type ScheduleListQuery,
  type SchedulePreviewInput,
} from '@/lib/api/report-schedules';

/**
 * `lib/hooks/useReportSchedules.ts` — HO-RPT-002 §8.3, US-RPT-06. Keys start with
 * `['report-schedules', …]`. The preview is a MUTATION, not a query, so it is explicit and
 * debounced by the form (ADR-FE-136).
 */
const ROOT = ['report-schedules'] as const;

function retryTransient(failureCount: number, error: unknown): boolean {
  if (error instanceof ApiError && error.status >= 400 && error.status < 500) return false;
  return failureCount < 1;
}

export function useScheduleList(query: ScheduleListQuery) {
  return useQuery({
    queryKey: [...ROOT, 'list', query],
    queryFn: () => listSchedules(query),
    retry: retryTransient,
  });
}

export function useSchedule(id: string) {
  return useQuery({
    queryKey: [...ROOT, 'detail', id],
    queryFn: () => getSchedule(id),
    retry: retryTransient,
  });
}

export function useScheduleRuns(id: string, pageNumber: number) {
  return useQuery({
    queryKey: [...ROOT, 'runs', id, pageNumber],
    queryFn: () => listScheduleRuns(id, pageNumber),
    retry: retryTransient,
  });
}

export function useEligibleRecipients(reportKey: string, branchId: string | null) {
  return useQuery({
    queryKey: [...ROOT, 'recipients', reportKey, branchId],
    queryFn: () => listEligibleRecipients(reportKey, branchId),
    enabled: reportKey !== '',
    retry: retryTransient,
  });
}

export function useSchedulePreview() {
  return useMutation({ mutationFn: (input: SchedulePreviewInput) => previewSchedule(input) });
}

export function useSaveSchedule(scheduleId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (
      input: ReportScheduleInput,
    ): Promise<{ scheduleId?: string; nextRunOn: string | null }> =>
      scheduleId ? updateSchedule(scheduleId, input) : createSchedule(input),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ROOT }),
  });
}

export function useScheduleTransition(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (action: 'pause' | 'resume' | 'cancel') => transitionSchedule(id, action),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ROOT }),
  });
}

export function useRunScheduleNow(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => runScheduleNow(id),
    // The run list and the schedule's last-run fields both change.
    onSettled: () => queryClient.invalidateQueries({ queryKey: ROOT }),
  });
}
