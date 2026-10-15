import { useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query';
import {
  ApiConflictError,
  ApiForbiddenError,
  ApiNotFoundError,
  ApiValidationError,
} from '@/lib/api/client';
import { useBranchScope } from './useBranchScope';

/**
 * `lib/hooks/useInventoryQuery.ts` — HO-IMP-002 §8.3. One place that knows the Inventory/Procurement
 * query-key convention: `['inventory', family, ...parts, branchId]`, so
 *  - every key includes the ACTIVE BRANCH (switching branches refetches — nothing stale is shown);
 *  - a mutation invalidates by `['inventory', family]` prefix;
 *  - no request is sent until the active branch is known (`ready`), so none goes out without the
 *    `X-Branch-Id` it should carry.
 * Procurement shares the `inventory` root: both sections are the same bounded context.
 */
export function noRetryOnRefusal(failureCount: number, error: unknown): boolean {
  return (
    !(
      error instanceof ApiForbiddenError ||
      error instanceof ApiNotFoundError ||
      error instanceof ApiValidationError ||
      error instanceof ApiConflictError
    ) && failureCount < 1
  );
}

export interface InventoryQueryOptions {
  enabled?: boolean;
  /** ADR-FE-045: only the alert badge polls. */
  refetchInterval?: number;
  staleTime?: number;
}

export function useInventoryQuery<T>(
  family: string,
  parts: ReadonlyArray<unknown>,
  queryFn: () => Promise<T>,
  options: InventoryQueryOptions = {},
) {
  const { branchId, ready } = useBranchScope();
  return useQuery<T>({
    queryKey: ['inventory', family, ...parts, branchId],
    queryFn,
    enabled: ready && (options.enabled ?? true),
    retry: noRetryOnRefusal,
    refetchInterval: options.refetchInterval,
    staleTime: options.staleTime,
  });
}

/** Invalidates `['inventory', family]` for each family, plus the alert badge (§8.3). */
export function invalidateInventory(queryClient: QueryClient, ...families: string[]): void {
  for (const family of new Set([...families, 'alerts'])) {
    void queryClient.invalidateQueries({ queryKey: ['inventory', family] });
  }
}

export function useInvalidateInventory(): (...families: string[]) => void {
  const queryClient = useQueryClient();
  return (...families: string[]) => invalidateInventory(queryClient, ...families);
}
