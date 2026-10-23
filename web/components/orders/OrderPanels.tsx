'use client';

import Link from 'next/link';
import { ArrowUpRight, Mail, Phone, TriangleAlert } from 'lucide-react';
import { cardClass, flatCardClass } from '@/components/hearth/card';
import { IconTile } from '@/components/hearth/IconTile';
import { Button, buttonVariants } from '@/components/ui/button';
import { describeDroppedReason } from '@/lib/api/errors';
import type { PublicBranch } from '@/lib/api/admin-branches';
import type { CustomerShipment, CustomerStatusChange, ReorderResult } from '@/lib/api/my-orders';
import { formatOrderWhen, orderStatusLabel } from '@/lib/format/orders';
import { cn } from '@/lib/utils';
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

const SUB_LABEL = 'text-xs text-ink-muted';

/**
 * `ContactShopCard` — HO-ORD-002 §11.6, US-ORD-12 AC2. When an order can no longer be cancelled online,
 * the disabled **Cancel order** with the reason beside it, and the branch's phone and email (`tel:` /
 * `mailto:`, from `GET /api/branches`) so the customer can ask.
 */
export function ContactShopCard({
  branch,
  reason,
}: {
  branch: PublicBranch | undefined;
  /** Why Cancel is not available (a sentence). */
  reason: string;
}) {
  return (
    <section
      aria-labelledby="contact-heading"
      className={cn(flatCardClass, 'flex flex-col gap-2.5')}
    >
      <h2 id="contact-heading" className="m-0 text-base font-bold text-ink">
        Contact the shop
      </h2>
      <Button
        variant="pillSecondary"
        size="pillMd"
        disabled
        aria-describedby="cancel-why"
        className="self-start disabled:bg-surface"
      >
        Cancel order
      </Button>
      <p id="cancel-why" className="m-0 text-[13px] text-ink-muted">
        {reason} This order can&rsquo;t be changed online any more. Please call or email us and
        we&rsquo;ll help.
      </p>
      {branch ? (
        <div className="flex flex-col gap-2">
          <p className="m-0 text-sm font-semibold text-ink">{branch.name}</p>
          <div className="flex flex-wrap gap-2.5">
            {branch.phoneNumber && (
              <a
                href={`tel:${branch.phoneNumber.replace(/\s/g, '')}`}
                className={buttonVariants({ variant: 'pillSecondary', size: 'pillMd' })}
              >
                <Phone size={14} aria-hidden="true" />
                {branch.phoneNumber}
              </a>
            )}
            {branch.email && (
              <a
                href={`mailto:${branch.email}`}
                className={buttonVariants({ variant: 'pillSecondary', size: 'pillMd' })}
              >
                <Mail size={14} aria-hidden="true" />
                {branch.email}
              </a>
            )}
          </div>
        </div>
      ) : (
        <p className="m-0 text-sm text-ink-muted">
          Our contact details are on the shop&rsquo;s Contact page.
        </p>
      )}
    </section>
  );
}

function Row({
  label,
  children,
  action,
}: {
  label: string;
  children: React.ReactNode;
  action?: React.ReactNode;
}) {
  return (
    <div className="flex items-center justify-between gap-2.5 border-b border-dashed border-border py-2.5 last:border-0">
      <div className="flex min-w-0 flex-col">
        <dt className={SUB_LABEL}>{label}</dt>
        <dd className="m-0 text-sm [overflow-wrap:anywhere]">{children}</dd>
      </div>
      {action}
    </div>
  );
}

/** `TrackingPanel` — HO-ORD-002 §11.6, OQ-4: the courier, the tracking number as copyable text, a link
 *  only when the courier has a tracking page, and the last status the courier reported. */
export function TrackingPanel({ shipment }: { shipment: CustomerShipment }) {
  const href = safeHttpUrl(shipment.trackingUrl);
  return (
    <section aria-labelledby="tracking-heading" className={cardClass}>
      <h2 id="tracking-heading" className="m-0 text-base font-bold text-ink">
        Track your parcel
      </h2>
      <dl className="m-0 mt-1.5">
        <Row label="Courier">
          <span className="font-semibold text-ink">{shipment.courierName}</span>
        </Row>
        <Row
          label="Tracking number"
          action={
            shipment.trackingNumber ? (
              <CopyButton value={shipment.trackingNumber} label="tracking number" />
            ) : undefined
          }
        >
          {shipment.trackingNumber ? (
            <span className="font-mono text-[15px] font-semibold text-ink">
              {shipment.trackingNumber}
            </span>
          ) : (
            <span className="text-ink-muted">
              Not available yet — we&rsquo;ll tell you when it is added.
            </span>
          )}
        </Row>
        {shipment.lastTrackingStatus && (
          <Row label="Latest update">
            <span className="text-ink">{shipment.lastTrackingStatus}</span>
          </Row>
        )}
      </dl>
      {href && (
        <p className="m-0 mt-3">
          <a
            href={href}
            target="_blank"
            rel="noopener noreferrer"
            className={buttonVariants({ variant: 'pillSecondary', size: 'pillMd' })}
          >
            Track on the courier&rsquo;s site
            <ArrowUpRight size={14} aria-hidden="true" />
            <span className="sr-only"> (opens in a new tab)</span>
          </a>
        </p>
      )}
    </section>
  );
}

