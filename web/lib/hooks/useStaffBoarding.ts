import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';
import {
  actOnDaycareVisit,
  cancelMaintenanceBlock,
  cancelStaffStay,
  changeStaffEnquiry,
  checkInStay,
  checkOutStay,
  clearStayAttention,
  clearSuspectedInfectious,
  confirmStay,
  createBoardingUnit,
  createMaintenanceBlock,
  createStaffEnquiry,
  extendStay,
  flagSuspectedInfectious,
  getBoardingDashboard,
  getBoardingOccupancy,
  getBoardingRateCards,
  getBoardingUnits,
  getCareRoutine,
  getCheckOutPreview,
  getDaycareDayList,
  getDaycareRateCards,
  getMaintenanceBlocks,
  getStaffAvailability,
  getStaffStay,
  getStaffStaySummary,
  getStaffStays,
  getVetHeadroom,
  overrideVaccinationGate,
  recordIsolationEntry,
  recordIsolationExit,
  recordStaffApprovalAnswer,
  recordStayNoShow,
  referToClinic,
  returnFromReferral,
  relocateStay,
  requestVetApproval,
  reserveStaffStay,
  saveStaffCareProfile,
  scheduleBoardingRateCard,
  scheduleDaycareRateCard,
  setBoardingUnitActivation,
  setCareRoutine,
  updateBoardingUnit,
  type CheckInCatInput,
  type CheckOutCatInput,
  type DaycareAction,
  type DaycareRateCardInput,
  type MaintenanceInput,
  type RateCardInput,
  type RelocateInput,
  type StaffAvailabilityParams,
  type StaffStaysParams,
  type UnitInput,
} from '@/lib/api/staff-boarding';
import type {
  BoardingMoney,
  BoardingUnitType,
  CareProfile,
  EnquiryRequest,
  RoutineTaskKind,
  TriagePriority,
} from '@/lib/api/types';
import { useBranchScope } from './useBranchScope';

/**
 * `lib/hooks/useStaffBoarding.ts` — HO-BRD-002 §8.3, keys `['staff', 'boarding', …, activeBranchId]`
 * so a branch switch refetches them. The dashboard and the occupancy board refetch every 30 s
 * (UI-5, ADR-FE-101): a poll only swaps the data — wizards, forms and dialogs keep their own state,
 * so nothing open is reset by one.
 */

export const BOARDING_POLL_MS = 30_000;

function noRetryOnRefusal(failureCount: number, error: unknown) {
  if (error instanceof ApiForbiddenError || error instanceof ApiNotFoundError) return false;
  return failureCount < 1;
}

function useScopedQuery<T>(
  key: unknown[],
  fn: () => Promise<T>,
  options: { enabled?: boolean; poll?: boolean } = {},
) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['staff', 'boarding', ...key, branchId],
    queryFn: fn,
    enabled: ready && (options.enabled ?? true),
    refetchInterval: options.poll ? BOARDING_POLL_MS : false,
    retry: noRetryOnRefusal,
  });
}

// ---- reads ----

export function useBoardingUnits() {
  return useScopedQuery(['units'], getBoardingUnits);
}

export function useMaintenanceBlocks(unitId: string | null) {
  return useScopedQuery(['maintenance', unitId], () => getMaintenanceBlocks(unitId!), {
    enabled: !!unitId,
  });
}

export function useBoardingRateCards(unitType: BoardingUnitType) {
  return useScopedQuery(['rate-cards', unitType], () => getBoardingRateCards(unitType));
}

export function useDaycareRateCards() {
  return useScopedQuery(['daycare-rate-cards'], getDaycareRateCards);
}

export function useCareRoutine() {
  return useScopedQuery(['care-routine'], getCareRoutine);
}

export function useStaffAvailability(params: StaffAvailabilityParams | null) {
  return useScopedQuery(['availability', params], () => getStaffAvailability(params!), {
    enabled: params !== null,
  });
}

export function useBoardingOccupancy(from: string, to: string) {
  return useScopedQuery(['occupancy', from, to], () => getBoardingOccupancy(from, to), {
    poll: true,
  });
}

export function useStaffStays(params: StaffStaysParams) {
  return useScopedQuery(['stays', params], () => getStaffStays(params));
}

export function useStaffStay(stayId: string, options: { poll?: boolean } = {}) {
  return useScopedQuery(['stay', stayId], () => getStaffStay(stayId), {
    enabled: !!stayId,
    poll: options.poll,
  });
}

export function useStaffStaySummary(stayId: string, enabled = true) {
  return useScopedQuery(['summary', stayId], () => getStaffStaySummary(stayId), {
    enabled: !!stayId && enabled,
  });
}

export function useVetHeadroom(stayId: string, catId: string | null) {
  return useScopedQuery(['headroom', stayId, catId], () => getVetHeadroom(stayId, catId!), {
    enabled: !!stayId && !!catId,
  });
}

