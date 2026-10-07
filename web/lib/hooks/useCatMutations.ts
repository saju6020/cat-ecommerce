import { useMutation, useQueryClient, type QueryClient } from '@tanstack/react-query';
import {
  registerCat,
  updateCatProfile,
  recordCatNeuterStatus,
  updateCatMedicalProfile,
  addCatPhoto,
  flagCatHighRisk,
  clearCatHighRiskFlag,
  recordCatWeight,
  recordCatVaccination,
  recordCatParasiteTreatment,
  markCatDeceased,
  transferCatOwnership,
  type RegisterCatRequest,
  type UpdateCatProfileRequest,
  type RecordCatNeuterStatusRequest,
  type UpdateCatMedicalProfileRequest,
  type AddCatPhotoRequest,
  type FlagCatHighRiskRequest,
  type ClearCatHighRiskFlagRequest,
  type RecordCatWeightRequest,
  type RecordCatVaccinationRequest,
  type RecordCatParasiteTreatmentRequest,
  type MarkCatDeceasedRequest,
  type TransferCatOwnershipRequest,
} from '@/lib/api/cats';

/**
 * Every Cat Profiles mutation (HO-CAT-002 §8.2 rule 3 — only lib/hooks/ calls useMutation).
 *
 * Invalidation follows §8.4: a mutation invalidates `['cat', catId]` and, because TanStack
 * Query's default `invalidateQueries` matching is a prefix match (`exact: false`), that one call
 * also invalidates every narrower key beneath it (`['cat', catId, 'weights']`,
 * `['cat', catId, 'vaccination-status']`, ...) without naming each one. Mutations that change
 * something a list row shows (name, high-risk flag, life status, owner) also invalidate the
 * `['cats', ...]` family so a list open in another tab/screen refetches instead of showing stale
 * data.
 */
function invalidateCat(queryClient: QueryClient, catId: string) {
  return queryClient.invalidateQueries({ queryKey: ['cat', catId] });
}

function invalidateCatsList(queryClient: QueryClient) {
  return queryClient.invalidateQueries({ queryKey: ['cats'] });
}

export function useRegisterCat() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: RegisterCatRequest) => registerCat(request),
    onSuccess: () => invalidateCatsList(queryClient),
  });
}

export function useUpdateCatProfile(catId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: UpdateCatProfileRequest) => updateCatProfile(catId, request),
    onSuccess: async () => {
      await invalidateCat(queryClient, catId);
      await invalidateCatsList(queryClient);
    },
  });
}

export function useRecordCatNeuterStatus(catId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: RecordCatNeuterStatusRequest) => recordCatNeuterStatus(catId, request),
    onSuccess: () => invalidateCat(queryClient, catId),
  });
}

export function useUpdateCatMedicalProfile(catId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: UpdateCatMedicalProfileRequest) =>
      updateCatMedicalProfile(catId, request),
    onSuccess: () => invalidateCat(queryClient, catId),
  });
}

export function useAddCatPhoto(catId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: AddCatPhotoRequest) => addCatPhoto(catId, request),
    onSuccess: () => invalidateCat(queryClient, catId),
  });
}

export function useFlagCatHighRisk(catId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: FlagCatHighRiskRequest) => flagCatHighRisk(catId, request),
    onSuccess: async () => {
      await invalidateCat(queryClient, catId);
      await invalidateCatsList(queryClient);
    },
  });
}

export function useClearCatHighRiskFlag(catId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: ClearCatHighRiskFlagRequest) => clearCatHighRiskFlag(catId, request),
    onSuccess: async () => {
      await invalidateCat(queryClient, catId);
      await invalidateCatsList(queryClient);
    },
  });
}

export function useRecordCatWeight(catId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: RecordCatWeightRequest) => recordCatWeight(catId, request),
    // The cat's latestWeightKg (header, UI-7) is derived from this series server-side, so the
    // profile itself must be invalidated too, not only the weights keys.
    onSuccess: () => invalidateCat(queryClient, catId),
  });
}

export function useRecordCatVaccination(catId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: RecordCatVaccinationRequest) => recordCatVaccination(catId, request),
    // §8.4: recording a vaccination MUST also invalidate the vaccination-status key — covered by
    // invalidateCat's prefix match on ['cat', catId], but named here so the requirement is
    // impossible to miss on a future refactor of this function.
    onSuccess: async () => {
      await invalidateCat(queryClient, catId);
      await queryClient.invalidateQueries({ queryKey: ['cat', catId, 'vaccination-status'] });
    },
  });
}

export function useRecordCatParasiteTreatment(catId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: RecordCatParasiteTreatmentRequest) =>
      recordCatParasiteTreatment(catId, request),
    onSuccess: () => invalidateCat(queryClient, catId),
  });
}

export function useMarkCatDeceased(catId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: MarkCatDeceasedRequest) => markCatDeceased(catId, request),
    onSuccess: async () => {
      await invalidateCat(queryClient, catId);
      await invalidateCatsList(queryClient);
    },
  });
}

export function useTransferCatOwnership(catId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: TransferCatOwnershipRequest) => transferCatOwnership(catId, request),
    onSuccess: async () => {
      await invalidateCat(queryClient, catId);
      await invalidateCatsList(queryClient);
    },
  });
}
