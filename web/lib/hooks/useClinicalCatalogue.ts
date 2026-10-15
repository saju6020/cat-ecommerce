import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  createDiagnosisCode,
  createLabAnalyte,
  createNoteTemplate,
  getDiagnosisCode,
  getDiagnosisCodes,
  getLabAnalyte,
  getLabAnalytes,
  getNoteTemplate,
  getNoteTemplates,
  setDiagnosisCodeActive,
  setLabAnalyteActive,
  setNoteTemplateActive,
  updateDiagnosisCode,
  updateLabAnalyte,
  updateNoteTemplate,
  type CatalogueQuery,
  type DiagnosisCodeInput,
  type LabAnalyteInput,
  type NoteTemplateInput,
} from '@/lib/api/clinical-catalogue';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';

/**
 * `lib/hooks/useClinicalCatalogue.ts` — HO-EMR-002 §8.3/§11.8. Diagnosis codes, lab analytes and note
 * templates: lists with search/active filter/paging, single reads, and the writes.
 */

function noRetryOnRefusal(failureCount: number, error: unknown) {
  if (error instanceof ApiForbiddenError || error instanceof ApiNotFoundError) return false;
  return failureCount < 1;
}

// ---- diagnosis codes ---------------------------------------------------------------------

export function useDiagnosisCodes(query: CatalogueQuery, enabled = true) {
  return useQuery({
    queryKey: ['clinical-catalogue', 'diagnosis-codes', query],
    queryFn: () => getDiagnosisCodes(query),
    enabled,
    retry: noRetryOnRefusal,
  });
}

export function useDiagnosisCode(id: string) {
  return useQuery({
    queryKey: ['clinical-catalogue', 'diagnosis-code', id],
    queryFn: () => getDiagnosisCode(id),
    enabled: !!id,
    staleTime: 5 * 60_000,
    retry: noRetryOnRefusal,
  });
}

export function useSaveDiagnosisCode(id: string | null) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (input: DiagnosisCodeInput & { code?: string }): Promise<void> =>
      void (id
        ? await updateDiagnosisCode(id, input)
        : await createDiagnosisCode({ ...input, code: input.code ?? '' })),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['clinical-catalogue'] }),
  });
}

export function useSetDiagnosisCodeActive() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) =>
      setDiagnosisCodeActive(id, isActive),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['clinical-catalogue'] }),
  });
}

// ---- lab analytes ------------------------------------------------------------------------

export function useLabAnalytes(query: CatalogueQuery, enabled = true) {
  return useQuery({
    queryKey: ['clinical-catalogue', 'lab-analytes', query],
    queryFn: () => getLabAnalytes(query),
    enabled,
    retry: noRetryOnRefusal,
  });
}

export function useLabAnalyte(id: string) {
  return useQuery({
    queryKey: ['clinical-catalogue', 'lab-analyte', id],
    queryFn: () => getLabAnalyte(id),
    enabled: !!id,
    retry: noRetryOnRefusal,
  });
}

export function useSaveLabAnalyte(id: string | null) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (input: LabAnalyteInput & { code?: string }): Promise<void> =>
      void (id
        ? await updateLabAnalyte(id, input)
        : await createLabAnalyte({ ...input, code: input.code ?? '' })),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['clinical-catalogue'] }),
  });
}

export function useSetLabAnalyteActive() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) =>
      setLabAnalyteActive(id, isActive),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['clinical-catalogue'] }),
  });
}

// ---- note templates ----------------------------------------------------------------------

export function useNoteTemplates(
  query: { serviceId?: string; activeOnly?: boolean; pageNumber?: number; pageSize?: number },
  enabled = true,
) {
  return useQuery({
    queryKey: ['clinical-catalogue', 'note-templates', query],
    queryFn: () => getNoteTemplates(query),
    enabled,
    retry: noRetryOnRefusal,
  });
}

export function useNoteTemplate(id: string) {
  return useQuery({
    queryKey: ['clinical-catalogue', 'note-template', id],
    queryFn: () => getNoteTemplate(id),
    enabled: !!id,
    retry: noRetryOnRefusal,
  });
}

export function useSaveNoteTemplate(id: string | null) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (input: NoteTemplateInput & { serviceId?: string }): Promise<void> =>
      void (id
        ? await updateNoteTemplate(id, input)
        : await createNoteTemplate({ ...input, serviceId: input.serviceId ?? '' })),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['clinical-catalogue'] }),
  });
}

export function useSetNoteTemplateActive() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) =>
      setNoteTemplateActive(id, isActive),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['clinical-catalogue'] }),
  });
}
