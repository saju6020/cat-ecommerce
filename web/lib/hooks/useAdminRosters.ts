import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getBranchRosters,
  setRosterPattern,
  cancelScheduledRosterPattern,
  getRosterExceptions,
  createRosterException,
  cancelRosterException,
  getAvailability,
  type SetRosterPatternRequest,
  type CreateRosterExceptionRequest,
  type GetAvailabilityParams,
} from '@/lib/api/admin-rosters';
import { ApiForbiddenError } from '@/lib/api/client';

function noRetryOn403(failureCount: number, error: unknown) {
  return !(error instanceof ApiForbiddenError) && failureCount < 1;
}

export function useBranchRosters(branchId: string) {
  return useQuery({
    queryKey: ['admin', 'branches', branchId, 'rosters'],
    queryFn: () => getBranchRosters(branchId),
    enabled: !!branchId,
    retry: noRetryOn403,
  });
}

export function useSetRosterPattern(branchId: string, staffAccountId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: SetRosterPatternRequest) =>
      setRosterPattern(branchId, staffAccountId, request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['admin', 'branches', branchId, 'rosters'] });
      void queryClient.invalidateQueries({
        queryKey: ['admin', 'branches', branchId, 'availability'],
      });
    },
  });
}

export function useCancelScheduledRosterPattern(branchId: string, staffAccountId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => cancelScheduledRosterPattern(branchId, staffAccountId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['admin', 'branches', branchId, 'rosters'] });
    },
  });
}

export function useRosterExceptions(branchId: string, params: { from?: string; to?: string } = {}) {
  return useQuery({
    queryKey: ['admin', 'branches', branchId, 'roster-exceptions', params],
    queryFn: () => getRosterExceptions(branchId, params),
    enabled: !!branchId,
    retry: noRetryOn403,
  });
}

export function useCreateRosterException(branchId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateRosterExceptionRequest) => createRosterException(branchId, request),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: ['admin', 'branches', branchId, 'roster-exceptions'],
      });
      void queryClient.invalidateQueries({
        queryKey: ['admin', 'branches', branchId, 'availability'],
      });
    },
  });
}

export function useCancelRosterException(branchId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (rosterExceptionId: string) => cancelRosterException(branchId, rosterExceptionId),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: ['admin', 'branches', branchId, 'roster-exceptions'],
      });
    },
  });
}

export function useAvailability(branchId: string, params: GetAvailabilityParams) {
  return useQuery({
    queryKey: ['admin', 'branches', branchId, 'availability', params],
    queryFn: () => getAvailability(branchId, params),
    enabled: !!branchId && !!params.from && !!params.to,
    retry: noRetryOn403,
  });
}
