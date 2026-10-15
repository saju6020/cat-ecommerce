import { useMutation, useQueryClient } from '@tanstack/react-query';
import { reorderOrder, type ReorderResult } from '@/lib/api/my-orders';
import { CART_QUERY_KEY } from '@/lib/hooks/useCart';
import { useIdempotencyKey } from '@/lib/hooks/useIdempotencyKey';

/**
 * `lib/hooks/useReorder.ts` — HO-POR-002 §1 Amended (2): the portal has no re-order of its own;
 * this calls Orders' `POST /api/customers/me/orders/{orderId}/reorder` (ADR-FE-123) through
 * HO-ORD-002's `reorderOrder` (`lib/api/my-orders.ts`). The server decides what can be added
 * (stock, prescription, basket limits) — this hook checks nothing (ADR-FE-123).
 *
 * The `Idempotency-Key` is HO-ORD-002's `useIdempotencyKey` (ADR-FE-097): one UUID per button, kept
 * across a retry of the same click (a network error, a double click) so the server replays the first
 * answer instead of adding twice, renewed after a success, and forgotten by `settleFailure` after a
 * definitive refusal (`shouldRenewIdempotencyKey`; POR backlog 31 deduped at merge).
 *
 * U-B5: success invalidates the basket (`['cart']`, HO-ORD-002 ADR-FE-089) and NOT the dashboard —
 * the basket is not on the dashboard, and refetching it would jolt the tiles under the dialog.
 */
export function useReorder(orderId: string) {
  const queryClient = useQueryClient();
  const idempotencyKey = useIdempotencyKey();

  return useMutation<ReorderResult, Error>({
    mutationFn: () => reorderOrder(orderId, idempotencyKey.keyFor(orderId)),
    onSuccess: () => {
      idempotencyKey.renew();
      void queryClient.invalidateQueries({ queryKey: CART_QUERY_KEY });
    },
    onError: (error) => idempotencyKey.settleFailure(error),
  });
}
