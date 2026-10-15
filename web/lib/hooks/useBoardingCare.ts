import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';
import {
  completeTask,
  getIncidentQueue,
  getStayIncidents,
  getStayMedia,
  getStayObservations,
  getTaskDayList,
  hideStayMedia,
  recordIncident,
  recordIncidentAcknowledgement,
  recordIncidentPhoneCall,
  recordObservation,
  recordStayMedia,
  resolveIncident,
  skipTask,
  type IncidentInput,
  type MediaInput,
  type ObservationInput,
  type TaskDayParams,
} from '@/lib/api/boarding-care';
import type { IncidentStatus, TaskDayList } from '@/lib/api/types';
import { useBranchScope } from './useBranchScope';
import { BOARDING_POLL_MS } from './useStaffBoarding';

/**
 * `lib/hooks/useBoardingCare.ts` — HO-BRD-002 §8.3: the caretaker's task board, observations, media
 * references and incidents. The day list and the incident queue refetch every 30 s (UI-5); a poll
 * swaps the data but never an open sheet's values nor an in-flight completion (ADR-FE-101).
 *
 * Completion is **optimistic** (ADR-FE-100): the card flips to `Done` the moment it is tapped, and
 * rolls back to its exact previous state if the server refuses.
 */

function noRetryOnRefusal(failureCount: number, error: unknown) {
  if (error instanceof ApiForbiddenError || error instanceof ApiNotFoundError) return false;
  return failureCount < 1;
}

export function useTaskDayList(params: TaskDayParams) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['staff', 'boarding', 'tasks', params, branchId],
    queryFn: () => getTaskDayList(params),
    enabled: ready,
    refetchInterval: BOARDING_POLL_MS,
    retry: noRetryOnRefusal,
  });
}

export function useStayObservations(stayId: string, enabled = true) {
  const { ready } = useBranchScope();
  return useQuery({
    queryKey: ['staff', 'boarding', 'observations', stayId],
    queryFn: () => getStayObservations(stayId),
    enabled: ready && enabled && !!stayId,
    retry: noRetryOnRefusal,
  });
}

export function useStayMedia(stayId: string, enabled = true) {
  const { ready } = useBranchScope();
  return useQuery({
    queryKey: ['staff', 'boarding', 'media', stayId],
    queryFn: () => getStayMedia(stayId),
    enabled: ready && enabled && !!stayId,
    retry: noRetryOnRefusal,
  });
}

export function useStayIncidents(stayId: string, enabled = true) {
  const { ready } = useBranchScope();
  return useQuery({
    queryKey: ['staff', 'boarding', 'stay-incidents', stayId],
    queryFn: () => getStayIncidents(stayId),
    enabled: ready && enabled && !!stayId,
    retry: noRetryOnRefusal,
  });
}

export function useIncidentQueue(status?: IncidentStatus) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['staff', 'boarding', 'incidents', status ?? 'all', branchId],
    queryFn: () => getIncidentQueue(status),
    enabled: ready,
    refetchInterval: BOARDING_POLL_MS,
    retry: noRetryOnRefusal,
  });
}

function useInvalidateCare() {
  const queryClient = useQueryClient();
  return () => {
    void queryClient.invalidateQueries({ queryKey: ['staff', 'boarding'] });
  };
}

interface CompleteVariables {
  taskId: string;
  /** The signed-in member of staff's name, for the optimistic "done by" line. */
  completedByName: string;
  signal?: AbortSignal;
}

/** ADR-FE-100 — one tap, no dialog. Optimistic with rollback; the server's reason is surfaced by
 *  the caller from the mutation error. */
export function useCompleteTask() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (variables: CompleteVariables) => completeTask(variables.taskId, variables.signal),
    onMutate: async (variables) => {
      await queryClient.cancelQueries({ queryKey: ['staff', 'boarding', 'tasks'] });
      const snapshots = queryClient.getQueriesData<TaskDayList>({
        queryKey: ['staff', 'boarding', 'tasks'],
      });
      const completedOn = new Date().toISOString();
      for (const [key, data] of snapshots) {
        if (!data) continue;
        queryClient.setQueryData<TaskDayList>(key, {
          ...data,
          units: data.units.map((unit) => ({
            ...unit,
            tasks: unit.tasks.map((task) =>
              task.id === variables.taskId
                ? {
                    ...task,
                    status: 'Done' as const,
                    isOverdue: false,
                    minutesLate: null,
                    completedOn,
                    completedByName: variables.completedByName,
                  }
                : task,
            ),
          })),
        });
      }
      return { snapshots };
    },
    onError: (_error, _variables, context) => {
      for (const [key, data] of context?.snapshots ?? []) queryClient.setQueryData(key, data);
    },
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: ['staff', 'boarding'] });
    },
  });
}

export function useSkipTask() {
  const invalidate = useInvalidateCare();
  return useMutation({
    mutationFn: (input: { taskId: string; reason: string }) => skipTask(input.taskId, input.reason),
    onSuccess: invalidate,
  });
}

export function useRecordObservation(stayId: string) {
  const invalidate = useInvalidateCare();
  return useMutation({
    mutationFn: (input: ObservationInput) => recordObservation(stayId, input),
    onSuccess: invalidate,
  });
}

export function useRecordStayMedia(stayId: string) {
  const invalidate = useInvalidateCare();
  return useMutation({
    mutationFn: (input: MediaInput) => recordStayMedia(stayId, input),
    onSuccess: invalidate,
  });
}

export function useHideStayMedia(stayId: string) {
  const invalidate = useInvalidateCare();
  return useMutation({
    mutationFn: (input: { mediaId: string; reason: string }) =>
      hideStayMedia(stayId, input.mediaId, input.reason),
    onSuccess: invalidate,
  });
}

export function useRecordIncident(stayId: string) {
  const invalidate = useInvalidateCare();
  return useMutation({
    mutationFn: (input: IncidentInput) => recordIncident(stayId, input),
    onSuccess: invalidate,
  });
}

export function useRecordIncidentAcknowledgement() {
  const invalidate = useInvalidateCare();
  return useMutation({
    mutationFn: (incidentId: string) => recordIncidentAcknowledgement(incidentId),
    onSuccess: invalidate,
  });
}

export function useRecordIncidentPhoneCall() {
  const invalidate = useInvalidateCare();
  return useMutation({
    mutationFn: (input: { incidentId: string; outcome: string; ownerReached: boolean }) =>
      recordIncidentPhoneCall(input.incidentId, input.outcome, input.ownerReached),
    onSuccess: invalidate,
  });
}

export function useResolveIncident() {
  const invalidate = useInvalidateCare();
  return useMutation({
    mutationFn: (input: { incidentId: string; resolution: string }) =>
      resolveIncident(input.incidentId, input.resolution),
    onSuccess: invalidate,
  });
}
