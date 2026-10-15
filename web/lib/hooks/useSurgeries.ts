import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  admitSurgery,
  applySterilisationOutcome,
  bookSurgery,
  cancelFollowUp,
  cancelSurgery,
  completeChecklistItem,
  completeFollowUp,
  dischargeSurgery,
  endProcedure,
  getDischargeDraft,
  getRecoveryBoard,
  getSurgeryAvailability,
  getSurgery,
  getSurgeryDay,
  getSurgeryServices,
  getSurgeryStaff,
  recordAsaGrade,
  rescheduleSurgery,
  setRecoveryStatus,
  startSurgery,
  startSurgeryWithConsentOverride,
  waiveChecklistItem,
  type AvailabilityParams,
  type AsaGrade,
  type ChecklistKind,
  type DischargeRequest,
  type ProcedureOutcome,
  type RecoveryStatus,
  type RescheduleSurgeryRequest,
  type SurgeryStatus,
} from '@/lib/api/surgeries';
import { noRetryOnRefusal } from './useInventoryQuery';
import { useBranchScope } from './useBranchScope';

/**
 * `lib/hooks/useSurgeries.ts` — HO-SRG-002 §8.3. Surgery queries live under `['surgeries', …]` and every
 * key carries the ACTIVE BRANCH (switching branch refetches; no request goes out before the branch is
 * known). The day list, the recovery board and an open surgery (while `PreOp` / `InTheatre` /
 * `Recovery`) refetch every 30 s (UI-5, ADR-FE-027): a poll updates the cache, never a form's own
 * state, so an open dialog or unsaved text is untouched. Availability is refetched on any choice change
 * and after a `400`/`409` (ADR-FE-069).
 */
export const SURGERY_POLL_MS = 30_000;

const POLLED_STATUSES: SurgeryStatus[] = ['PreOp', 'InTheatre', 'Recovery'];

export function useSurgeryServices() {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['surgeries', 'services', branchId],
    queryFn: getSurgeryServices,
    enabled: ready,
    retry: noRetryOnRefusal,
  });
}

export function useSurgeryStaff(serviceId: string) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['surgeries', 'staff', serviceId, branchId],
    queryFn: () => getSurgeryStaff(serviceId),
    enabled: ready && serviceId !== '',
    retry: noRetryOnRefusal,
  });
}

export function useSurgeryAvailability(params: AvailabilityParams | null) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['surgeries', 'availability', params, branchId],
    queryFn: () => getSurgeryAvailability(params as AvailabilityParams),
    enabled: ready && params !== null,
    staleTime: 0,
    retry: noRetryOnRefusal,
  });
}

export function useSurgeryDay(date: string) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['surgeries', 'day', branchId, date],
    queryFn: () => getSurgeryDay(date),
    enabled: ready && date !== '',
    refetchInterval: SURGERY_POLL_MS,
    retry: noRetryOnRefusal,
  });
}

export function useRecoveryBoard() {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['surgeries', 'recovery', branchId],
    queryFn: getRecoveryBoard,
    enabled: ready,
    refetchInterval: SURGERY_POLL_MS,
    retry: noRetryOnRefusal,
  });
}

export function useSurgery(surgeryId: string) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['surgeries', surgeryId, branchId],
    queryFn: () => getSurgery(surgeryId),
    enabled: ready,
    refetchInterval: (query) =>
      query.state.data && POLLED_STATUSES.includes(query.state.data.status)
        ? SURGERY_POLL_MS
        : false,
    retry: noRetryOnRefusal,
  });
}

export function useDischargeDraft(surgeryId: string, enabled: boolean) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['surgeries', surgeryId, 'discharge-draft', branchId],
    queryFn: () => getDischargeDraft(surgeryId),
    enabled: ready && enabled,
    staleTime: Infinity,
    refetchOnWindowFocus: false,
    retry: noRetryOnRefusal,
  });
}

/** Everything one surgery shows is under `['surgeries', id]`, so one invalidation refreshes the page. */
function useInvalidateSurgery(surgeryId: string) {
  const queryClient = useQueryClient();
  return async () => {
    await queryClient.invalidateQueries({ queryKey: ['surgeries', surgeryId] });
    await queryClient.invalidateQueries({ queryKey: ['surgeries', 'day'] });
    await queryClient.invalidateQueries({ queryKey: ['surgeries', 'recovery'] });
    // The cat header (neuter status) and the calendar move with a surgery.
    await queryClient.invalidateQueries({ queryKey: ['cat'] });
  };
}

