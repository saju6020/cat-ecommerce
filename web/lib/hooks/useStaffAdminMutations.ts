import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  createStaffAccount,
  changeStaffRoles,
  type CreateStaffAccountRequest,
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
