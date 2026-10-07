'use client';

import Link from 'next/link';
import { Bell } from 'lucide-react';
import { useUnreadCount } from '@/lib/hooks/useInbox';

export const BADGE_CAP = 99; // OQ-1

/** The text a screen reader gets: "Inbox, 3 unread messages" (UI-6). */
export function inboxAccessibleName(count: number): string {
  if (count <= 0) return 'Inbox';
  if (count > BADGE_CAP) return `Inbox, more than ${BADGE_CAP} unread messages`;
  return `Inbox, ${count} unread ${count === 1 ? 'message' : 'messages'}`;
}

/** The text announced when the count changes (the link's own name is not re-read on change). */
export function inboxLiveText(count: number): string {
  if (count <= 0) return 'No unread messages';
  if (count > BADGE_CAP) return `More than ${BADGE_CAP} unread messages`;
  return `${count} unread ${count === 1 ? 'message' : 'messages'}`;
}

/**
 * `components/inbox/InboxBadge.tsx` — ⭐ HO-NTF-002 §11.1, US-NTF-01. Sits in the `(portal)` header: a
 * link to `/portal/inbox` carrying the unread count from `['inbox','unread-count']`, polled per
 * ADR-FE-159. The count bubble is hidden at 0 and reads `99+` above 99; the link itself stays so the
 * inbox is always reachable. The count is text, not colour alone, and the link's accessible name
 * states it ("Inbox, 3 unread messages"). A failed poll shows nothing rather than an error — the
 * badge is a hint, and the inbox page has its own error state. A visually hidden polite live region
 * announces count changes, since the link's accessible name is not re-read when it changes (N15).
 */
export function InboxBadge() {
  const { data } = useUnreadCount();
  const count = data?.count ?? 0;

  return (
    <>
      <Link
        href="/portal/inbox"
        aria-label={inboxAccessibleName(count)}
        className="relative inline-flex items-center gap-1.5 rounded-[var(--radius-sm)] px-2 py-1.5 text-sm font-semibold text-ink-muted no-underline hover:bg-surface-sunken"
      >
        <Bell size={16} aria-hidden="true" />
        <span aria-hidden="true">Inbox</span>
        {count > 0 && (
          <span
            aria-hidden="true"
            data-testid="inbox-unread-count"
            className="inline-flex min-w-5 items-center justify-center rounded-full bg-warm-ink px-1.5 text-[11px] font-bold leading-5 text-white"
          >
            {count > BADGE_CAP ? `${BADGE_CAP}+` : count}
          </span>
        )}
      </Link>
      <span role="status" aria-live="polite" className="sr-only">
        {inboxLiveText(count)}
      </span>
    </>
  );
}
