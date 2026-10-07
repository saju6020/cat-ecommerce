import { useQuery } from '@tanstack/react-query';
import {
  getStaffRoleCatalogue,
  getStaffAccounts,
  getStaffAccount,
  type GetStaffAccountsParams,
} from '@/lib/api/staff-admin';
import { ApiForbiddenError } from '@/lib/api/client';

/** Read-side staff-administration queries (US-ACC-07). `noRetryOn403` — ADR-FE-009: retrying a
 *  permission refusal only delays `PermissionDeniedState`, never changes the outcome. */
function noRetryOn403(failureCount: number, error: unknown) {
  if (error instanceof ApiForbiddenError) return false;
  return failureCount < 1;
}

export function useStaffRoleCatalogue() {
  return useQuery({
    queryKey: ['staff-admin', 'role-catalogue'],
    queryFn: getStaffRoleCatalogue,
    staleTime: 60_000,
    retry: noRetryOn403,
  });
}

export function useStaffAccounts(params: GetStaffAccountsParams) {
  return useQuery({
    queryKey: ['staff-admin', 'staff-accounts', params],
    queryFn: () => getStaffAccounts(params),
    retry: noRetryOn403,
  });
}

export function useStaffAccount(staffAccountId: string) {
  return useQuery({
    queryKey: ['staff-admin', 'staff-account', staffAccountId],
    queryFn: () => getStaffAccount(staffAccountId),
    enabled: !!staffAccountId,
    retry: noRetryOn403,
  });
}