export function useBookSurgery() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: bookSurgery,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['surgeries', 'day'] }),
  });
}

export function useRescheduleSurgery(surgeryId: string) {
  const invalidate = useInvalidateSurgery(surgeryId);
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: RescheduleSurgeryRequest) => rescheduleSurgery(surgeryId, body),
    onSuccess: async () => {
      await invalidate();
      await queryClient.invalidateQueries({ queryKey: ['staff', 'calendar'] });
    },
  });
}

export function useCancelSurgery(surgeryId: string) {
  const invalidate = useInvalidateSurgery(surgeryId);
  return useMutation({
    mutationFn: (reason: string) => cancelSurgery(surgeryId, reason),
    onSuccess: invalidate,
  });
}

export function useCompleteChecklistItem(surgeryId: string) {
  const invalidate = useInvalidateSurgery(surgeryId);
  return useMutation({
    mutationFn: (args: { kind: ChecklistKind; note?: string }) =>
      completeChecklistItem(surgeryId, args.kind, { note: args.note }),
    onSuccess: invalidate,
  });
}

export function useWaiveChecklistItem(surgeryId: string) {
  const invalidate = useInvalidateSurgery(surgeryId);
  return useMutation({
    mutationFn: (args: { kind: ChecklistKind; reason: string }) =>
      waiveChecklistItem(surgeryId, args.kind, args.reason),
    onSuccess: invalidate,
  });
}

export function useRecordAsaGrade(surgeryId: string) {
  const invalidate = useInvalidateSurgery(surgeryId);
  return useMutation({
    mutationFn: (body: { grade: AsaGrade; isEmergency: boolean }) =>
      recordAsaGrade(surgeryId, body),
    onSuccess: invalidate,
  });
}

export function useAdmitSurgery(surgeryId: string) {
  const invalidate = useInvalidateSurgery(surgeryId);
  return useMutation({ mutationFn: () => admitSurgery(surgeryId), onSuccess: invalidate });
}

export function useStartSurgery(surgeryId: string) {
  const invalidate = useInvalidateSurgery(surgeryId);
  return useMutation({ mutationFn: () => startSurgery(surgeryId), onSuccess: invalidate });
}

export function useStartWithConsentOverride(surgeryId: string) {
  const invalidate = useInvalidateSurgery(surgeryId);
  return useMutation({
    mutationFn: (reason: string) => startSurgeryWithConsentOverride(surgeryId, reason),
    onSuccess: invalidate,
  });
}

export function useEndProcedure(surgeryId: string) {
  const invalidate = useInvalidateSurgery(surgeryId);
  return useMutation({
    mutationFn: (outcome: ProcedureOutcome) => endProcedure(surgeryId, outcome),
    onSuccess: invalidate,
  });
}

export function useApplySterilisationOutcome(surgeryId: string) {
  const invalidate = useInvalidateSurgery(surgeryId);
  return useMutation({
    mutationFn: () => applySterilisationOutcome(surgeryId),
    onSuccess: invalidate,
  });
}

export function useSetRecoveryStatus(surgeryId: string) {
  const invalidate = useInvalidateSurgery(surgeryId);
  return useMutation({
    mutationFn: (body: { status: RecoveryStatus; note?: string }) =>
      setRecoveryStatus(surgeryId, body),
    onSuccess: invalidate,
  });
}

export function useDischargeSurgery(surgeryId: string) {
  const invalidate = useInvalidateSurgery(surgeryId);
  return useMutation({
    mutationFn: (body: DischargeRequest) => dischargeSurgery(surgeryId, body),
    onSuccess: invalidate,
  });
}

export function useCompleteFollowUp(surgeryId: string) {
  const invalidate = useInvalidateSurgery(surgeryId);
  return useMutation({
    mutationFn: (followUpId: string) => completeFollowUp(surgeryId, followUpId),
    onSuccess: invalidate,
  });
}

export function useCancelFollowUp(surgeryId: string) {
  const invalidate = useInvalidateSurgery(surgeryId);
  return useMutation({
    mutationFn: (args: { followUpId: string; reason: string }) =>
      cancelFollowUp(surgeryId, args.followUpId, args.reason),
    onSuccess: invalidate,
  });
}
