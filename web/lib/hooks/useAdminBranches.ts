import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getAdminBranches,
  getAdminBranch,
  createBranch,
  updateBranchDetails,
  setBranchWeeklyHours,
  setBranchActivation,
  getBranchClosures,
  createBranchClosure,
  cancelBranchClosure,
  getBranchRooms,
  createRoom,
  updateRoom,
  getBranchCalendar,
  getStaffBranchAssignment,
  assignStaffBranches,
  type CreateBranchRequest,
  type UpdateBranchDetailsRequest,
  type OpeningInterval,
  type CreateBranchClosureRequest,
  type CreateRoomRequest,
  type UpdateRoomRequest,
  type AssignStaffBranchesRequest,
} from '@/lib/api/admin-branches';
import { ApiForbiddenError } from '@/lib/api/client';

/** `lib/hooks/useAdminBranches.ts` — HO-ADM-002 §8.3/§11.2. Every query key starts with `'admin'`
 *  so `useSetActiveBranch()` can reset every branch-scoped read in one place. */

function noRetryOn403(failureCount: number, error: unknown) {
  return !(error instanceof ApiForbiddenError) && failureCount < 1;
}

export function useAdminBranches() {
  return useQuery({
    queryKey: ['admin', 'branches'],
    queryFn: getAdminBranches,
    retry: noRetryOn403,
  });
}

export function useAdminBranch(branchId: string) {
  return useQuery({
    queryKey: ['admin', 'branches', branchId],
    queryFn: () => getAdminBranch(branchId),
    enabled: !!branchId,
    retry: noRetryOn403,
  });
}

export function useCreateBranch() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateBranchRequest) => createBranch(request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'branches'] }),
  });
}

export function useUpdateBranchDetails(branchId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: UpdateBranchDetailsRequest) => updateBranchDetails(branchId, request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['admin', 'branches'] });
    },
  });
}

export function useSetBranchWeeklyHours(branchId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (intervals: OpeningInterval[]) => setBranchWeeklyHours(branchId, intervals),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['admin', 'branches', branchId] });
    },
  });
}

export function useSetBranchActivation(branchId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (isActive: boolean) => setBranchActivation(branchId, isActive),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['admin', 'branches'] });
    },
  });
}

export function useBranchClosures(branchId: string, params: { from?: string; to?: string } = {}) {
  return useQuery({
    queryKey: ['admin', 'branches', branchId, 'closures', params],
    queryFn: () => getBranchClosures(branchId, params),
    enabled: !!branchId,
    retry: noRetryOn403,
  });
}

export function useCreateBranchClosure(branchId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateBranchClosureRequest) => createBranchClosure(branchId, request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['admin', 'branches', branchId, 'closures'] });
      // HO-ADM-002-FU1 §12.1 — a new or cancelled closure changes the calendar too.
      void queryClient.invalidateQueries({ queryKey: ['admin', 'branches', branchId, 'calendar'] });
    },
  });
}

export function useCancelBranchClosure(branchId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (closureId: string) => cancelBranchClosure(branchId, closureId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['admin', 'branches', branchId, 'closures'] });
      // HO-ADM-002-FU1 §12.1 — a new or cancelled closure changes the calendar too.
      void queryClient.invalidateQueries({ queryKey: ['admin', 'branches', branchId, 'calendar'] });
    },
  });
}

export function useBranchRooms(branchId: string) {
  return useQuery({
    queryKey: ['admin', 'branches', branchId, 'rooms'],
    queryFn: () => getBranchRooms(branchId),
    enabled: !!branchId,
    retry: noRetryOn403,
  });
}

export function useCreateRoom(branchId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateRoomRequest) => createRoom(branchId, request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['admin', 'branches', branchId, 'rooms'] });
    },
  });
}

export function useUpdateRoom(branchId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ roomId, request }: { roomId: string; request: UpdateRoomRequest }) =>
      updateRoom(branchId, roomId, request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['admin', 'branches', branchId, 'rooms'] });
    },
  });
}

export function useBranchCalendar(branchId: string, from: string, to: string) {
  return useQuery({
    queryKey: ['admin', 'branches', branchId, 'calendar', from, to],
    queryFn: () => getBranchCalendar(branchId, from, to),
    enabled: !!branchId && !!from && !!to,
    retry: noRetryOn403,
  });
}

export function useStaffBranchAssignment(staffAccountId: string) {
  return useQuery({
    queryKey: ['admin', 'staff-branch-assignment', staffAccountId],
    queryFn: () => getStaffBranchAssignment(staffAccountId),
    enabled: !!staffAccountId,
    retry: noRetryOn403,
  });
}

export function useAssignStaffBranches(staffAccountId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: AssignStaffBranchesRequest) =>
      assignStaffBranches(staffAccountId, request),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: ['admin', 'staff-branch-assignment', staffAccountId],
      });
    },
  });
}
