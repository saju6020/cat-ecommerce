import Link from 'next/link';
import { Bell, BellOff } from 'lucide-react';
import { Button, buttonVariants } from '@/components/ui/button';
import { describeChannels } from '@/lib/shop/helpers';
import type { StockAlertDeliverability } from '@/lib/api/shop-customer';

export interface StockAlertButtonProps {
  /** True when this customer already has a pending alert for the selected SKU. */
  requested: boolean;
  /** Known only right after the request: the real routing decision (HO-SHP-001 F33 / OQ-2). */
  deliverability?: StockAlertDeliverability;
  pending?: boolean;
  onRequest: () => void;
  onCancel: () => void;
  signInHref?: string;
  error?: string;
}

/**
 * HO-SHP-002 §11.3 `StockAlertButton` — ADR-FE-038, US-SHP-13. *Tell me when it's back* asks the
 * API; the confirmation shows the server's `deliverability`: when `willNotify` is false it says the
 * alert will NOT reach the customer (OQ-7) and links to notification settings, otherwise it names
 * the channels. An existing alert reads *You'll be told when it's back* with **Cancel alert**.
 */
export function StockAlertButton({
  requested,
  deliverability,
  pending,
  onRequest,
  onCancel,
  signInHref,
  error,
}: StockAlertButtonProps) {
  if (signInHref) {
    return (
      <Link
        href={signInHref}
        className={buttonVariants({ variant: 'pillSecondary', size: 'pillMd' })}
      >
        <Bell size={16} aria-hidden="true" />
        {"Tell me when it's back"}
      </Link>
    );
  }
  return (
    <div className="flex flex-col gap-2">
      {requested ? (
        <div className="flex flex-col gap-2">
          {deliverability && !deliverability.willNotify ? (
            <p role="status" className="flex items-start gap-2 text-sm text-warning-ink">
              <BellOff size={16} className="mt-0.5 flex-none" aria-hidden="true" />
              <span>
                {
                  "You haven't agreed to receive offers and updates, so we can't tell you when this is back. Turn on in-app, SMS or email updates in your "
                }
                <Link href="/portal/account/notifications" className="font-semibold underline">
                  notification settings
                </Link>
                .
              </span>
            </p>
          ) : (
            <p role="status" className="text-sm text-ink">
              {"You'll be told when it's back."}
              {deliverability && (
                <>
                  {" We'll send one message by " + describeChannels(deliverability.channels) + '.'}
                </>
              )}
            </p>
          )}
          <Button
            variant="pillSecondary"
            size="pillMd"
            className="self-start"
            disabled={pending}
            onClick={onCancel}
          >
            Cancel alert
          </Button>
        </div>
      ) : (
        <Button variant="pillSecondary" size="pillMd" disabled={pending} onClick={onRequest}>
          <Bell size={16} aria-hidden="true" />
          {"Tell me when it's back"}
        </Button>
      )}
      {error && (
        <p role="alert" className="text-xs font-semibold text-danger-ink">
          {error}
        </p>
      )}
    </div>
  );
}
