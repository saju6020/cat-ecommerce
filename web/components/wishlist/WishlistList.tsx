import Link from 'next/link';
import { Button, buttonVariants } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { EmptyCatIllustration, EmptyState } from '@/components/common/EmptyState';
import { ProductImage } from '@/components/product/ProductImage';
import { AvailabilityText } from '@/components/product/StatusText';
import { formatMoney } from '@/lib/format/money';
import { formatShortInstant } from '@/lib/format/shop';
import { productHref } from '@/lib/shop/helpers';
import type { StockAlert, WishlistItem } from '@/lib/api/shop-customer';

/** HO-SHP-002 §11.5 `WishlistList` — image, name, variant, price, availability in TEXT, *No longer
 *  sold* when `onSale` is false, **Remove**, and a link to the product with `?sku=`. */
export function WishlistList({
  items,
  onRemove,
  removingSkuId,
}: {
  items: WishlistItem[];
  onRemove: (skuId: string) => void;
  removingSkuId?: string;
}) {
  if (items.length === 0) {
    return (
      <EmptyState
        icon={<EmptyCatIllustration />}
        title="Nothing saved yet"
        description="Tap Save on a product to keep it here."
        action={
          <Link href="/shop/products" className={buttonVariants({ variant: 'secondary' })}>
            Browse the shop
          </Link>
        }
      />
    );
  }
  return (
    <ul className="m-0 grid list-none grid-cols-1 gap-3 p-0 sm:grid-cols-2">
      {items.map((item) => (
        <li key={item.skuId}>
          <article className="flex gap-3 rounded-[var(--radius-md)] border border-border bg-surface p-3">
            <div className="w-24 flex-none">
              <ProductImage
                image={item.primaryImage}
                fallbackAlt={`${item.productName} — photo coming soon`}
                size={96}
              />
            </div>
            <div className="flex min-w-0 flex-1 flex-col gap-1">
              <h3 className="text-sm font-semibold text-ink">
                <Link
                  href={productHref(item.slug, { sku: item.skuId })}
                  className="text-ink no-underline hover:underline"
                >
                  {item.productName}
                </Link>
              </h3>
              <p className="text-sm text-ink-muted">{item.skuLabel}</p>
              {item.price && (
                <p className="text-sm font-semibold text-ink">{formatMoney(item.price)}</p>
              )}
              {item.onSale ? (
                <AvailabilityText availability={item.availability} />
              ) : (
                <Badge variant="neutral">No longer sold</Badge>
              )}
              <Button
                variant="ghost"
                size="sm"
                className="self-start"
                disabled={removingSkuId === item.skuId}
                aria-label={`Remove ${item.productName} (${item.skuLabel}) from wishlist`}
                onClick={() => onRemove(item.skuId)}
              >
                Remove
              </Button>
            </div>
          </article>
        </li>
      ))}
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
          className="flex flex-wrap items-center justify-between gap-2 rounded-[var(--radius-sm)] border border-border bg-surface px-3 py-2 text-sm"
        >
          <span>
            <span className="font-semibold text-ink">{alert.productName ?? 'Product'}</span>{' '}
            <span className="text-ink-muted">({alert.skuLabel ?? 'size'})</span>
          </span>
          <span className="flex items-center gap-3">
            <span className="text-ink-muted">{statusText(alert)}</span>
            {alert.status === 'Pending' && (
              <Button variant="ghost" size="sm" onClick={() => onCancel(alert.stockAlertId)}>
                Cancel
              </Button>
            )}
          </span>
        </li>
      ))}
    </ul>
  );
}
