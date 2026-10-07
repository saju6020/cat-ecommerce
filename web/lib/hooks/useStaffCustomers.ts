import { useQueries, useQuery } from '@tanstack/react-query';
import {
  getStaffCustomer,
  findCustomers,
  getDuplicateCustomerGroups,
  type FindCustomersParams,
  type GetDuplicateGroupsParams,
} from '@/lib/api/staff-customers';
import { ApiForbiddenError } from '@/lib/api/client';

/** Read-side staff customer queries (US-ACC-10/11). */

function noRetryOn403(failureCount: number, error: unknown) {
  if (error instanceof ApiForbiddenError) return false;
  return failureCount < 1;
}

export function useStaffCustomer(customerId: string) {
  return useQuery({
    queryKey: ['staff-customers', 'customer', customerId],
    queryFn: () => getStaffCustomer(customerId),
    enabled: !!customerId,
    retry: noRetryOn403,
  });
}

/** `ExistingCustomerNotice` — deliberately `enabled: false` by default; the caller triggers this
 *  only after a `MobileNumberAlreadyRegistered` refusal (`refetch()`), never eagerly. */
export function useFindCustomers(params: FindCustomersParams, enabled: boolean) {
  return useQuery({
    queryKey: ['staff-customers', 'find', params],
    queryFn: () => findCustomers(params),
    enabled,
    retry: noRetryOn403,
  });
}

export function useDuplicateCustomerGroups(params: GetDuplicateGroupsParams) {
  return useQuery({
    queryKey: ['staff-customers', 'duplicates', params],
    queryFn: () => getDuplicateCustomerGroups(params),
    retry: noRetryOn403,
  });
}

/**
 * `MergeReview` (ADR-FE-011(c)) — "a fresh read of every record in the group and its cats on
 * arrival ... not the report row". Hitting `GET /api/staff/customers/{id}` per member (the same
 * detail endpoint the customer-detail screen uses) rather than reusing the duplicates report's
 * embedded `DuplicateCustomerMember` rows already satisfies "fresh, not the report row" — this is
 * a different query, `refetchOnMount: 'always'` in case a stale cache entry exists from an earlier
 * visit to that customer's own detail page.
 */
export function useStaffCustomersByIds(customerIds: string[]) {
  return useQueries({
    queries: customerIds.map((customerId) => ({
      queryKey: ['staff-customers', 'customer', customerId],
      queryFn: () => getStaffCustomer(customerId),
      refetchOnMount: 'always' as const,
      retry: noRetryOn403,
    })),
  });
}
