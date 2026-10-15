import { useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query';
import {
  addAddendum,
  attachPhoto,
  clearFollowUp,
  finaliseVisit,
  getDischargeSummary,
  getOpenVisits,
  getVisit,
  getVisitByAppointment,
  getVitalSignRules,
  openVisit,
  recordVitals,
  saveNote,
  setFollowUp,
  type RecordVitalsRequest,
  type SaveNoteRequest,
  type SetFollowUpRequest,
} from '@/lib/api/visits';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';
import { catQueryKey } from './useCat';
import { useBranchScope } from './useBranchScope';

/**
 * `lib/hooks/useVisit.ts` — HO-EMR-002 §8.3. `['visit', visitId]` is the source of `status`,
 * `version`, `allowedActions`, `draft` and `template`. A query that holds signed photo URLs has a
 * 4-minute `staleTime` (URLs live 5); `refetchOnWindowFocus: 'always'` is OQ-1 — the workspace
 * picks up a nurse's vitals when the vet's tab regains focus. The note FORM never re-initialises
 * from a refetch (it is initialised once per mount), so a refetch cannot cost the vet any text.
 */

export const VISIT_POLL_MS = 30_000;
const SIGNED_URL_STALE_MS = 4 * 60_000;

export function visitQueryKey(visitId: string) {
  return ['visit', visitId] as const;
}

export function clinicalHistoryKey(catId: string) {
  return ['cat', catId, 'clinical-history'] as const;
}

function noRetryOnRefusal(failureCount: number, error: unknown) {
  if (error instanceof ApiForbiddenError || error instanceof ApiNotFoundError) return false;
  return failureCount < 1;
}

export function useVisit(visitId: string) {
  return useQuery({
    queryKey: visitQueryKey(visitId),
    queryFn: () => getVisit(visitId),
    enabled: !!visitId,
    staleTime: SIGNED_URL_STALE_MS,
    refetchOnWindowFocus: 'always',
    retry: noRetryOnRefusal,
  });
}

export function useOpenVisits() {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['visits', 'open', branchId],
    queryFn: () => getOpenVisits('Open'),
    enabled: ready,
    refetchInterval: VISIT_POLL_MS,
    retry: noRetryOnRefusal,
  });
}

export function useVisitForAppointment(appointmentId: string, enabled = true) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['visits', 'by-appointment', branchId, appointmentId],
    queryFn: () => getVisitByAppointment(appointmentId),
    enabled: ready && enabled && !!appointmentId,
    retry: noRetryOnRefusal,
  });
}

export function useVitalSignRules() {
  return useQuery({
    queryKey: ['clinical', 'vital-sign-rules'],
    queryFn: getVitalSignRules,
    staleTime: Infinity,
    retry: noRetryOnRefusal,
  });
}

export function useDischargeSummary(visitId: string) {
  return useQuery({
    queryKey: ['visit', visitId, 'summary'],
    queryFn: () => getDischargeSummary(visitId),
    enabled: !!visitId,
    retry: noRetryOnRefusal,
  });
}

/** Any visit write invalidates the visit and the cat's clinical history (§8.3). */
export function invalidateVisit(queryClient: QueryClient, visitId: string, catId?: string) {
  void queryClient.invalidateQueries({ queryKey: visitQueryKey(visitId) });
  void queryClient.invalidateQueries({ queryKey: ['visits'] });
  if (catId) void queryClient.invalidateQueries({ queryKey: clinicalHistoryKey(catId) });
}

export function useOpenVisit() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (appointmentId: string) => openVisit(appointmentId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['visits'] });
    },
  });
}

/** Vitals with a weight also invalidate the cat and its weights (the header's latest weight). */
export function useRecordVitals(visitId: string, catId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: RecordVitalsRequest) => recordVitals(visitId, request),
    onSuccess: (_result, request) => {
      invalidateVisit(queryClient, visitId, catId);
      if (request.weightKg !== undefined) {
        void queryClient.invalidateQueries({ queryKey: catQueryKey(catId) });
        void queryClient.invalidateQueries({ queryKey: ['cat', catId, 'weights'] });
      }
    },
  });
}

export function useSaveNote(visitId: string, catId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: SaveNoteRequest) => saveNote(visitId, request),
    onSuccess: () => invalidateVisit(queryClient, visitId, catId),
  });
}

export function useFinaliseVisit(visitId: string, catId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (expectedVersion: number) => finaliseVisit(visitId, expectedVersion),
    onSuccess: () => {
      invalidateVisit(queryClient, visitId, catId);
      void queryClient.invalidateQueries({ queryKey: ['staff', 'appointment'] });
    },
  });
}

export function useAddAddendum(visitId: string, catId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: { text: string; reason?: string }) => addAddendum(visitId, request),
    onSuccess: () => invalidateVisit(queryClient, visitId, catId),
  });
}

export function useAttachPhoto(visitId: string, catId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: { fileId: string; takenOn: string; note?: string }) =>
      attachPhoto(visitId, request),
    onSuccess: () => invalidateVisit(queryClient, visitId, catId),
  });
}

export function useSetFollowUp(visitId: string, catId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: SetFollowUpRequest) => setFollowUp(visitId, request),
    onSuccess: () => invalidateVisit(queryClient, visitId, catId),
  });
}

export function useClearFollowUp(visitId: string, catId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => clearFollowUp(visitId),
    onSuccess: () => invalidateVisit(queryClient, visitId, catId),
  });
}
