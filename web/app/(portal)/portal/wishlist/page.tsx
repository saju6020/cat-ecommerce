'use client';

import Link from 'next/link';
import { useState } from 'react';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { StockAlertList, WishlistList } from '@/components/wishlist/WishlistList';
import { describeOrderingError } from '@/lib/api/errors';
import { useAddCartLine } from '@/lib/hooks/useCart';
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
  const addToCart = useAddCartLine();
  const [addedName, setAddedName] = useState('');

  return (
    <div className="flex flex-col gap-8">
      <section aria-labelledby="wishlist-heading" className="flex flex-col gap-3">
        <h1
          id="wishlist-heading"
          className="m-0 font-display text-[clamp(1.75rem,3.5vw,2.5rem)] font-semibold text-ink"
        >
          Your wishlist
        </h1>
        {wishlist.isPending && <LoadingState rows={3} label="Loading your wishlist…" />}
        {wishlist.isError && <ErrorState onRetry={() => wishlist.refetch()} />}
        {wishlist.data && (
          <WishlistList
            items={wishlist.data.items}
            removingSkuId={setItem.isPending ? setItem.variables?.skuId : undefined}
            onRemove={(skuId) => setItem.mutate({ skuId, saved: false })}
            addingSkuId={addToCart.isPending ? addToCart.variables?.skuId : undefined}
            onAdd={(item) => {
              setAddedName('');
              addToCart.mutate(
                { skuId: item.skuId, quantity: 1 },
                { onSuccess: () => setAddedName(item.productName) },
              );
            }}
          />
        )}
        <p role="status" className="m-0 text-sm font-semibold text-accent-ink empty:hidden">
          {addedName && (
            <>
              Added {addedName} to your cart.{' '}
              <Link href="/cart" className="underline">
                View cart
              </Link>
            </>
          )}
        </p>
        {addToCart.isError && (
          <p role="alert" className="m-0 text-sm font-semibold text-danger-ink">
            {describeOrderingError(addToCart.error)} Open the product to choose options.
          </p>
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
