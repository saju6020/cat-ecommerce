import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  cancelPrescription,
  checkPrescriptionSafety,
  declineRepeatRequest,
  getPrescription,
  getVisitPrescriptions,
  issuePrescription,
  listCatPrescriptions,
  renewPrescription,
  type IssuePrescriptionRequest,
  type PrescriptionScope,
  type RenewPrescriptionRequest,
  type SafetyAssessment,
  type SafetyCheckRequest,
} from '@/lib/api/prescriptions';
import { noRetryOnRefusal } from './useInventoryQuery';
import { useDebouncedValue } from './useDebouncedValue';

/**
 * `lib/hooks/usePrescriptions.ts` — HO-PHM-002 §8.3. Cat prescriptions and the prescription detail
 * nest under HO-CAT-002's `['cat', catId]` / are keyed `['prescription', id]`, so a cat invalidation
 * refreshes them. The safety assessment is a QUERY (it writes nothing server-side).
 */
export const SAFETY_DEBOUNCE_MS = 400;

/** Stable text of what the safety check depends on — a change of any of these is a new check. */
function requestKey(request: SafetyCheckRequest | null): string {
  return request
    ? [
        request.medicineId,
        request.dose.amount,
        request.dose.unit,
        request.route,
        request.frequency,
      ].join('|')
    : '';
}

/**
 * The live safety check (ADR-FE-060). `request` is `null` until the form has a medicine, a dose with
 * its unit, a route and a frequency; it is debounced 400 ms after the last change, so one check runs
 * per pause, not per keystroke. The last result stays in place while the next one loads
 * (`isChecking`), and `setAssessment` lets the issue call replace it with the one the server
 * returned on `400`/`409`.
 */
export function useSafetyCheck(catId: string, request: SafetyCheckRequest | null) {
  const queryClient = useQueryClient();
  const debounced = useDebouncedValue(request, SAFETY_DEBOUNCE_MS);
  const key = requestKey(debounced);
  const queryKey = ['prescriptions', 'safety', catId, key] as const;
  const query = useQuery<SafetyAssessment>({
    queryKey,
    queryFn: () => checkPrescriptionSafety(catId, debounced as SafetyCheckRequest),
    enabled: debounced !== null,
    staleTime: 0,
    gcTime: 0,
    retry: false,
    placeholderData: keepPreviousData,
  });
  const isDebouncing = requestKey(request) !== key;
  return {
    assessment: request === null ? undefined : query.data,
    /** A check is running (or about to) while the last answer stays visible. */
    isChecking: debounced !== null && (query.isFetching || isDebouncing),
    /** The assessment on screen belongs to an earlier form state. */
    isStale: query.isPlaceholderData || isDebouncing,
    failed: query.isError && !query.isPlaceholderData,
    refetch: query.refetch,
    setAssessment: (next: SafetyAssessment) => queryClient.setQueryData(queryKey, next),
  };
}

export function useCatPrescriptions(
  catId: string,
  scope: PrescriptionScope,
  pageNumber: number,
  pageSize = 20,
) {
  return useQuery({
    queryKey: ['cat', catId, 'prescriptions', scope, pageNumber, pageSize],
    queryFn: () => listCatPrescriptions(catId, { scope, pageNumber, pageSize }),
    retry: noRetryOnRefusal,
  });
}

export function usePrescription(prescriptionId: string) {
  return useQuery({
    queryKey: ['prescription', prescriptionId],
    queryFn: () => getPrescription(prescriptionId),
    enabled: prescriptionId !== '',
    retry: noRetryOnRefusal,
  });
}

export function useVisitPrescriptions(visitId: string) {
  return useQuery({
    queryKey: ['visit', visitId, 'prescriptions'],
    queryFn: () => getVisitPrescriptions(visitId),
    retry: noRetryOnRefusal,
  });
}

function useInvalidatePrescriptions(catId?: string) {
  const queryClient = useQueryClient();
  return async (prescriptionId?: string) => {
    if (catId) await queryClient.invalidateQueries({ queryKey: ['cat', catId, 'prescriptions'] });
    else await queryClient.invalidateQueries({ queryKey: ['cat'] });
    if (prescriptionId) {
      await queryClient.invalidateQueries({ queryKey: ['prescription', prescriptionId] });
    }
    await queryClient.invalidateQueries({ queryKey: ['pharmacy'] });
  };
}

export function useIssuePrescription(catId: string) {
  const invalidate = useInvalidatePrescriptions(catId);
  return useMutation({
    mutationFn: (body: IssuePrescriptionRequest) => issuePrescription(catId, body),
    onSuccess: () => invalidate(),
  });
}

export function useRenewPrescription(sourceId: string, catId?: string) {
  const invalidate = useInvalidatePrescriptions(catId);
  return useMutation({
    mutationFn: (body: RenewPrescriptionRequest) => renewPrescription(sourceId, body),
    onSuccess: () => invalidate(sourceId),
  });
}

export function useCancelPrescription(prescriptionId: string, catId?: string) {
  const invalidate = useInvalidatePrescriptions(catId);
  return useMutation({
    mutationFn: (reason: string) => cancelPrescription(prescriptionId, reason),
    onSuccess: () => invalidate(prescriptionId),
  });
}

export function useDeclineRepeat(prescriptionId: string, catId?: string) {
  const invalidate = useInvalidatePrescriptions(catId);
  return useMutation({
    mutationFn: (input: { requestId: string; reason: string }) =>
      declineRepeatRequest(prescriptionId, input.requestId, input.reason),
    onSuccess: () => invalidate(prescriptionId),
  });
}