export function useBoardingDashboard() {
  return useScopedQuery(['dashboard'], getBoardingDashboard, { poll: true });
}

export function useDaycareDayList(date: string) {
  return useScopedQuery(['daycare', date], () => getDaycareDayList(date), { poll: true });
}

// ---- writes ----

function useInvalidateStaffBoarding() {
  const queryClient = useQueryClient();
  return () => {
    void queryClient.invalidateQueries({ queryKey: ['staff', 'boarding'] });
    void queryClient.invalidateQueries({ queryKey: ['boarding', 'availability'] });
  };
}

export function useCreateBoardingUnit() {
  const invalidate = useInvalidateStaffBoarding();
  return useMutation({
    mutationFn: (input: UnitInput) => createBoardingUnit(input),
    onSuccess: invalidate,
  });
}

export function useUpdateBoardingUnit(unitId: string) {
  const invalidate = useInvalidateStaffBoarding();
  return useMutation({
    mutationFn: (input: UnitInput) => updateBoardingUnit(unitId, input),
    onSuccess: invalidate,
  });
}

export function useSetUnitActivation(unitId: string) {
  const invalidate = useInvalidateStaffBoarding();
  return useMutation({
    mutationFn: (isActive: boolean) => setBoardingUnitActivation(unitId, isActive),
    onSuccess: invalidate,
  });
}

export function useCreateMaintenanceBlock(unitId: string) {
  const invalidate = useInvalidateStaffBoarding();
  return useMutation({
    mutationFn: (input: MaintenanceInput) => createMaintenanceBlock(unitId, input),
    onSuccess: invalidate,
  });
}

export function useCancelMaintenanceBlock(unitId: string) {
  const invalidate = useInvalidateStaffBoarding();
  return useMutation({
    mutationFn: (blockId: string) => cancelMaintenanceBlock(unitId, blockId),
    onSuccess: invalidate,
  });
}

export function useScheduleRateCard() {
  const invalidate = useInvalidateStaffBoarding();
  return useMutation({
    mutationFn: (input: RateCardInput) => scheduleBoardingRateCard(input),
    onSuccess: invalidate,
  });
}

export function useScheduleDaycareRateCard() {
  const invalidate = useInvalidateStaffBoarding();
  return useMutation({
    mutationFn: (input: DaycareRateCardInput) => scheduleDaycareRateCard(input),
    onSuccess: invalidate,
  });
}

export function useSetCareRoutine() {
  const invalidate = useInvalidateStaffBoarding();
  return useMutation({
    mutationFn: (times: Array<{ kind: RoutineTaskKind; localTime: string }>) =>
      setCareRoutine(times),
    onSuccess: invalidate,
  });
}

export function useCreateStaffEnquiry() {
  const invalidate = useInvalidateStaffBoarding();
  return useMutation({
    mutationFn: (request: EnquiryRequest) => createStaffEnquiry(request),
    onSuccess: invalidate,
  });
}

export function useChangeStaffEnquiry(stayId: string) {
  const invalidate = useInvalidateStaffBoarding();
  return useMutation({
    mutationFn: (request: Omit<EnquiryRequest, 'customerId'>) =>
      changeStaffEnquiry(stayId, request),
    onSuccess: invalidate,
  });
}

export function useSaveStaffCareProfile(stayId: string) {
  const invalidate = useInvalidateStaffBoarding();
  return useMutation({
    mutationFn: (input: {
      catId: string;
      profile: CareProfile;
      vetAuthorityChangeReason?: string;
    }) => saveStaffCareProfile(stayId, input.catId, input.profile, input.vetAuthorityChangeReason),
    onSuccess: invalidate,
  });
}

export function useOverrideVaccinationGate(stayId: string) {
  const invalidate = useInvalidateStaffBoarding();
  return useMutation({
    mutationFn: (input: { catId: string; reason: string }) =>
      overrideVaccinationGate(stayId, input.catId, input.reason),
    onSuccess: invalidate,
  });
}

export function useReserveStaffStay(stayId: string) {
  const invalidate = useInvalidateStaffBoarding();
  return useMutation({ mutationFn: () => reserveStaffStay(stayId), onSuccess: invalidate });
}

/** ADR-FE-106 — asks Boarding to re-read Billing; it records nothing. */
export function useConfirmStay(stayId: string) {
  const invalidate = useInvalidateStaffBoarding();
  return useMutation({ mutationFn: () => confirmStay(stayId), onSuccess: invalidate });
}

export function useCancelStaffStay(stayId: string) {
  const invalidate = useInvalidateStaffBoarding();
  return useMutation({
    mutationFn: (note: string) => cancelStaffStay(stayId, note),
    onSuccess: invalidate,
  });
}

