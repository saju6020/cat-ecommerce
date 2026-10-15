'use client';

import * as React from 'react';
import { usePathname, useRouter, useSearchParams } from 'next/navigation';
import { BuyBox } from '@/components/product/BuyBox';
import { ProductGallery } from '@/components/product/ProductGallery';
import { VariantSelector } from '@/components/product/VariantSelector';
import { AddToCartButton } from '@/components/cart/AddToCartButton';
import { PrescriptionGate } from '@/components/prescription/PrescriptionGate';
import { StockAlertButton } from '@/components/wishlist/StockAlertButton';
import { WishlistButton } from '@/components/wishlist/WishlistButton';
import { ApiUnauthorizedError, ApiValidationError } from '@/lib/api/client';
import { describeApiError, SHOP_CODE_MESSAGES } from '@/lib/api/errors';
import type { ProductDetail } from '@/lib/api/shop-catalogue';
import type { StockAlertDeliverability } from '@/lib/api/shop-customer';
import { useCustomerSession } from '@/lib/hooks/useSession';
import {
  useCancelStockAlert,
  usePurchasability,
  useRequestStockAlert,
  useSetWishlistItem,
  useStockAlerts,
  useWishlist,
} from '@/lib/hooks/useShopCustomer';
import { defaultSku, productHref } from '@/lib/shop/helpers';

export interface ProductPurchaseProps {
  detail: ProductDetail;
  initialSkuId: string;
  lang: string;
  /** The server-rendered heading block, laid out beside the gallery. */
  heading: React.ReactNode;
  /** An override for the buy box action slot; by default HO-ORD-002's *Add to cart* (§3.3). */
  actions?: React.ReactNode;
}

function messageFor(error: unknown): string {
  if (error instanceof ApiUnauthorizedError)
    return 'Your session has ended. Sign in again to continue.';
  if (error instanceof ApiValidationError) return describeApiError(error, SHOP_CODE_MESSAGES);
  return 'Something went wrong. Please try again.';
}

/**
 * HO-SHP-002 §11.3 — the product page's interactive island: gallery, variant selector and buy box
 * for the SELECTED variant (`?sku=`, ADR in §8.4). Price, availability and the best-before notice
 * come straight from the SKU the API sent; the personal parts (prescription gate, wishlist, stock
 * alert) load after the server-rendered page and never block it (§8.2).
 */
