import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getPublishedContent,
  getPublishedBanners,
  getPublishedFaqs,
  getPublicPolicy,
  getPendingPolicyAcceptances,
  acceptPolicyVersion,
} from '@/lib/api/public-content';
import type { ContentKind, BannerPlacement } from '@/lib/api/admin-content';
import { ApiNotFoundError } from '@/lib/api/client';

function noRetryOn404(failureCount: number, error: unknown) {
  return !(error instanceof ApiNotFoundError) && failureCount < 1;
}

export function usePublishedContent(kind: ContentKind, slug: string) {
  return useQuery({
    queryKey: ['public-content', kind, slug],
    queryFn: () => getPublishedContent(kind, slug),
    enabled: !!slug,
    retry: noRetryOn404,
  });
}

export function usePublishedBanners(placement?: BannerPlacement) {
  return useQuery({
    queryKey: ['public-content', 'banners', placement ?? 'all'],
    queryFn: () => getPublishedBanners(placement),
  });
}

export function usePublishedFaqs() {
  return useQuery({ queryKey: ['public-content', 'faqs'], queryFn: getPublishedFaqs });
}

export function usePublicPolicy(key: string) {
  return useQuery({
    queryKey: ['public-content', 'policy', key],
    queryFn: () => getPublicPolicy(key),
    enabled: !!key,
    retry: noRetryOn404,
  });
}

export function usePendingPolicyAcceptances() {
  return useQuery({
    queryKey: ['portal', 'policy-acceptances', 'pending'],
    queryFn: getPendingPolicyAcceptances,
  });
}

export function useAcceptPolicyVersion() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ policyKey, versionNumber }: { policyKey: string; versionNumber: number }) =>
      acceptPolicyVersion(policyKey, versionNumber),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ['portal', 'policy-acceptances', 'pending'] }),
  });
}
