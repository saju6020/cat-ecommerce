import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  authoriseDiet,
  extendDietAuthorisation,
  listCatDietAuthorisations,
  revokeDietAuthorisation,
  searchDietProducts,
  type AuthoriseDietInput,
} from '@/lib/api/diet-authorisations';
import { noRetryOnRefusal } from './useInventoryQuery';

/** `lib/hooks/useDietAuthorisations.ts` — HO-PHM-002 §8.3, US-PHM-08 (staff side). */
export function useDietProducts(search: string) {
  return useQuery({
    queryKey: ['diet', 'products', search],
    queryFn: () => searchDietProducts(search),
    retry: noRetryOnRefusal,
  });
}

export function useCatDietAuthorisations(catId: string, scope: 'active' | 'all') {
  return useQuery({
    queryKey: ['cat', catId, 'diet-authorisations', scope],
    queryFn: () => listCatDietAuthorisations(catId, scope),
    retry: noRetryOnRefusal,
  });
}

function useInvalidateDiet(catId: string) {
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: ['cat', catId, 'diet-authorisations'] });
}

export function useAuthoriseDiet(catId: string) {
  const invalidate = useInvalidateDiet(catId);
  return useMutation({
    mutationFn: (body: AuthoriseDietInput) => authoriseDiet(catId, body),
    onSuccess: invalidate,
  });
}

export function useExtendDiet(catId: string) {
  const invalidate = useInvalidateDiet(catId);
  return useMutation({
    mutationFn: (input: { id: string; validTo: string }) =>
      extendDietAuthorisation(input.id, input.validTo),
    onSuccess: invalidate,
  });
}

export function useRevokeDiet(catId: string) {
  const invalidate = useInvalidateDiet(catId);
  return useMutation({
    mutationFn: (input: { id: string; reason: string }) =>
      revokeDietAuthorisation(input.id, input.reason),
    onSuccess: invalidate,
  });
}
