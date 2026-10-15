'use client';

import Link from 'next/link';
import { Mail, Phone } from 'lucide-react';
import { Button, buttonVariants } from '@/components/ui/button';
import { describeDroppedReason } from '@/lib/api/errors';
import type { PublicBranch } from '@/lib/api/admin-branches';
import type { CustomerShipment, CustomerStatusChange, ReorderResult } from '@/lib/api/my-orders';
import { formatOrderWhen, orderStatusLabel } from '@/lib/format/orders';
import { CopyButton } from './CopyButton';

/** `https:`/`http:` URLs only — a tracking link from the API is data, not trusted markup. */
export function safeHttpUrl(url: string | null | undefined): string | null {
  if (!url) return null;
  try {
    const parsed = new URL(url);
    return parsed.protocol === 'https:' || parsed.protocol === 'http:' ? parsed.toString() : null;
  } catch {
    return null;
  }
}

/**
 * `ContactShopCard` — HO-ORD-002 §11.6, US-ORD-12 AC2. When an order can no longer be cancelled online,
 * the branch's phone and email (`tel:` / `mailto:`, from `GET /api/branches`) so the customer can ask.
 */
export function ContactShopCard({ branch }: { branch: PublicBranch | undefined }) {
  return (
    <section aria-labelledby="contact-heading" className="rounded-[var(--radius-md)] border border-border bg-surface p-4">
      <h3 id="contact-heading" className="font-display text-base font-semibold text-ink">
        Contact the shop
      </h3>
      <p className="mt-1 text-sm text-ink-muted">
        This order can&rsquo;t be changed online any more. Please call or email us and we&rsquo;ll help.
      </p>
      {branch ? (
        <ul className="m-0 mt-2 flex list-none flex-col gap-1.5 p-0 text-sm">
          <li className="font-semibold text-ink">{branch.name}</li>
          {branch.phoneNumber && (
            <li className="flex items-center gap-2">
              <Phone size={14} aria-hidden="true" />
              <a href={`tel:${branch.phoneNumber.replace(/\s/g, '')}`} className="font-semibold text-accent-ink underline">
                {branch.phoneNumber}
              </a>
            </li>
          )}
          {branch.email && (
            <li className="flex items-center gap-2">
              <Mail size={14} aria-hidden="true" />
              <a href={`mailto:${branch.email}`} className="font-semibold text-accent-ink underline">
                {branch.email}
              </a>
            </li>
          )}
        </ul>
      ) : (
        <p className="mt-2 text-sm text-ink-muted">Our contact details are on the shop&rsquo;s Contact page.</p>
      )}
    </section>
  );
}

/** `TrackingPanel` — HO-ORD-002 §11.6, OQ-4: the courier, the tracking number as copyable text, a link
 *  only when the courier has a tracking page, and the last status the courier reported. */
export function TrackingPanel({ shipment }: { shipment: CustomerShipment }) {
  const href = safeHttpUrl(shipment.trackingUrl);
  return (
    <section aria-labelledby="tracking-heading" className="rounded-[var(--radius-md)] border border-border bg-surface p-4">
      <h3 id="tracking-heading" className="font-display text-base font-semibold text-ink">
        Track your parcel
      </h3>
      <dl className="m-0 mt-2 grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-sm">
        <dt className="text-ink-muted">Courier</dt>
        <dd className="m-0 font-semibold text-ink">{shipment.courierName}</dd>
        <dt className="text-ink-muted">Tracking number</dt>
        <dd className="m-0 flex flex-wrap items-center gap-2">
          {shipment.trackingNumber ? (
            <>
              <span className="font-semibold text-ink [overflow-wrap:anywhere]">{shipment.trackingNumber}</span>
              <CopyButton value={shipment.trackingNumber} label="tracking number" />
            </>
          ) : (
            <span className="text-ink-muted">Not available yet — we&rsquo;ll tell you when it is added.</span>
          )}
        </dd>
        {shipment.lastTrackingStatus && (
          <>
            <dt className="text-ink-muted">Latest update</dt>
            <dd className="m-0 text-ink">{shipment.lastTrackingStatus}</dd>
          </>
        )}
      </dl>
      {href && (
        <p className="mt-3">
          <a href={href} target="_blank" rel="noopener noreferrer" className={buttonVariants({ variant: 'secondary', size: 'sm' })}>
            Track on the courier&rsquo;s site
            <span className="sr-only"> (opens in a new tab)</span>
          </a>
        </p>
      )}
    </section>
  );
}

/** `OrderTimeline` — status and time only, never a staff name (OQ-6), in the branch's zone. */
export function OrderTimeline({ history, zone }: { history: CustomerStatusChange[]; zone: string }) {
  return (
    <section aria-labelledby="timeline-heading" className="rounded-[var(--radius-md)] border border-border bg-surface p-4">
      <h3 id="timeline-heading" className="font-display text-base font-semibold text-ink">
        Order history
      </h3>
      <ol className="m-0 mt-2 list-none p-0 text-sm">
        {history.map((entry) => (
          <li key={`${entry.to}-${entry.changedOn}`} className="flex flex-wrap justify-between gap-2 border-b border-dashed border-border py-1.5 last:border-0">
            <span className="font-semibold text-ink">{orderStatusLabel(entry.to)}</span>
            <span className="text-ink-muted">{formatOrderWhen(entry.changedOn, zone, true)}</span>
          </li>
        ))}
      </ol>
    </section>
  );
}

/**
 * `ExpiredOrderCard` — HO-ORD-002 §11.6, US-ORD-19 AC3. *"We couldn't confirm your transfer in time, so
 * the items were released."* with **Order again** (the re-order endpoint puts what is still available
 * back in the cart at today's prices) and, after the call, a list of what could not be added.
 */
export function ExpiredOrderCard({
  canReorder,
  pending,
  result,
  error,
  onReorder,
}: {
  canReorder: boolean;
  pending: boolean;
  result: ReorderResult | null;
  error: string | null;
  onReorder: () => void;
}) {
  return (
    <section aria-labelledby="expired-heading" className="flex flex-col gap-3 rounded-[var(--radius-md)] border border-warning-ink/30 bg-warning-bg p-4 text-warning-ink">
      <h3 id="expired-heading" className="font-display text-base font-semibold">
        This order has expired
      </h3>
      <p className="text-sm">We couldn&rsquo;t confirm your transfer in time, so the items were released.</p>
      {result ? (
        <div role="status" className="text-sm">
          <p className="font-semibold">
            {result.addedCount === 0
              ? 'Nothing could be added to your cart.'
              : `${result.addedCount} ${result.addedCount === 1 ? 'item was' : 'items were'} added to your cart.`}
          </p>
          {result.unavailable.length > 0 && (
            <>
              <p className="mt-1">These could not be added:</p>
              <ul className="m-0 list-disc pl-5">
                {result.unavailable.map((item) => (
                  <li key={item.productName}>
                    <b>{item.productName}</b> — {describeDroppedReason(item.reason)}
                  </li>
                ))}
              </ul>
            </>
          )}
          <p className="mt-2">
            <Link href="/cart" className="font-semibold underline">
              Go to your cart
            </Link>
          </p>
        </div>
      ) : (
        canReorder && (
          <div>
            <Button onClick={onReorder} disabled={pending} aria-busy={pending}>
              {pending ? 'Adding to your cart…' : 'Order again'}
            </Button>
          </div>
        )
      )}
      {error && (
        <p role="alert" className="text-sm font-semibold text-danger-ink">
          {error}
        </p>
      )}
    </section>
  );
}