export function useRecordStayNoShow(stayId: string) {
  const invalidate = useInvalidateStaffBoarding();
  return useMutation({ mutationFn: () => recordStayNoShow(stayId), onSuccess: invalidate });
}

export function useClearStayAttention(stayId: string) {
  const invalidate = useInvalidateStaffBoarding();
  return useMutation({
    mutationFn: (note: string) => clearStayAttention(stayId, note),
    onSuccess: invalidate,
  });
}

export function useCheckInStay(stayId: string) {
  const invalidate = useInvalidateStaffBoarding();
  return useMutation({
    mutationFn: (cats: CheckInCatInput[]) => checkInStay(stayId, cats),
    // the variables hold the owner's signature: do not keep them in the mutation cache
    gcTime: 0,
    onSuccess: invalidate,
  });
}

/** The server's check-out recalculation (HO-BRD-001 1.1): early or late. Read-only, so it is always fetched. */
export function useCheckOutPreview(stayId: string, enabled = true) {
  return useScopedQuery(['check-out-preview', stayId], () => getCheckOutPreview(stayId), {
    enabled: !!stayId && enabled,
  });
}

export function useCheckOutStay(stayId: string) {
  const invalidate = useInvalidateStaffBoarding();
  return useMutation({
    mutationFn: (cats: CheckOutCatInput[]) => checkOutStay(stayId, cats),
    // the variables hold the owner's signature: do not keep them in the mutation cache
    gcTime: 0,
    onSuccess: invalidate,
  });
}

export function useExtendStay(stayId: string) {
  const invalidate = useInvalidateStaffBoarding();
  return useMutation({
    mutationFn: (input: { newDepartureOn: string; expectedVersion: number }) =>
      extendStay(stayId, input.newDepartureOn, input.expectedVersion),
    onSuccess: invalidate,
  });
}

export function useRelocateStay(stayId: string) {
  const invalidate = useInvalidateStaffBoarding();
  return useMutation({
    mutationFn: (input: RelocateInput) => relocateStay(stayId, input),
    onSuccess: invalidate,
  });
}

export function useFlagInfectious(stayId: string) {
  const invalidate = useInvalidateStaffBoarding();
  return useMutation({
    mutationFn: (input: { catId: string; note: string; targetIsolationUnitId?: string }) =>
      flagSuspectedInfectious(stayId, input.catId, input.note, input.targetIsolationUnitId),
    onSuccess: invalidate,
  });
}

export function useClearInfectious(stayId: string) {
  const invalidate = useInvalidateStaffBoarding();
  return useMutation({
    mutationFn: (input: { catId: string; note: string }) =>
      clearSuspectedInfectious(stayId, input.catId, input.note),
    onSuccess: invalidate,
  });
}

export function useRecordIsolationEntry(stayId: string) {
  const invalidate = useInvalidateStaffBoarding();
  return useMutation({
    mutationFn: (purpose: string) => recordIsolationEntry(stayId, purpose),
    onSuccess: invalidate,
  });
}

export function useRecordIsolationExit(stayId: string) {
  const invalidate = useInvalidateStaffBoarding();
  return useMutation({
    mutationFn: (entryId: string) => recordIsolationExit(stayId, entryId),
    onSuccess: invalidate,
  });
}

export function useReferToClinic(stayId: string) {
  const invalidate = useInvalidateStaffBoarding();
  return useMutation({
    mutationFn: (input: { catId: string; reason: string; urgency: TriagePriority }) =>
      referToClinic(stayId, input),
    onSuccess: invalidate,
  });
}

export function useReturnFromReferral(stayId: string) {
  const invalidate = useInvalidateStaffBoarding();
  return useMutation({
    mutationFn: (referralId: string) => returnFromReferral(stayId, referralId),
    onSuccess: invalidate,
  });
}

export function useRequestVetApproval(stayId: string) {
  const invalidate = useInvalidateStaffBoarding();
  return useMutation({
    mutationFn: (input: {
      catId: string;
      referralId?: string;
      amount: BoardingMoney;
      description: string;
    }) => requestVetApproval(stayId, input),
    onSuccess: invalidate,
  });
}

export function useStaffApprovalAnswer(stayId: string) {
  const invalidate = useInvalidateStaffBoarding();
  return useMutation({
    mutationFn: (input: { approvalId: string; answer: 'Approve' | 'Decline'; note: string }) =>
      recordStaffApprovalAnswer(stayId, input.approvalId, input.answer, input.note),
    onSuccess: invalidate,
  });
}

export function useDaycareAction() {
  const invalidate = useInvalidateStaffBoarding();
  return useMutation({
    mutationFn: (input: { visitId: string; action: DaycareAction }) =>
      actOnDaycareVisit(input.visitId, input.action),
    onSuccess: invalidate,
  });
}
