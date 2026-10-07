import { useMutation, useQueryClient } from '@tanstack/react-query';
import { registerVaccine, type RegisterVaccineRequest } from '@/lib/api/vaccines';

export function useRegisterVaccine() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: RegisterVaccineRequest) => registerVaccine(request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['vaccines'] }),
  });
}
