import * as React from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getRegister,
  getRegisterBalances,
  getRegisterEntry,
  getRegisterVerification,
  recordCorrection,
  type CorrectionInput,
  type RegisterFilters,
} from '@/lib/api/controlled-drugs';
import { noRetryOnRefusal } from './useInventoryQuery';
import { useBranchScope } from './useBranchScope';

/**
 * `lib/hooks/useControlledDrugs.ts` — HO-PHM-002 §8.3: `['cd', …]` keys, branch-scoped. There is no
 * edit or delete mutation (ADR-FE-065); a correction creates a new entry and invalidates the root.
 */
export function useRegister(filters: RegisterFilters) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['cd', 'register', filters, branchId],
    queryFn: () => getRegister(filters),
    enabled: ready,
    retry: noRetryOnRefusal,
  });
}

export function useRegisterEntry(entryId: string) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['cd', 'entry', entryId, branchId],
    queryFn: () => getRegisterEntry(entryId),
    enabled: ready,
    retry: noRetryOnRefusal,
  });
}

export function useRegisterBalances(enabled = true) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['cd', 'balances', branchId],
    queryFn: getRegisterBalances,
    enabled: ready && enabled,
    retry: noRetryOnRefusal,
  });
}

export function useRegisterVerification(enabled = true) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['cd', 'verification', branchId],
    queryFn: getRegisterVerification,
    enabled: ready && enabled,
    retry: noRetryOnRefusal,
  });
}

/** A plain function, not a mutation: the body carries a witness's password and must not sit in the mutation cache (U-C8). */
export function useRecordCorrection(entryId: string) {
  const queryClient = useQueryClient();
  return React.useCallback(
    async (body: CorrectionInput) => {
      const result = await recordCorrection(entryId, body);
      await queryClient.invalidateQueries({ queryKey: ['cd'] });
      return result;
    },
    [entryId, queryClient],
  );
}
