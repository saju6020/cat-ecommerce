import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  registerWalkInCustomer,
  mergeCustomer,
  type RegisterWalkInCustomerRequest,
  type MergeCustomerRequest,
} from '@/lib/api/staff-customers';

/** US-ACC-10/11 mutations. */

export function useRegisterWalkInCustomer() {
  return useMutation({
    mutationFn: (request: RegisterWalkInCustomerRequest) => registerWalkInCustomer(request),
  });
}

/**
 * ADR-FE-011 — one request to the merge endpoint, never client-orchestrated cat moves. Takes
 * `duplicateCustomerId` as part of the mutate call (not fixed at hook-creation time) so
 * `MergeReview` can re-run it identically — same id, same body — from its "Run again" recovery
 * path (§11.13) without re-instantiating the hook.
 *
 * `removeQueries`, not `invalidateQueries`, for the two customers' own detail query
 * (`['staff-customers','customer', id]`) — the same reasoning as this codebase's session queries
 * (`lib/hooks/useSession.ts`): `MergeReview` itself is still the mounted observer at merge time
 * (the detail screen for either customer isn't open yet), so `invalidateQueries`'s default
 * `refetchType: 'active'` would leave the pre-merge `catCount` sitting in the cache — fresh enough
 * (`staleTime: 30_000`, `app/providers.tsx`) that the detail screen would show it unrefetched the
 * moment "View the surviving record" is clicked, silently contradicting ADR-FE-011(c)'s own "fresh
 * read" promise for the one screen a real user checks right after a merge. The duplicates LIST
 * query, by contrast, keeps `invalidateQueries` — that list is what's on screen right now if the
 * merge fails partway and the user returns to it, so an active refetch is exactly what's wanted.
 */
export function useMergeCustomer() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      duplicateCustomerId,
      request,
    }: {
      duplicateCustomerId: string;
      request: MergeCustomerRequest;
    }) => mergeCustomer(duplicateCustomerId, request),
    onSuccess: (response, variables) => {
      queryClient.invalidateQueries({ queryKey: ['staff-customers', 'duplicates'] });
      queryClient.removeQueries({
        queryKey: ['staff-customers', 'customer', variables.duplicateCustomerId],
      });
      queryClient.removeQueries({
        queryKey: ['staff-customers', 'customer', response.survivingCustomerId],
      });
    },
  });
}