export function ProductPurchase({
  detail,
  initialSkuId,
  lang,
  heading,
  actions,
}: ProductPurchaseProps) {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const [skuId, setSkuId] = React.useState(initialSkuId);
  const urlSku = searchParams.get('sku');
  // The URL is the source of truth, including when `?sku=` goes away (Back to the first variant's
  // URL): then the default variant applies again (S8). On first render an absent `?sku=` keeps
  // `initialSkuId`, which the server already resolved from the same URL.
  const previousUrlSku = React.useRef(urlSku);
  React.useEffect(() => {
    const hadSku = previousUrlSku.current !== null;
    previousUrlSku.current = urlSku;
    if (urlSku && detail.skus.some((s) => s.skuId === urlSku)) setSkuId(urlSku);
    else if (hadSku) setSkuId(defaultSku(detail.skus)?.skuId ?? initialSkuId);
  }, [urlSku, detail.skus, initialSkuId]);

  const sku = defaultSku(detail.skus, skuId);
  const session = useCustomerSession();
  const signedIn = !!session.data;
  const sessionKnown = !session.isPending;
  const purchasability = usePurchasability(detail.slug, signedIn && detail.prescriptionRequired);
  const wishlist = useWishlist(signedIn);
  const alerts = useStockAlerts(signedIn);
  const setWishlist = useSetWishlistItem();
  const requestAlert = useRequestStockAlert();
  const cancelAlert = useCancelStockAlert();
  // An error belongs to the variant it happened on: drop it when the shopper moves to another (S15).
  const { reset: resetWishlist } = setWishlist;
  const { reset: resetRequestAlert } = requestAlert;
  const { reset: resetCancelAlert } = cancelAlert;
  const currentSkuId = defaultSku(detail.skus, skuId)?.skuId;
  React.useEffect(() => {
    resetWishlist();
    resetRequestAlert();
    resetCancelAlert();
  }, [currentSkuId, resetWishlist, resetRequestAlert, resetCancelAlert]);
  const [deliverability, setDeliverability] = React.useState<
    Record<string, StockAlertDeliverability>
  >({});

  if (!sku) return <div>{heading}</div>;

  function select(nextSkuId: string) {
    setSkuId(nextSkuId);
    const next = new URLSearchParams(searchParams.toString());
    next.set('sku', nextSkuId);
    router.replace(`${pathname}?${next.toString()}`, { scroll: false });
  }

  const signInHref = `/sign-in?next=${encodeURIComponent(productHref(detail.slug, { sku: sku.skuId, lang: lang || undefined }))}`;
  const saved = !!wishlist.data?.items.some((item) => item.skuId === sku.skuId);
  const pendingAlert = alerts.data?.find((a) => a.skuId === sku.skuId && a.status === 'Pending');
  const outOfStock = sku.availability === 'OutOfStock';
  const guest = sessionKnown && !signedIn;

  return (
    <div className="grid gap-6 md:grid-cols-2">
      <ProductGallery
        key={sku.skuId}
        images={[...sku.images, ...detail.images]}
        productName={detail.name}
      />
      <div className="flex min-w-0 flex-col gap-4">
        {heading}
        <VariantSelector skus={detail.skus} selectedSkuId={sku.skuId} onSelect={select} />
        <BuyBox
          sku={sku}
          pricesIncludeTax={detail.pricesIncludeTax}
          actions={
            actions ?? (
              <AddToCartButton
                key={sku.skuId}
                skuId={sku.skuId}
                productName={detail.name}
                requiresPrescription={detail.prescriptionRequired}
                disabled={outOfStock || !sku.price}
                disabledReason={outOfStock ? 'This variant is out of stock.' : 'This variant has no price yet.'}
                signedIn={sessionKnown ? signedIn : undefined}
                eligibleCatIds={
                  purchasability.data?.skus
                    .find((s) => s.skuId === sku.skuId)
                    ?.eligibleCats.map((cat) => cat.catId) ?? []
                }
                signInHref={signInHref}
              />
            )
          }
          gate={
            detail.prescriptionRequired && sessionKnown ? (
              <PrescriptionGate
                audience={guest ? 'guest' : 'customer'}
                isLoading={signedIn && purchasability.isPending}
                isError={signedIn && purchasability.isError}
                onRetry={() => void purchasability.refetch()}
                purchasability={purchasability.data?.skus.find((s) => s.skuId === sku.skuId)}
                signInHref={signInHref}
              />
            ) : null
          }
          wishlist={
            sessionKnown ? (
              <WishlistButton
                saved={saved}
                pending={setWishlist.isPending}
                signInHref={guest ? signInHref : undefined}
                error={setWishlist.isError ? messageFor(setWishlist.error) : undefined}
                onToggle={() => setWishlist.mutate({ skuId: sku.skuId, saved: !saved })}
              />
            ) : null
          }
          stockAlert={
            outOfStock && sessionKnown ? (
              <StockAlertButton
                requested={!!pendingAlert}
                deliverability={deliverability[sku.skuId]}
                pending={requestAlert.isPending || cancelAlert.isPending}
                signInHref={guest ? signInHref : undefined}
                error={
                  requestAlert.isError
                    ? messageFor(requestAlert.error)
                    : cancelAlert.isError
                      ? messageFor(cancelAlert.error)
                      : undefined
                }
                onRequest={() =>
                  requestAlert.mutate(sku.skuId, {
                    onSuccess: (result) =>
                      setDeliverability((current) => ({
                        ...current,
                        [sku.skuId]: result.deliverability,
                      })),
                  })
                }
                onCancel={() => pendingAlert && cancelAlert.mutate(pendingAlert.stockAlertId)}
              />
            ) : null
          }
        />
      </div>
    </div>
  );
}
