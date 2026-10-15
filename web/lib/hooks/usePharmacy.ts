import * as React from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  dispensePrescription,
  getDispensePlan,
  getDispensing,
  getPharmacyQueue,
  lookupPrescription,
  recordLabelPrint,
  reverseDispensing,
  type DispenseRequest,
  type WitnessCredentials,
} from '@/lib/api/pharmacy';
import { noRetryOnRefusal } from './useInventoryQuery';
import { useBranchScope } from './useBranchScope';

/**
 * `lib/hooks/usePharmacy.ts` — HO-PHM-002 §8.3. Dispensary queries are keyed by the ACTIVE BRANCH
 * (switching branch refetches) and wait until the branch is known, so no request goes out without the
 * `X-Branch-Id` it should carry. The queue polls every 30 s (ADR-FE-027's rule); the dispense plan is
 * never polled — it is refetched on entry and after a `400`/`409`.
 */
export const QUEUE_POLL_MS = 30_000;

export function usePharmacyQueue() {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['pharmacy', 'queue', branchId],
    queryFn: getPharmacyQueue,
    enabled: ready,
    refetchInterval: QUEUE_POLL_MS,
    retry: noRetryOnRefusal,
  });
}

export function useDispensePlan(prescriptionId: string) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['pharmacy', 'plan', prescriptionId, branchId],
    queryFn: () => getDispensePlan(prescriptionId),
    enabled: ready,
    staleTime: 0,
    retry: noRetryOnRefusal,
  });
}

/** The scan/typed lookup is a command-like read (`Enter` triggers it), so it is a mutation. */
export function useLookupPrescription() {
  return useMutation({ mutationFn: (number: string) => lookupPrescription(number) });
}

/**
 * Dispensing and reversing can carry a WITNESS'S credentials. A TanStack mutation keeps its `variables`
 * in the mutation cache until it is garbage collected, which would retain a password in memory after
 * the request finished — so these two are plain functions: the request is sent, the caches are
 * invalidated, and nothing about the call is stored (ADR-FE-063, U-C8).
 */
export function useDispense(prescriptionId: string) {
  const queryClient = useQueryClient();
  return React.useCallback(
    async (body: DispenseRequest) => {
      const result = await dispensePrescription(prescriptionId, body);
      await queryClient.invalidateQueries({ queryKey: ['pharmacy'] });
      await queryClient.invalidateQueries({ queryKey: ['prescription', prescriptionId] });
      await queryClient.invalidateQueries({ queryKey: ['cat'] });
      return result;
    },
    [prescriptionId, queryClient],
  );
}

/** Refetch the plan after a refusal — the stock moved on (`400`/`409`). */
export function useRefetchPlan(prescriptionId: string) {
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: ['pharmacy', 'plan', prescriptionId] });
}

export function useDispensing(dispensingId: string) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['pharmacy', 'dispensing', dispensingId, branchId],
    queryFn: () => getDispensing(dispensingId),
    enabled: ready,
    retry: noRetryOnRefusal,
  });
}

/** One call per print ACTION (ADR-FE-064); the label itself is the frozen snapshot, never re-rendered into a post. */
export function useRecordLabelPrint(dispensingId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => recordLabelPrint(dispensingId),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ['pharmacy', 'dispensing', dispensingId] }),
  });
}

export function useReverseDispensing(dispensingId: string) {
  const queryClient = useQueryClient();
  return React.useCallback(
    async (body: { reason: string } & Partial<WitnessCredentials>) => {
      await reverseDispensing(dispensingId, body);
      await queryClient.invalidateQueries({ queryKey: ['pharmacy'] });
      await queryClient.invalidateQueries({ queryKey: ['prescription'] });
      await queryClient.invalidateQueries({ queryKey: ['cd'] });
    },
    [dispensingId, queryClient],
  );
}
