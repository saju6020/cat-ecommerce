import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  createFormularyMedicine,
  getFormularyMedicine,
  listFormulary,
  searchFormularySkus,
  setMedicineActive,
  setMedicineControlled,
  updateFormularyMedicine,
  type FormularyListParams,
  type FormularyMedicineInput,
} from '@/lib/api/formulary';
import { noRetryOnRefusal } from './useInventoryQuery';

/**
 * `lib/hooks/useFormulary.ts` — HO-PHM-002 §8.3. Formulary queries live under `['formulary', …]`; a
 * mutation invalidates the whole root so the list, the detail and the composer's picker agree.
 */
export function useFormularyList(params: FormularyListParams, enabled = true) {
  return useQuery({
    queryKey: ['formulary', 'list', params],
    queryFn: () => listFormulary(params),
    enabled,
    retry: noRetryOnRefusal,
  });
}

export function useFormularyMedicine(medicineId: string | null) {
  return useQuery({
    queryKey: ['formulary', 'detail', medicineId],
    queryFn: () => getFormularyMedicine(medicineId as string),
    enabled: medicineId !== null,
    retry: noRetryOnRefusal,
  });
}

export function useFormularySkuSearch(term: string) {
  return useQuery({
    queryKey: ['formulary', 'sku-search', term],
    queryFn: () => searchFormularySkus(term),
    enabled: term.trim().length >= 2,
    retry: noRetryOnRefusal,
  });
}

function useInvalidateFormulary() {
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: ['formulary'] });
}

export function useCreateMedicine() {
  const invalidate = useInvalidateFormulary();
  return useMutation({
    mutationFn: (body: FormularyMedicineInput) => createFormularyMedicine(body),
    onSuccess: invalidate,
  });
}

export function useUpdateMedicine(medicineId: string) {
  const invalidate = useInvalidateFormulary();
  return useMutation({
    mutationFn: (body: FormularyMedicineInput) => updateFormularyMedicine(medicineId, body),
    onSuccess: invalidate,
  });
}

export function useSetMedicineControlled(medicineId: string) {
  const invalidate = useInvalidateFormulary();
  return useMutation({
    mutationFn: (body: {
      isControlled: boolean;
      controlledSchedule?: string | null;
      reason?: string | null;
    }) => setMedicineControlled(medicineId, body),
    onSuccess: invalidate,
  });
}

export function useSetMedicineActive(medicineId: string) {
  const invalidate = useInvalidateFormulary();
  return useMutation({
    mutationFn: (isActive: boolean) => setMedicineActive(medicineId, isActive),
    onSuccess: invalidate,
  });
}
