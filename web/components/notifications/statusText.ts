import { formatInZone, timeOnlyInZone } from '@/lib/format/time';
import type { NotificationStatus } from '@/lib/api/notifications-log';

/**
 * `components/notifications/statusText.ts` — HO-NTF-002 §11.5, UI-6. A notification's status as
 * words (*Sent*, *Retrying*, *Held until 08:00*, *Undeliverable*, …) — status is text, never colour
 * alone. A `Queued` row with a future `deferredUntil` is *held for quiet hours*, shown with the
 * release time in the BRANCH zone (ADR-FE-015), not the browser's.
 */
export function statusLabel(status: string): string {
  switch (status) {
    case 'Queued':
      return 'Queued';
    case 'Sending':
      return 'Sending';
    case 'Sent':
      return 'Sent';
    case 'RetryPending':
      return 'Retrying';
    case 'Undeliverable':
      return 'Undeliverable';
    case 'Expired':
      return 'Expired';
    case 'Cancelled':
      return 'Cancelled';
    case 'InboxDelivered':
      return 'Delivered to inbox';
    default:
      return status;
  }
}

export function isHeld(
  item: { status: NotificationStatus | string; deferredUntil: string | null },
  now: number = Date.now(),
): boolean {
  return (
    item.status === 'Queued' &&
    item.deferredUntil !== null &&
    new Date(item.deferredUntil).getTime() > now
  );
}

export function describeStatus(
  item: { status: NotificationStatus | string; deferredUntil: string | null },
  timeZoneId: string,
): string {
  if (isHeld(item)) return `Held until ${timeOnlyInZone(item.deferredUntil!, timeZoneId)}`;
  return statusLabel(item.status);
}

export function formatLogTime(instant: string, timeZoneId: string): string {
  return formatInZone(instant, timeZoneId);
}

const CHANNEL_NAMES: Record<string, string> = {
  Sms: 'text message (SMS)',
  Email: 'email',
  InApp: 'in-app message',
  Push: 'push notification',
};

/** "email", "text message (SMS)" — lower-case, for use inside a sentence. */
export function channelName(channel: string): string {
  return CHANNEL_NAMES[channel] ?? channel.toLowerCase();
}

export function channelTitle(channel: string): string {
  const name = channelName(channel);
  return name.charAt(0).toUpperCase() + name.slice(1);
}
