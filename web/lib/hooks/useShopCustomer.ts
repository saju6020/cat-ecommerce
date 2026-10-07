import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  addWishlistItem,
  cancelStockAlert,
  editReview,
  getFoodRecommendations,
  getMyReviews,
  getPurchasability,
  getReviewEligibility,
  getStockAlerts,
  getWishlist,
  removeWishlistItem,
  requestStockAlert,
  submitReview,
  withdrawReview,
  type EditReviewRequest,
  type SubmitReviewRequest,
} from '@/lib/api/shop-customer';
import { ApiForbiddenError, ApiNotFoundError, ApiUnauthorizedError } from '@/lib/api/client';

/**
 * HO-SHP-002 §8.4 — the signed-in customer's shop state. Keys follow the handover: personal product
 * state under `['shop', …, slug]`, everything else under `['me', …]`. Sign-in/sign-out clears the
 * cache (HO-ACC-002), so nothing here outlives a person. Every read takes `enabled` so a guest
 * never calls a customer endpoint.
 */
function noRetryOnRefusal(failureCount: number, error: unknown) {
  return (
    !(
      error instanceof ApiNotFoundError ||
      error instanceof ApiForbiddenError ||
      error instanceof ApiUnauthorizedError
    ) && failureCount < 1
  );
}

export function usePurchasability(slug: string, enabled: boolean) {
  return useQuery({
    queryKey: ['shop', 'purchasability', slug],
    queryFn: () => getPurchasability(slug),
    enabled,
    retry: noRetryOnRefusal,
  });
}

export function useReviewEligibility(slug: string, enabled: boolean) {
  return useQuery({
    queryKey: ['shop', 'review-eligibility', slug],
    queryFn: () => getReviewEligibility(slug),
    enabled,
    retry: noRetryOnRefusal,
  });
}

export function useWishlist(enabled = true) {
  return useQuery({
    queryKey: ['me', 'wishlist'],
    queryFn: getWishlist,
    enabled,
    retry: noRetryOnRefusal,
  });
}

export function useSetWishlistItem() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ skuId, saved }: { skuId: string; saved: boolean }) =>
      saved ? addWishlistItem(skuId) : removeWishlistItem(skuId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['me', 'wishlist'] }),
  });
}

export function useStockAlerts(enabled = true) {
  return useQuery({
    queryKey: ['me', 'stock-alerts'],
    queryFn: getStockAlerts,
    enabled,
    retry: noRetryOnRefusal,
  });
}

export function useRequestStockAlert() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (skuId: string) => requestStockAlert(skuId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['me', 'stock-alerts'] }),
  });
}

export function useCancelStockAlert() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (stockAlertId: string) => cancelStockAlert(stockAlertId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['me', 'stock-alerts'] }),
  });
}

export function useMyReviews(enabled = true) {
  return useQuery({
    queryKey: ['me', 'reviews'],
    queryFn: getMyReviews,
    enabled,
    retry: noRetryOnRefusal,
  });
}

function invalidateReviews(queryClient: ReturnType<typeof useQueryClient>) {
  void queryClient.invalidateQueries({ queryKey: ['me', 'reviews'] });
  void queryClient.invalidateQueries({ queryKey: ['shop', 'review-eligibility'] });
}

export function useSubmitReview() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: SubmitReviewRequest) => submitReview(request),
    onSuccess: () => invalidateReviews(queryClient),
  });
}

export function useEditReview(reviewId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: EditReviewRequest) => editReview(reviewId, request),
    onSuccess: () => invalidateReviews(queryClient),
  });
}

export function useWithdrawReview() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (reviewId: string) => withdrawReview(reviewId),
    onSuccess: () => invalidateReviews(queryClient),
  });
}

export function useFoodRecommendations(catId: string, lang: string) {
  return useQuery({
    queryKey: ['me', 'cat', catId, 'food', lang],
    queryFn: () => getFoodRecommendations(catId, lang || undefined),
    enabled: !!catId,
    retry: noRetryOnRefusal,
  });
}
