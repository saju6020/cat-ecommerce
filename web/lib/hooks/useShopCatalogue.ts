import { useQuery } from '@tanstack/react-query';
import {
  getCategories,
  getProductDetail,
  getProductReviews,
  getRecommendations,
  getSystemStatus,
  type StorefrontStatus,
} from '@/lib/api/shop-catalogue';
import { ApiNotFoundError } from '@/lib/api/client';

function noRetryOn404(failureCount: number, error: unknown) {
  return !(error instanceof ApiNotFoundError) && failureCount < 1;
}

/**
 * HO-SHP-002 §11.1, US-ADM-08 — the server renders the maintenance state (`MaintenanceGate`); this
 * client watch re-checks it so a page cached for up to 60 s still gives way to the notice quickly.
 * `initialDataUpdatedAt: 0` makes the server's value stale at once, so it is confirmed on mount.
 */
export function useShopMaintenance(initial: StorefrontStatus) {
  return useQuery({
    queryKey: ['shop', 'maintenance'],
    queryFn: getSystemStatus,
    initialData: initial,
    initialDataUpdatedAt: 0,
    staleTime: 60_000,
    // The first request after load can race the mock worker's start (and a real blip): retry.
    retry: 3,
    retryDelay: 300,
  });
}

/** Paged published reviews (client side, so a moderator's publish shows up without a rebuild). */
export function useProductReviews(slug: string, pageNumber: number) {
  return useQuery({
    queryKey: ['shop', 'reviews', slug, pageNumber],
    queryFn: () => getProductReviews(slug, pageNumber),
    retry: noRetryOn404,
  });
}

/** Recommendations are fetched client-side so the exclusions reflect the session (US-SHP-08). */
export function useRecommendations(slug: string, lang: string) {
  return useQuery({
    queryKey: ['shop', 'recommendations', slug, lang],
    queryFn: () => getRecommendations(slug, lang || undefined),
    retry: noRetryOn404,
  });
}

export function useProductDetail(slug: string) {
  return useQuery({
    queryKey: ['shop', 'product', slug],
    queryFn: () => getProductDetail(slug),
    enabled: !!slug,
    retry: noRetryOn404,
  });
}

export function useShopCategories() {
  return useQuery({ queryKey: ['shop', 'categories'], queryFn: getCategories, staleTime: 300_000 });
}
