'use client';

import { AlertTriangle, TrendingUp, XCircle } from 'lucide-react';
import { Amount } from '@/components/billing/Amount';
import { ProductPackArt, type PackTone } from '@/components/hearth/ProductPackArt';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { CatChip } from '@/components/orders/OrderLines';
import { resolveFileUrl } from '@/lib/api/files';
import type { CartLine } from '@/lib/api/cart';
import { formatMoney } from '@/lib/format/money';
import { cn } from '@/lib/utils';
import { QuantityStepper } from './QuantityStepper';

/**
 * `components/cart/CartLineRow.tsx` — HO-ORD-002 §11.1, Hearth cart card (docs/design/orders/
 * Cart.dc.html `.icard`). One line: image (or a tinted pack tile), product and variant, the cat chip on
 * a prescription line, availability and price notes **as text** (never colour alone), the stepper and
 * Remove. An unavailable line is dimmed, cannot be changed, and offers removal. Every number is the
 * server's (`availability`, `availableQuantity`, `priceChanged`, `lineSubtotal`).
 */
export function CartLineRow({
  line,
  maxQuantityPerLine,
  busy,
  tone = 'sunken',
  onQuantity,
  onRemove,
}: {
  line: CartLine;
  maxQuantityPerLine: number;
  busy: boolean;
  /** The tile tint when the line has no image. */
  tone?: PackTone;
  onQuantity: (quantity: number) => void;
  onRemove: () => void;
}) {
  const label = `${line.productName} ${line.variantLabel}`.trim();
  const unavailable = line.availability === 'Unavailable';
  const cap = Math.min(maxQuantityPerLine, line.availableQuantity ?? maxQuantityPerLine);
  const limitedByStock =
    line.availableQuantity !== null && line.availableQuantity <= maxQuantityPerLine;
  const imageUrl = resolveFileUrl(line.imagePath);

  return (
    <li
      className={cn(
        'flex gap-4 rounded-card border border-border p-3.5',
        unavailable ? 'bg-surface-sunken' : 'bg-surface',
      )}
    >
      <span
        aria-hidden="true"
        className={cn(
          'block h-24 w-[84px] flex-none overflow-hidden rounded-2xl',
          unavailable && 'opacity-55',
        )}
      >
        {imageUrl ? (
          // eslint-disable-next-line @next/next/no-img-element -- a signed API file URL, not a build-time asset
          <img src={imageUrl} alt="" className="h-full w-full object-cover" />
        ) : (
          <ProductPackArt tone={tone} className="size-full rounded-2xl" />
        )}
      </span>
      <div className="flex min-w-0 flex-1 flex-col gap-1.5">
        <div className={cn('flex min-w-0 flex-col gap-1.5', unavailable && 'opacity-70')}>
          <p className="m-0 text-[15px] font-bold leading-snug text-ink [overflow-wrap:anywhere]">
            {line.productName}
          </p>
          <p className="m-0 text-[13px] text-ink-muted [overflow-wrap:anywhere]">
            {line.variantLabel}
            {line.unitPrice && (
              <>
                {line.variantLabel ? ' · ' : ''}
                <Amount money={line.unitPrice} /> each
              </>
            )}
            {line.requiresPrescription ? ' · prescription food' : ''}
          </p>
          <div className="flex flex-wrap items-center gap-2">
            {line.availability === 'InStock' && (
              <span className="text-[13px] font-semibold text-accent">In stock</span>
            )}
            {line.availability === 'Limited' && (
              <Badge variant="warning" icon={<AlertTriangle size={12} aria-hidden="true" />}>
                {`Only ${line.availableQuantity ?? 0} left`}
              </Badge>
            )}
            {unavailable && (
              <Badge variant="danger" icon={<XCircle size={12} aria-hidden="true" />}>
                Unavailable
              </Badge>
            )}
            {line.priceChanged && line.unitPrice && (
              <Badge
                variant="warning"
                className="whitespace-normal"
                icon={<TrendingUp size={12} aria-hidden="true" />}
              >
                {`Price is now ${formatMoney(line.unitPrice)} (was ${formatMoney(line.priceWhenAdded)})`}
              </Badge>
            )}
            {line.cat && <CatChip name={line.cat.name} appearance="hearth" />}
            {line.prescriptionStatus === 'NotAuthorised' && (
              <Badge variant="danger" className="whitespace-normal">
                No current vet authorisation for this cat
              </Badge>
            )}
          </div>
        </div>
        {unavailable ? (
          <div className="mt-auto flex flex-wrap items-center justify-between gap-3">
            <p className="m-0 text-[13px] font-semibold text-danger-ink">
              This item can&rsquo;t be ordered right now.
            </p>
            <Button
              variant="pillSecondary"
              size="pillMd"
              disabled={busy}
              onClick={onRemove}
              aria-label={`Remove ${label} from cart`}
            >
              Remove from cart
            </Button>
          </div>
        ) : (
          <div className="mt-auto flex flex-wrap items-center justify-between gap-x-4 gap-y-2">
            <div className="flex flex-wrap items-start gap-2">
              <QuantityStepper
                label={label}
                value={line.quantity}
                max={cap}
                maxReason={
                  limitedByStock
                    ? `You can't add more — only ${line.availableQuantity} ${line.availableQuantity === 1 ? 'is' : 'are'} in stock.`
                    : `You can buy at most ${maxQuantityPerLine} of an item at a time.`
                }
                disabled={busy}
                onChange={onQuantity}
              />
              <button
                type="button"
                disabled={busy}
                onClick={onRemove}
                aria-label={`Remove ${label} from your cart`}
                className="inline-flex min-h-11 items-center rounded-pill px-3 text-sm font-semibold text-accent-ink hover:bg-accent-tint disabled:opacity-50"
              >
                Remove
              </button>
            </div>
            {line.lineSubtotal && (
              <p className="m-0 text-[17px] font-bold text-ink">
                <Amount money={line.lineSubtotal} />
              </p>
            )}
          </div>
        )}
      </div>
    </li>
  );
}
