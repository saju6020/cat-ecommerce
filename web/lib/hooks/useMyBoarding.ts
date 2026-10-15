import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';
import { getBoardingAvailability, getBoardingUnitTypes } from '@/lib/api/boarding-public';
import {
  acknowledgeIncident,
  bookDaycare,
  cancelDaycare,
  cancelMyStay,
  changeEnquiry,
  createEnquiry,
  getMyApproval,
  getMyBoardingWaitlist,
  getMyDaycareVisits,
  getMyIncident,
  getMyStay,
  getMyStayDiary,
  getMyStays,
  getMyStaySummary,
  joinBoardingWaitlist,
  reserveStay,
  respondToApproval,
  saveCareProfile,
  withdrawFromBoardingWaitlist,
  type ApprovalAnswer,
  type BookDaycareRequest,
  type JoinWaitlistRequest,
  type MyStayScope,
} from '@/lib/api/my-boarding';
import type { CareProfile, EnquiryRequest, StaySearchParams } from '@/lib/api/types';

/**
 * `lib/hooks/useMyBoarding.ts` — HO-BRD-002 §8.3, keys `['me', 'boarding', …]` (and
 * `['boarding', 'availability' | 'unit-types']` for the public reads). Someone else's stay is a
 * `404`, which must not be retried; a `403` is the permission state, not a retry (ADR-FE-107).
 */

function noRetryOnRefusal(failureCount: number, error: unknown) {
  if (error instanceof ApiNotFoundError || error instanceof ApiForbiddenError) return false;
  return failureCount < 1;
}

export function useBoardingAvailability(params: StaySearchParams | null) {
  return useQuery({
    queryKey: ['boarding', 'availability', params],
    queryFn: () => getBoardingAvailability(params!),
    enabled: params !== null,
    staleTime: 0,
    refetchOnMount: 'always',
    retry: noRetryOnRefusal,
  });
}

export function useBoardingUnitTypes() {
  return useQuery({
    queryKey: ['boarding', 'unit-types'],
    queryFn: getBoardingUnitTypes,
    staleTime: 60_000,
  });
}

export function useMyStays(scope: MyStayScope) {
  return useQuery({
    queryKey: ['me', 'boarding', 'stays', scope],
    queryFn: () => getMyStays(scope),
    retry: noRetryOnRefusal,
  });
}

export function useMyStay(stayId: string | null) {
  return useQuery({
    queryKey: ['me', 'boarding', 'stay', stayId],
    queryFn: () => getMyStay(stayId!),
    enabled: !!stayId,
    retry: noRetryOnRefusal,
  });
}

export function useMyStayDiary(stayId: string) {
  return useQuery({
    queryKey: ['me', 'boarding', 'diary', stayId],
    queryFn: () => getMyStayDiary(stayId),
    enabled: !!stayId,
    retry: noRetryOnRefusal,
  });
}

export function useMyStaySummary(stayId: string, enabled = true) {
  return useQuery({
    queryKey: ['me', 'boarding', 'summary', stayId],
    queryFn: () => getMyStaySummary(stayId),
    enabled: !!stayId && enabled,
    retry: noRetryOnRefusal,
  });
}

export function useMyIncident(stayId: string, incidentId: string) {
  return useQuery({
    queryKey: ['me', 'boarding', 'incident', stayId, incidentId],
    queryFn: () => getMyIncident(stayId, incidentId),
    enabled: !!stayId && !!incidentId,
    retry: noRetryOnRefusal,
  });
}

export function useMyApproval(stayId: string, approvalId: string) {
  return useQuery({
    queryKey: ['me', 'boarding', 'approval', stayId, approvalId],
    queryFn: () => getMyApproval(stayId, approvalId),
    enabled: !!stayId && !!approvalId,
    retry: noRetryOnRefusal,
  });
}

export function useMyBoardingWaitlist() {
  return useQuery({ queryKey: ['me', 'boarding', 'waitlist'], queryFn: getMyBoardingWaitlist });
}

export function useMyDaycareVisits() {
  return useQuery({ queryKey: ['me', 'boarding', 'daycare'], queryFn: getMyDaycareVisits });
}

function useInvalidateMine() {
  const queryClient = useQueryClient();
  return () => {
    void queryClient.invalidateQueries({ queryKey: ['me', 'boarding'] });
  };
}

export function useCreateEnquiry() {
  const invalidate = useInvalidateMine();
  return useMutation({
    mutationFn: (request: EnquiryRequest) => createEnquiry(request),
    onSuccess: invalidate,
  });
}

export function useChangeEnquiry(stayId: string) {
  const invalidate = useInvalidateMine();
  return useMutation({
    mutationFn: (request: Omit<EnquiryRequest, 'customerId'>) => changeEnquiry(stayId, request),
    onSuccess: invalidate,
  });
}

export function useSaveCareProfile(stayId: string) {
  const invalidate = useInvalidateMine();
  return useMutation({
    mutationFn: (input: { catId: string; profile: CareProfile }) =>
      saveCareProfile(stayId, input.catId, input.profile),
    onSuccess: invalidate,
  });
}

export function useReserveStay(stayId: string) {
  const invalidate = useInvalidateMine();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => reserveStay(stayId),
    onSuccess: (result) => {
      queryClient.setQueryData(['me', 'boarding', 'stay', stayId], result.stay);
      invalidate();
      void queryClient.invalidateQueries({ queryKey: ['boarding', 'availability'] });
    },
  });
}

export function useCancelMyStay(stayId: string) {
  const invalidate = useInvalidateMine();
  return useMutation({
    mutationFn: (note: string | null) => cancelMyStay(stayId, note),
    onSuccess: invalidate,
  });
}

export function useAcknowledgeIncident(stayId: string, incidentId: string) {
  const invalidate = useInvalidateMine();
  return useMutation({
    mutationFn: () => acknowledgeIncident(stayId, incidentId),
    onSuccess: invalidate,
  });
}

export function useRespondToApproval(stayId: string, approvalId: string) {
  const invalidate = useInvalidateMine();
  return useMutation({
    mutationFn: (input: { answer: ApprovalAnswer; note: string | null }) =>
      respondToApproval(stayId, approvalId, input.answer, input.note),
    onSuccess: invalidate,
  });
}

export function useJoinBoardingWaitlist() {
  const invalidate = useInvalidateMine();
  return useMutation({
    mutationFn: (request: JoinWaitlistRequest) => joinBoardingWaitlist(request),
    onSuccess: invalidate,
  });
}

export function useWithdrawFromBoardingWaitlist() {
  const invalidate = useInvalidateMine();
  return useMutation({
    mutationFn: (entryId: string) => withdrawFromBoardingWaitlist(entryId),
    onSuccess: invalidate,
  });
}

export function useBookDaycare() {
  const invalidate = useInvalidateMine();
  return useMutation({
    mutationFn: (request: BookDaycareRequest) => bookDaycare(request),
    onSuccess: invalidate,
  });
}

export function useCancelDaycare() {
  const invalidate = useInvalidateMine();
  return useMutation({
    mutationFn: (visitId: string) => cancelDaycare(visitId),
    onSuccess: invalidate,
  });
}
