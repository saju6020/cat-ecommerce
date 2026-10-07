import { useQuery } from '@tanstack/react-query';
import { getCat } from '@/lib/api/cats';
import { ApiNotFoundError } from '@/lib/api/client';

/** Query key convention — HO-CAT-002 §8.4: `['cat', catId]`. */
export function catQueryKey(catId: string) {
  return ['cat', catId] as const;
}

/**
 * `enabled` — HO-CAT-002 §1 Amended (2026-09-27, owner decision on `/cats/{id}`'s ownership
 * check). `GET /api/cats/{catId}` is the same endpoint for every caller; the server applies its
 * own caller-scoped filter (HO-CAT-001 §20.3 — "a query filter, not authorization"), so a signed-
 * in customer who isn't this cat's owner gets the same `404` as a cat that doesn't exist at all.
 * `/cats/{id}`'s smart redirect reuses this exact hook as its ownership check for a signed-in
 * customer, gated by `enabled` so the request is never issued for staff or an anonymous visitor —
 * no new endpoint, no second cat-reading hook. Defaults to `true`, so every existing call site is
 * unaffected.
 */
export function useCat(catId: string, options?: { enabled?: boolean; staleTime?: number }) {
  return useQuery({
    queryKey: catQueryKey(catId),
    queryFn: () => getCat(catId),
    enabled: options?.enabled ?? true,
    ...(options?.staleTime !== undefined && { staleTime: options.staleTime }),
    retry: (failureCount, error) => {
      // A 404 renders "not found" (HO-CAT-002 §11.2) — retrying it just delays that page.
      if (error instanceof ApiNotFoundError) return false;
      return failureCount < 1;
    },
  });
}
