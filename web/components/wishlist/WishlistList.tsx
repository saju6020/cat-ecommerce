import Link from 'next/link';
import { Button, buttonVariants } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Heart } from 'lucide-react';
import { ProductPackArt } from '@/components/hearth/ProductPackArt';
import { EmptyCatIllustration, EmptyState } from '@/components/common/EmptyState';
import { AvailabilityText } from '@/components/product/StatusText';
import { formatMoney } from '@/lib/format/money';
import { resolveFileUrl } from '@/lib/api/files';
import { formatShortInstant } from '@/lib/format/shop';
import { productHref } from '@/lib/shop/helpers';
import type { StockAlert, WishlistItem } from '@/lib/api/shop-customer';

/** HO-SHP-002 §11.5 `WishlistList` — Hearth cards: a tinted image, the filled heart (it removes the
 *  item), name, variant, price, availability in TEXT and the action: *Add* for something in stock,
 *  *Alert me* for something out of stock, *Remove* for something *No longer sold*. The name links to
 *  the product with `?sku=`. */
export function WishlistList({
  items,
  onRemove,
  removingSkuId,
  onAdd,
  addingSkuId,
}: {
  items: WishlistItem[];
  onRemove: (skuId: string) => void;
  removingSkuId?: string;
  /** Adds one of the item to the cart (the page wires the existing cart mutation). */
  onAdd?: (item: WishlistItem) => void;
  addingSkuId?: string;
}) {
  if (items.length === 0) {
    return (
      <EmptyState
        icon={<EmptyCatIllustration />}
        title="Nothing saved yet"
        description="Tap Save on a product to keep it here."
        action={
          <Link
            href="/shop/products"
            className={buttonVariants({ variant: 'pillSecondary', size: 'pillMd' })}
          >
            Browse the shop
          </Link>
        }
      />
    );
  }
  return (
    <ul className="m-0 grid list-none grid-cols-1 gap-5 p-0 sm:grid-cols-2 lg:grid-cols-3">
      {items.map((item) => {
        const href = productHref(item.slug, { sku: item.skuId });
        const inStock = item.availability === 'InStock';
        return (
          <li key={item.skuId} className="min-w-0">
            <article className="relative flex h-full flex-col overflow-hidden rounded-card border border-border bg-surface">
              <div className="relative grid h-[180px] place-items-center bg-accent-tint">
                {resolveFileUrl(item.primaryImage?.url) ? (
                  // eslint-disable-next-line @next/next/no-img-element -- ADR-FE-035
                  <img
                    src={resolveFileUrl(item.primaryImage?.url) ?? undefined}
                    alt={item.primaryImage?.altText || `${item.productName} — photo coming soon`}
                    width={240}
                    height={180}
                    loading="lazy"
                    className="size-full object-cover"
                  />
                ) : (
                  <span
                    role="img"
                    aria-label={`${item.productName} — photo coming soon`}
                    className="grid size-full place-items-center"
                  >
                    <ProductPackArt
                      tone="teal"
                      className="size-[120px] rounded-none bg-transparent"
                    />
                  </span>
                )}
                {item.onSale && (
                  <button
                    type="button"
                    aria-pressed="true"
                    aria-label={`Remove ${item.productName} (${item.skuLabel}) from wishlist`}
                    disabled={removingSkuId === item.skuId}
                    onClick={() => onRemove(item.skuId)}
                    className="absolute right-2.5 top-2.5 grid size-11 place-items-center rounded-pill border-0 bg-surface p-0 text-warm-ink hover:bg-warm-soft disabled:opacity-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
                  >
                    <Heart size={18} fill="currentColor" aria-hidden="true" />
                  </button>
                )}
              </div>
              <div className="flex flex-1 flex-col gap-2 px-[18px] pb-5 pt-[18px]">
                <h3 className="m-0 text-base font-semibold leading-snug text-ink">
                  <Link
                    href={href}
                    className="text-ink no-underline hover:text-accent hover:underline"
                  >
                    {item.productName}
                  </Link>
                </h3>
                <p className="m-0 text-[13px] text-ink-muted">{item.skuLabel}</p>
                {item.onSale ? (
                  <AvailabilityText availability={item.availability} />
                ) : (
                  <Badge variant="neutral" className="self-start">
                    No longer sold
                  </Badge>
                )}
                <div className="mt-auto flex flex-wrap items-center justify-between gap-2 pt-1.5">
                  <p className="m-0 text-xl font-bold text-ink">
                    {item.price ? formatMoney(item.price) : ''}
                  </p>
                  {!item.onSale ? (
                    <Button
                      variant="pillSecondary"
                      size="pillMd"
                      disabled={removingSkuId === item.skuId}
                      aria-label={`Remove ${item.productName} (${item.skuLabel}) from wishlist`}
                      onClick={() => onRemove(item.skuId)}
                    >
                      Remove
                    </Button>
                  ) : inStock && onAdd ? (
                    <Button
                      variant="pill"
                      size="pillMd"
                      className="px-4"
                      disabled={addingSkuId === item.skuId}
                      aria-label={`Add ${item.productName} (${item.skuLabel}) to cart`}
                      onClick={() => onAdd(item)}
                    >
                      Add
                    </Button>
                  ) : !inStock ? (
                    <Link
                      href={`${href}#stock-alert`}
                      aria-label={`Tell me when ${item.productName} (${item.skuLabel}) is back`}
                      className={buttonVariants({
                        variant: 'pillSecondary',
                        size: 'pillMd',
                        className: 'px-4',
                      })}
                    >
                      Alert me
                    </Link>
                  ) : null}
                </div>
              </div>
            </article>
          </li>
        );
      })}
    </ul>
  );
}

function statusText(alert: StockAlert): string {
  if (alert.status === 'Pending') return 'Waiting';
  if (alert.status === 'Notified') {
    return `Notified on ${alert.notifiedOn ? formatShortInstant(alert.notifiedOn) : 'a recent date'}`;
  }
  return 'Closed — no longer sold';
}

/** HO-SHP-002 §11.5 `StockAlertList` — status in text; **Cancel** for the waiting ones. */
export function StockAlertList({
  alerts,
  onCancel,
}: {
  alerts: StockAlert[];
  onCancel: (stockAlertId: string) => void;
}) {
  if (alerts.length === 0)
    return <p className="text-sm text-ink-muted">You have no back-in-stock alerts.</p>;
  return (
    <ul className="m-0 flex list-none flex-col gap-2 p-0">
      {alerts.map((alert) => (
        <li
          key={alert.stockAlertId}
          className="flex flex-wrap items-center justify-between gap-2 rounded-card border border-border bg-surface px-4 py-3 text-sm"
        >
          <span>
            <span className="font-semibold text-ink">{alert.productName ?? 'Product'}</span>{' '}
            <span className="text-ink-muted">({alert.skuLabel ?? 'size'})</span>
          </span>
          <span className="flex items-center gap-3">
            <span className="text-ink-muted">{statusText(alert)}</span>
            {alert.status === 'Pending' && (
              <Button
                variant="pillSecondary"
                size="pillMd"
                onClick={() => onCancel(alert.stockAlertId)}
              >
                Cancel
              </Button>
            )}
          </span>
        </li>
      ))}
    </ul>
  );
}
