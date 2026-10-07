import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getAdminContentList,
  getAdminContentItem,
  createContentItem,
  editContentDraft,
  publishContentItem,
  unpublishContentItem,
  getAdminPolicies,
  createPolicyDocument,
  publishPolicyVersion,
  type GetAdminContentParams,
  type CreateContentItemRequest,
  type EditContentDraftRequest,
  type CreatePolicyDocumentRequest,
  type PublishPolicyVersionRequest,
} from '@/lib/api/admin-content';
import { ApiForbiddenError } from '@/lib/api/client';

function noRetryOn403(failureCount: number, error: unknown) {
  return !(error instanceof ApiForbiddenError) && failureCount < 1;
}

export function useAdminContentList(params: GetAdminContentParams) {
  return useQuery({
    queryKey: ['admin', 'content', params],
    queryFn: () => getAdminContentList(params),
    retry: noRetryOn403,
  });
}

export function useAdminContentItem(contentId: string) {
  return useQuery({
    queryKey: ['admin', 'content', 'item', contentId],
    queryFn: () => getAdminContentItem(contentId),
    enabled: !!contentId,
    retry: noRetryOn403,
  });
}

function invalidateContent(queryClient: ReturnType<typeof useQueryClient>) {
  void queryClient.invalidateQueries({ queryKey: ['admin', 'content'] });
}

export function useCreateContentItem() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateContentItemRequest) => createContentItem(request),
    onSuccess: () => invalidateContent(queryClient),
  });
}

export function useEditContentDraft(contentId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: EditContentDraftRequest) => editContentDraft(contentId, request),
    onSuccess: () => invalidateContent(queryClient),
  });
}

export function usePublishContentItem(contentId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => publishContentItem(contentId),
    onSuccess: () => invalidateContent(queryClient),
  });
}

export function useUnpublishContentItem(contentId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => unpublishContentItem(contentId),
    onSuccess: () => invalidateContent(queryClient),
  });
}

export function useAdminPolicies() {
  return useQuery({
    queryKey: ['admin', 'policies'],
    queryFn: getAdminPolicies,
    retry: noRetryOn403,
  });
}

export function useCreatePolicyDocument() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreatePolicyDocumentRequest) => createPolicyDocument(request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'policies'] }),
  });
}

export function usePublishPolicyVersion(key: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: PublishPolicyVersionRequest) => publishPolicyVersion(key, request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'policies'] }),
  });
}
