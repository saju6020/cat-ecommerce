'use client';

import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { StockAlertList, WishlistList } from '@/components/wishlist/WishlistList';
import {
  useCancelStockAlert,
  useSetWishlistItem,
  useStockAlerts,
  useWishlist,
} from '@/lib/hooks/useShopCustomer';

/** `/portal/wishlist` — US-SHP-13: saved items and back-in-stock alerts. */
export default function WishlistPage() {
  const wishlist = useWishlist();
  const alerts = useStockAlerts();
  const setItem = useSetWishlistItem();
  const cancel = useCancelStockAlert();

  return (
    <div className="flex flex-col gap-8">
      <section aria-labelledby="wishlist-heading" className="flex flex-col gap-3">
        <h1 id="wishlist-heading" className="font-display text-2xl font-semibold text-ink">
          Your wishlist
        </h1>
        {wishlist.isPending && <LoadingState rows={3} label="Loading your wishlist…" />}
        {wishlist.isError && <ErrorState onRetry={() => wishlist.refetch()} />}
        {wishlist.data && (
          <WishlistList
            items={wishlist.data.items}
            removingSkuId={setItem.isPending ? setItem.variables?.skuId : undefined}
            onRemove={(skuId) => setItem.mutate({ skuId, saved: false })}
          />
        )}
      </section>
      <section aria-labelledby="alerts-heading" className="flex flex-col gap-3">
        <h2 id="alerts-heading" className="font-display text-xl font-semibold text-ink">
          Back-in-stock alerts
        </h2>
        {alerts.isPending && <LoadingState rows={2} label="Loading your alerts…" />}
        {alerts.isError && <ErrorState onRetry={() => alerts.refetch()} />}
        {alerts.data && (
          <StockAlertList alerts={alerts.data} onCancel={(id) => cancel.mutate(id)} />
        )}
      </section>
    </div>
  );
}
