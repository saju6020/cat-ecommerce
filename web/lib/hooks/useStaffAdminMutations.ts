import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  createStaffAccount,
  changeStaffRoles,
  setStaffPassword,
  updateStaffAccountDetails,
  type CreateStaffAccountRequest,
  type UpdateStaffAccountDetailsRequest,
} from '@/lib/api/staff-admin';

/** Staff-administration mutations (US-ACC-07). */

export function useCreateStaffAccount() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateStaffAccountRequest) => createStaffAccount(request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['staff-admin', 'staff-accounts'] }),
  });
}

export function useChangeStaffRoles(staffAccountId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (roleIds: string[]) => changeStaffRoles(staffAccountId, roleIds),
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: ['staff-admin', 'staff-account', staffAccountId],
      });
      await queryClient.invalidateQueries({ queryKey: ['staff-admin', 'staff-accounts'] });
    },
  });
}

/** Owner, 2026-10-19: an administrator sets a staff member's password and shares it with them. */
export function useSetStaffPassword(staffAccountId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (password: string) => setStaffPassword(staffAccountId, password),
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: ['staff-admin', 'staff-account', staffAccountId],
      });
      await queryClient.invalidateQueries({ queryKey: ['staff-admin', 'staff-accounts'] });
    },
  });
}

/** Owner, 2026-10-20: an administrator edits a staff member's name, mobile number and email address. */
export function useUpdateStaffAccountDetails(staffAccountId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: UpdateStaffAccountDetailsRequest) =>
      updateStaffAccountDetails(staffAccountId, request),
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: ['staff-admin', 'staff-account', staffAccountId],
      });
      await queryClient.invalidateQueries({ queryKey: ['staff-admin', 'staff-accounts'] });
    },
  });
}
