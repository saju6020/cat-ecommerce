'use client';

import { AlertTriangle, CircleCheck, TrendingUp, XCircle } from 'lucide-react';
import { Amount } from '@/components/billing/Amount';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { CatChip } from '@/components/orders/OrderLines';
import { resolveFileUrl } from '@/lib/api/files';
import type { CartLine } from '@/lib/api/cart';
import { formatMoney } from '@/lib/format/money';
import { QuantityStepper } from './QuantityStepper';

/**
 * `components/cart/CartLineRow.tsx` — HO-ORD-002 §11.1. One line: image, product and variant, the cat
 * chip on a prescription line, availability and price notes **as text** (never colour alone), the
 * stepper and Remove. An unavailable line is dimmed, cannot be changed, and offers removal. Every
 * number is the server's (`availability`, `availableQuantity`, `priceChanged`, `lineSubtotal`).
 */
export function CartLineRow({
  line,
  maxQuantityPerLine,
  busy,
  onQuantity,
  onRemove,
}: {
  line: CartLine;
  maxQuantityPerLine: number;
  busy: boolean;
  onQuantity: (quantity: number) => void;
  onRemove: () => void;
}) {
  const label = `${line.productName} ${line.variantLabel}`.trim();
  const unavailable = line.availability === 'Unavailable';
  const cap = Math.min(maxQuantityPerLine, line.availableQuantity ?? maxQuantityPerLine);
  const limitedByStock = line.availableQuantity !== null && line.availableQuantity <= maxQuantityPerLine;
  const imageUrl = resolveFileUrl(line.imagePath);

  return (
    <li className="flex gap-3 border-b border-border py-4 last:border-0 sm:gap-4">
      <div
        aria-hidden="true"
        className={`flex h-14 w-14 flex-none items-center justify-center overflow-hidden rounded-lg border border-border bg-surface-sunken text-[10px] text-ink-muted ${unavailable ? 'opacity-55' : ''}`}
      >
        {/* eslint-disable-next-line @next/next/no-img-element -- a signed API file URL, not a build-time asset */}
        {imageUrl ? <img src={imageUrl} alt="" className="h-full w-full object-cover" /> : 'Photo'}
      </div>
      <div className="flex min-w-0 flex-1 flex-col gap-2 sm:flex-row sm:justify-between">
        <div className={`min-w-0 ${unavailable ? 'opacity-70' : ''}`}>
          <p className="font-bold text-ink [overflow-wrap:anywhere]">{line.productName}</p>
          <p className="text-sm text-ink-muted [overflow-wrap:anywhere]">
            {line.variantLabel}
            {line.unitPrice && (
              <>
                {line.variantLabel ? ' · ' : ''}
                <Amount money={line.unitPrice} /> each
              </>
            )}
            {line.requiresPrescription ? ' · prescription food' : ''}
          </p>
          <div className="mt-1.5 flex flex-wrap items-center gap-2">
            {line.availability === 'InStock' && (
              <Badge variant="success" icon={<CircleCheck size={12} aria-hidden="true" />}>
                In stock
              </Badge>
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
            {line.cat && <CatChip name={line.cat.name} />}
            {line.prescriptionStatus === 'NotAuthorised' && (
              <Badge variant="danger" className="whitespace-normal">
                No current vet authorisation for this cat
              </Badge>
            )}
          </div>
          {unavailable ? (
            <p className="mt-2 text-sm font-semibold text-danger-ink">
              This item can&rsquo;t be ordered right now.
            </p>
          ) : (
            <div className="mt-2.5 flex flex-wrap items-start gap-3">
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
              <Button variant="ghost" size="sm" disabled={busy} onClick={onRemove} aria-label={`Remove ${label} from your cart`}>
                Remove
              </Button>
            </div>
          )}
        </div>
        <div className="flex flex-row items-start justify-between gap-2 sm:w-36 sm:flex-col sm:items-end">
          {line.lineSubtotal && !unavailable ? (
            <p className="font-bold text-ink">
              <Amount money={line.lineSubtotal} />
            </p>
          ) : (
            <Button variant="secondary" size="sm" disabled={busy} onClick={onRemove} aria-label={`Remove ${label} from cart`}>
              Remove from cart
            </Button>
          )}
        </div>
      </div>
    </li>
  );
}
