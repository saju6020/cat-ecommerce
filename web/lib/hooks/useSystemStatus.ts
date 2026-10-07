import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getSystemStatus,
  setMaintenanceMode,
  type SetMaintenanceModeRequest,
} from '@/lib/api/system';

export function useSystemStatus() {
  return useQuery({ queryKey: ['admin', 'system-status'], queryFn: getSystemStatus });
}

export function useSetMaintenanceMode() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: SetMaintenanceModeRequest) => setMaintenanceMode(request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'system-status'] }),
  });
}
