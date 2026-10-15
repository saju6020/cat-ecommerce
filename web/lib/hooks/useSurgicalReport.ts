import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  addReportAddendum,
  finaliseReport,
  getSurgicalReport,
  saveReportDraft,
  type SaveReportDraftRequest,
} from '@/lib/api/surgical-reports';
import { ApiNotFoundError } from '@/lib/api/client';
import { noRetryOnRefusal } from './useInventoryQuery';
import { useBranchScope } from './useBranchScope';

/**
 * `lib/hooks/useSurgicalReport.ts` — HO-SRG-002 §8.3, ADR-FE-074. `GET` answers `404` until the first
 * draft is saved, which is "no report yet" and not an error: the query resolves to `null`. The report is
 * NOT polled — the editor owns the text while it is open (a refetch must never reset unsaved typing), and
 * a second author is found out by the `409` on the next autosave.
 */
export function useSurgicalReport(surgeryId: string) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['surgeries', surgeryId, 'report', branchId],
    queryFn: async () => {
      try {
        return await getSurgicalReport(surgeryId);
      } catch (error) {
        if (error instanceof ApiNotFoundError) return null;
        throw error;
      }
    },
    enabled: ready,
    staleTime: Infinity,
    refetchOnWindowFocus: false,
    retry: noRetryOnRefusal,
  });
}

/** Autosave is a plain function: it must not invalidate (and so refetch) the report mid-typing. */
export function saveReportDraftRequest(surgeryId: string, body: SaveReportDraftRequest) {
  return saveReportDraft(surgeryId, body);
}

export function useFinaliseReport(surgeryId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (expectedVersion: number) => finaliseReport(surgeryId, expectedVersion),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['surgeries', surgeryId] });
    },
  });
}

export function useAddAddendum(surgeryId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (text: string) => addReportAddendum(surgeryId, text),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['surgeries', surgeryId, 'report'] });
    },
  });
}
