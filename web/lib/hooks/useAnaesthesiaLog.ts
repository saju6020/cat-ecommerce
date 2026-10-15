import * as React from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  correctEntry,
  getAnaesthesiaLog,
  logDrug,
  logMilestone,
  logVitals,
  type CorrectEntryRequest,
  type LogDrugRequest,
  type LogMilestoneRequest,
  type LogVitalsRequest,
} from '@/lib/api/anaesthesia-log';
import { noRetryOnRefusal } from './useInventoryQuery';
import { useBranchScope } from './useBranchScope';
import { SURGERY_POLL_MS } from './useSurgeries';

/**
 * `lib/hooks/useAnaesthesiaLog.ts` — HO-SRG-002 §8.3. The log polls every 30 s while it is open
 * (UI-5); a poll never touches an open entry dialog (its fields live in the dialog's own state).
 *
 * Drug entries and corrections can carry a WITNESS'S identifier and password (HO-SRG-001 §1 Amended
 * (16)). A TanStack mutation keeps its `variables` in the mutation cache until it is garbage collected,
 * which would retain a password in memory after the request finished — so those two are plain
 * functions: the request is sent, the caches are invalidated, and nothing about the call is stored
 * (ADR-FE-071, U-C4). Vitals and milestones carry no credential and use ordinary mutations.
 */
export function useAnaesthesiaLog(surgeryId: string, poll: boolean) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['surgeries', surgeryId, 'log', branchId],
    queryFn: () => getAnaesthesiaLog(surgeryId),
    enabled: ready,
    refetchInterval: (query) => (poll && query.state.data?.isOpen ? SURGERY_POLL_MS : false),
    retry: noRetryOnRefusal,
  });
}

function useInvalidateLog(surgeryId: string) {
  const queryClient = useQueryClient();
  return React.useCallback(async () => {
    await queryClient.invalidateQueries({ queryKey: ['surgeries', surgeryId, 'log'] });
    // A drug entry moves stock and the controlled register.
    await queryClient.invalidateQueries({ queryKey: ['cd'] });
  }, [queryClient, surgeryId]);
}

/** One call per drug entry; the caller owns the `entryId` (the idempotency key) and reuses it on retry. */
export function useLogDrug(surgeryId: string) {
  const invalidate = useInvalidateLog(surgeryId);
  return React.useCallback(
    async (body: LogDrugRequest) => {
      const result = await logDrug(surgeryId, body);
      await invalidate();
      return result;
    },
    [surgeryId, invalidate],
  );
}

export function useCorrectEntry(surgeryId: string) {
  const invalidate = useInvalidateLog(surgeryId);
  return React.useCallback(
    async (entryId: string, body: CorrectEntryRequest) => {
      const result = await correctEntry(surgeryId, entryId, body);
      await invalidate();
      return result;
    },
    [surgeryId, invalidate],
  );
}

export function useLogVitals(surgeryId: string) {
  const invalidate = useInvalidateLog(surgeryId);
  return useMutation({
    mutationFn: (body: LogVitalsRequest) => logVitals(surgeryId, body),
    onSuccess: invalidate,
  });
}

export function useLogMilestone(surgeryId: string) {
  const invalidate = useInvalidateLog(surgeryId);
  return useMutation({
    mutationFn: (body: LogMilestoneRequest) => logMilestone(surgeryId, body),
    onSuccess: invalidate,
  });
}
