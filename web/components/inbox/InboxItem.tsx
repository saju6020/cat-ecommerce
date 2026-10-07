'use client';

import { useId, useRef, useState } from 'react';
import Link from 'next/link';
import { ChevronDown, ChevronRight } from 'lucide-react';
import { isSafeInternalPath } from '@/lib/routing/isSafeInternalPath';
import { formatAbsolute, formatRelative } from './relativeTime';
import { cn } from '@/lib/utils';
import type { InboxItem as InboxItemModel } from '@/lib/api/inbox';

/**
 * `InboxItemBody` — the message content, shared by the inbox row and the template editor's in-app
 * preview card ("a card mirroring `InboxItem`", HO-NTF-002 §11.3). Title and body are **plain
 * text** in `whitespace-pre-wrap` elements: React text children, never markup (ADR-FE-162). The
 * **View** link exists only when `isSafeInternalPath(linkPath)`; otherwise the item simply has no
 * link.
 */
export function InboxItemBody({
  body,
  linkPath,
  createdOn,
}: {
  body: string;
  linkPath: string | null;
  createdOn?: string;
}) {
  return (
    <div className="flex flex-col gap-3">
      <p className="whitespace-pre-wrap break-words text-sm text-ink">{body}</p>
      {createdOn && <p className="text-xs text-ink-muted">{formatAbsolute(createdOn)}</p>}
      {isSafeInternalPath(linkPath) && (
        <Link
          href={linkPath}
          className="w-fit rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-1.5 text-sm font-semibold text-accent-ink no-underline hover:bg-surface-sunken"
        >
          View
        </Link>
      )}
    </div>
  );
}

/**
 * `components/inbox/InboxItem.tsx` — HO-NTF-002 §11.1, US-NTF-01. A disclosure: the header is one
 * `<button aria-expanded>` showing the title, the first line of the body, the relative time and —
 * for an unread item — the word "Unread" as text plus a heavier weight (status is never colour
 * alone, UI-6). Opening an unread item calls `onOpen` exactly once (OQ-3: opening marks it read);
 * the page wires the mutation, which invalidates the list and the badge count.
 */
export function InboxItem({
  item,
  onOpen,
}: {
  item: InboxItemModel;
  onOpen: (item: InboxItemModel) => void;
}) {
  const [expanded, setExpanded] = useState(false);
  const reported = useRef(false);
  const panelId = useId();
  const isUnread = item.readOn === null;
  const firstLine = item.body.split('\n')[0] ?? '';

  function toggle() {
    const next = !expanded;
    setExpanded(next);
    if (next && isUnread && !reported.current) {
      reported.current = true;
      onOpen(item);
    }
  }

  return (
    <li className="rounded-[var(--radius-md)] border border-border bg-surface">
      <h2 className="m-0 text-base">
        <button
          type="button"
          aria-expanded={expanded}
          aria-controls={panelId}
          onClick={toggle}
          className="flex w-full items-start gap-3 rounded-[var(--radius-md)] p-4 text-left focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
        >
          {expanded ? (
            <ChevronDown size={16} aria-hidden="true" className="mt-1 flex-none text-ink-muted" />
          ) : (
            <ChevronRight size={16} aria-hidden="true" className="mt-1 flex-none text-ink-muted" />
          )}
          <span className="flex min-w-0 flex-1 flex-col gap-1">
            <span className="flex flex-wrap items-center gap-x-2 gap-y-1">
              {isUnread && (
                <span className="rounded-full bg-warm-soft px-2 py-0.5 text-[11px] font-bold text-warm-ink">
                  Unread
                </span>
              )}
              <span
                className={cn(
                  'break-words text-[15px] text-ink',
                  isUnread ? 'font-bold' : 'font-medium',
                )}
              >
                {item.title ?? firstLine}
              </span>
            </span>
            {!expanded && item.title && (
              <span className="truncate text-sm text-ink-muted">{firstLine}</span>
            )}
            <time dateTime={item.createdOn} className="text-xs text-ink-muted">
              {formatRelative(item.createdOn)}
            </time>
          </span>
        </button>
      </h2>
      <div id={panelId} hidden={!expanded} className="border-t border-border px-4 pb-4 pt-3">
        {expanded && (
          <InboxItemBody body={item.body} linkPath={item.linkPath} createdOn={item.createdOn} />
        )}
      </div>
    </li>
  );
}
