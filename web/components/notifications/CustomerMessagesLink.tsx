import Link from 'next/link';
import { MessageSquare } from 'lucide-react';
import { NOTIFICATION_LOG_VIEW_ROLES } from '@/lib/api/notifications-log';

/**
 * `components/notifications/CustomerMessagesLink.tsx` — HO-NTF-002 §11.5, US-NTF-06. On HO-ACC-002's
 * staff customer page: **Messages** -> the notification log filtered to this customer, with the
 * default date range. Shown only to roles in the presentation map for `NotificationLog.View`
 * (Administrator, Reception); that is convenience, not authorization — the log's own `403` still
 * decides (ADR-FE-009).
 */
export function CustomerMessagesLink({
  customerId,
  roleIds,
}: {
  customerId: string;
  roleIds: readonly string[];
}) {
  if (!NOTIFICATION_LOG_VIEW_ROLES.some((role) => roleIds.includes(role))) return null;
  return (
    <Link
      href={`/staff/notifications?recipientId=${encodeURIComponent(customerId)}`}
      className="inline-flex w-fit items-center gap-2 rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm font-semibold text-ink no-underline hover:bg-surface-sunken"
    >
      <MessageSquare size={14} aria-hidden="true" />
      Messages
    </Link>
  );
}
