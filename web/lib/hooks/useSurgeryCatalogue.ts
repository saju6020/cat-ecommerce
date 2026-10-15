import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  listSurgeryProtocols,
  upsertSurgeryProtocol,
  type UpsertProtocolRequest,
} from '@/lib/api/surgery-protocols';
import {
  createSurgeryCampaign,
  getApplicableCampaigns,
  getCampaignCases,
  listSurgeryCampaigns,
  setSurgeryCampaignActive,
  updateSurgeryCampaign,
  type CampaignInput,
} from '@/lib/api/surgery-campaigns';
import { getMySurgery, listMySurgeries } from '@/lib/api/my-surgeries';
import { noRetryOnRefusal } from './useInventoryQuery';
import { useBranchScope } from './useBranchScope';

/**
 * `lib/hooks/useSurgeryCatalogue.ts` — HO-SRG-002 §8.3. The configuration side of Surgery (protocols and
 * campaigns) and the owner's own pages. Staff reads carry the active branch in their key; the owner's
 * pages are the customer's own and carry none.
 */
export function useSurgeryProtocols() {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['surgeries', 'protocols', branchId],
    queryFn: listSurgeryProtocols,
    enabled: ready,
    retry: noRetryOnRefusal,
  });
}

export function useUpsertProtocol(serviceId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: UpsertProtocolRequest) => upsertSurgeryProtocol(serviceId, body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['surgeries', 'protocols'] }),
  });
}

export function useSurgeryCampaigns(pageNumber: number, pageSize = 20) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['surgeries', 'campaigns', 'list', pageNumber, pageSize, branchId],
    queryFn: () => listSurgeryCampaigns({ pageNumber, pageSize }),
    enabled: ready,
    retry: noRetryOnRefusal,
  });
}

export function useApplicableCampaigns(serviceId: string, date: string) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['surgeries', 'campaigns', 'applicable', serviceId, date, branchId],
    queryFn: () => getApplicableCampaigns({ serviceId, date }),
    enabled: ready && serviceId !== '' && date !== '',
    retry: noRetryOnRefusal,
  });
}

export function useCampaignCases(
  campaignId: string,
  params: { from?: string; to?: string; pageNumber: number },
) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['surgeries', 'campaigns', campaignId, 'cases', params, branchId],
    queryFn: () => getCampaignCases(campaignId, { ...params, pageSize: 25 }),
    enabled: ready && campaignId !== '',
    retry: noRetryOnRefusal,
  });
}

function useInvalidateCampaigns() {
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: ['surgeries', 'campaigns'] });
}

export function useCreateCampaign() {
  const invalidate = useInvalidateCampaigns();
  return useMutation({
    mutationFn: (body: CampaignInput & { code: string }) => createSurgeryCampaign(body),
    onSuccess: invalidate,
  });
}

export function useUpdateCampaign(campaignId: string) {
  const invalidate = useInvalidateCampaigns();
  return useMutation({
    mutationFn: (body: CampaignInput & { expectedVersion: number }) =>
      updateSurgeryCampaign(campaignId, body),
    onSuccess: invalidate,
  });
}

export function useSetCampaignActive(campaignId: string) {
  const invalidate = useInvalidateCampaigns();
  return useMutation({
    mutationFn: (isActive: boolean) => setSurgeryCampaignActive(campaignId, isActive),
    onSuccess: invalidate,
  });
}

export function useMySurgeries(scope: 'upcoming' | 'past', pageNumber = 1) {
  return useQuery({
    queryKey: ['my-surgeries', 'list', scope, pageNumber],
    queryFn: () => listMySurgeries({ scope, pageNumber, pageSize: 20 }),
    retry: noRetryOnRefusal,
  });
}

export function useMySurgery(surgeryId: string) {
  return useQuery({
    queryKey: ['my-surgeries', surgeryId],
    queryFn: () => getMySurgery(surgeryId),
    retry: noRetryOnRefusal,
  });
}
