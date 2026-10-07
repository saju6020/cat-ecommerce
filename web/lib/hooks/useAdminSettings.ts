import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getSettings,
  changeSettings,
  clearSettingOverride,
  type SettingChange,
} from '@/lib/api/admin-settings';
import { ApiForbiddenError } from '@/lib/api/client';

function noRetryOn403(failureCount: number, error: unknown) {
  return !(error instanceof ApiForbiddenError) && failureCount < 1;
}

export function useSettings(branchId?: string) {
  return useQuery({
    queryKey: ['admin', 'settings', branchId ?? 'global'],
    queryFn: () => getSettings(branchId),
    retry: noRetryOn403,
  });
}

export function useChangeSettings(branchId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (changes: SettingChange[]) => changeSettings(changes, branchId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'settings'] }),
  });
}

export function useClearSettingOverride(branchId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (key: string) => clearSettingOverride(key, branchId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'settings'] }),
  });
}