/** `OrderTimeline` — status and time only, never a staff name (OQ-6), in the branch's zone. The latest
 *  entry carries the apricot dot, earlier ones the teal dot (docs/design/orders/OrderDetail.dc.html). */
export function OrderTimeline({
  history,
  zone,
}: {
  history: CustomerStatusChange[];
  zone: string;
}) {
  return (
    <section aria-labelledby="timeline-heading" className={cardClass}>
      <h2 id="timeline-heading" className="m-0 text-base font-bold text-ink">
        What has happened
      </h2>
      <ol className="m-0 mt-3 flex list-none flex-col gap-3 p-0">
        {history.map((entry, index) => (
          <li key={`${entry.to}-${entry.changedOn}`} className="flex gap-3 text-sm">
            <span
              aria-hidden="true"
              className={cn(
                'mt-[5px] size-2.5 flex-none rounded-pill',
                index === history.length - 1 ? 'bg-warm' : 'bg-accent',
              )}
            />
            <span className="flex flex-col gap-0.5">
              <span className="font-semibold text-ink">{orderStatusLabel(entry.to)}</span>
              <span className={SUB_LABEL}>{formatOrderWhen(entry.changedOn, zone, true)}</span>
            </span>
          </li>
        ))}
      </ol>
    </section>
  );
}

/**
 * `ExpiredOrderCard` — HO-ORD-002 §11.6, US-ORD-19 AC3. *"We couldn't confirm your transfer in time, so
 * the items were released."* with **Order again** (the re-order endpoint puts what is still available
 * back in the cart at today's prices) and, after the call, a list of what could not be added. Neutral
 * and warning colours only: an expired transfer is a problem, so no warm band and no cat.
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
    <section
      aria-labelledby="expired-heading"
      className="flex gap-3.5 rounded-card border border-warning-ink/40 bg-surface p-5 ring-1 ring-warning-ink/15 sm:px-6"
    >
      <IconTile shape="tile" size="md" className="bg-warning-bg text-warning-ink">
        <TriangleAlert size={20} aria-hidden="true" />
      </IconTile>
      <div className="flex min-w-0 flex-1 flex-col gap-2.5">
        <h2 id="expired-heading" className="m-0 mt-2.5 text-base font-bold text-ink">
          This order has expired
        </h2>
        <p className="m-0 text-sm text-ink-muted">
          We couldn&rsquo;t confirm your transfer in time, so the items were released.
        </p>
        {result ? (
          <div role="status" className="text-sm">
            <p className="m-0 font-semibold">
              {result.addedCount === 0
                ? 'Nothing could be added to your cart.'
                : `${result.addedCount} ${result.addedCount === 1 ? 'item was' : 'items were'} added to your cart.`}
            </p>
            {result.unavailable.length > 0 && (
              <>
                <p className="m-0 mt-1">These could not be added:</p>
                <ul className="m-0 list-disc pl-5">
                  {result.unavailable.map((item) => (
                    <li key={item.productName}>
                      <b>{item.productName}</b> — {describeDroppedReason(item.reason)}
                    </li>
                  ))}
                </ul>
              </>
            )}
            <p className="m-0 mt-2">
              <Link href="/cart" className="font-semibold text-accent-ink underline">
                Go to your cart
              </Link>
            </p>
          </div>
        ) : (
          canReorder && (
            <>
              <Button
                variant="pill"
                size="pillMd"
                onClick={onReorder}
                disabled={pending}
                aria-busy={pending}
                aria-describedby="reorder-hint"
                className="self-start"
              >
                {pending ? 'Adding to your cart…' : 'Order again'}
              </Button>
              <p id="reorder-hint" className="m-0 text-xs text-ink-muted">
                Puts the same items back in your cart at today&rsquo;s prices. You check out as
                normal.
              </p>
            </>
          )
        )}
        {error && (
          <p role="alert" className="m-0 text-sm font-semibold text-danger-ink">
            {error}
          </p>
        )}
      </div>
    </section>
  );
}
