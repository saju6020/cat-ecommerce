'use client';

import { useEffect, useRef, useState } from 'react';
import { Clock } from 'lucide-react';
import {
  countdownMilestone,
  formatOrderWhen,
  formatTimeLeft,
  milestoneAnnouncement,
} from '@/lib/format/orders';
import { useServerNow } from '@/lib/hooks/useServerNow';

/**
 * `components/orders/TransferCountdown.tsx` — HO-ORD-002 ADR-FE-096, UI-6. *"Transfer by Mon 12 Oct,
 * 10:30 (Asia/Dhaka)"* and the time left, computed on the **server clock** (`useServerNow`). The timer
 * region (`role="timer"`) is not live; a separate polite region announces at **6 h, 1 h and expiry
 * only** — never continuously.
 */
/** After the deadline: how often, and how many times, the order is checked while it is still PendingPayment. */
export const EXPIRY_RECHECK_MS = 10_000;
const EXPIRY_RECHECKS = 18;

export function TransferCountdown({
  dueBy,
  zone,
  onExpired,
}: {
  dueBy: string;
  zone: string;
  /** Called once when the deadline passes, so the page can ask the server what became of the order. */
  onExpired?: () => void;
}) {
  const now = useServerNow(1000);
  const remaining = Date.parse(dueBy) - now;
  const milestone = countdownMilestone(remaining);
  const [announcement, setAnnouncement] = useState('');
  const announced = useRef<string | null>(null);
  // The milestone the page opened at is state, not news. A ref set once at mount survives React StrictMode's
  // double-run of effects (a "has mounted" flag flipped inside the effect would not).
  const openedAt = useRef(milestone);
  const expiredCallback = useRef(onExpired);
  expiredCallback.current = onExpired;

  // At the deadline the server still has to cancel the order, so one look can find it PendingPayment. Ask again
  // every EXPIRY_RECHECK_MS for a few minutes; the page unmounts this when the status leaves PendingPayment.
  useEffect(() => {
    if (milestone !== 'expired') return undefined;
    let asked = 0;
    expiredCallback.current?.();
    const timer = setInterval(() => {
      asked += 1;
      if (asked > EXPIRY_RECHECKS) {
        clearInterval(timer);
        return;
      }
      expiredCallback.current?.();
    }, EXPIRY_RECHECK_MS);
    return () => clearInterval(timer);
  }, [milestone]);

  useEffect(() => {
    if (milestone && announced.current !== milestone) {
      if (milestone !== openedAt.current || milestone === 'expired')
        setAnnouncement(milestoneAnnouncement(milestone));
      announced.current = milestone;
    }
  }, [milestone]);

  return (
    <div className="flex flex-col gap-1 rounded-2xl bg-warning-bg px-[18px] py-3.5 text-warning-ink">
      <p className="m-0 flex items-center gap-2.5">
        <Clock size={18} aria-hidden="true" className="flex-none" />
        <span className="font-semibold">{`Transfer by ${formatOrderWhen(dueBy, zone, true)}`}</span>
      </p>
      <p role="timer" className="m-0 pl-[28px] text-sm">
        {remaining > 0
          ? `Time left: ${formatTimeLeft(remaining)}`
          : 'The time to make the transfer has passed. We are checking the order…'}
      </p>
      <p role="status" aria-live="polite" className="sr-only">
        {announcement}
      </p>
    </div>
  );
}
