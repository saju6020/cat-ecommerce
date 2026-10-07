import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { getClinicQueue, registerWalkIn, type RegisterWalkInRequest } from '@/lib/api/front-desk';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';
import { useBranchScope } from './useBranchScope';
import { STAFF_POLL_MS } from './useStaffAppointments';

/** `lib/hooks/useFrontDesk.ts` — HO-APT-002 §8.3: `['staff','queue', branch, performerId]`, 30 s. */

export function useClinicQueue(performerId: string | undefined) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['staff', 'queue', branchId, performerId ?? 'all'],
    queryFn: () => getClinicQueue(performerId),
    enabled: ready,
    refetchInterval: STAFF_POLL_MS,
    retry: (failureCount, error) =>
      !(error instanceof ApiForbiddenError || error instanceof ApiNotFoundError) &&
      failureCount < 1,
  });
}

export function useRegisterWalkIn() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: RegisterWalkInRequest) => registerWalkIn(request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['staff', 'queue'] });
      void queryClient.invalidateQueries({ queryKey: ['staff', 'day'] });
    },
  });
}
