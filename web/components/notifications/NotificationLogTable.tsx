'use client';

import Link from 'next/link';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { EmptyState } from '@/components/common/EmptyState';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { SimplePager } from './SimplePager';
import { channelTitle, describeStatus, formatLogTime, isHeld } from './statusText';
import type { NotificationLogItem } from '@/lib/api/notifications-log';

function Tags({ item }: { item: NotificationLogItem }) {
  if (!item.isResend && !item.isFallback) return null;
  return (
    <span className="flex flex-wrap gap-1">
      {item.isResend && <Badge variant="neutral">Resend</Badge>}
      {item.isFallback && <Badge variant="neutral">Fallback</Badge>}
    </span>
  );
}

function StatusText({ item, timeZoneId }: { item: NotificationLogItem; timeZoneId: string }) {
  const held = isHeld(item);
  return (
    <span className="font-semibold text-ink">
      {describeStatus(item, timeZoneId)}
      {held && <span className="ml-1 text-xs font-normal text-ink-muted">({timeZoneId})</span>}
    </span>
  );
}

/**
 * `components/notifications/NotificationLogTable.tsx` — HO-NTF-002 §11.5, US-NTF-06, UI-1/UI-4.
 * Time, template, channel, destination (masked), recipient, status as text, attempts, *Resend* /
 * *Fallback* tags and the last provider response (truncated). Paged. Below 768 px the table
 * becomes a list of cards (the table is `hidden md:block`, the cards `md:hidden`; only one is ever
 * displayed). Every value is a React text child — a provider response containing markup is shown
 * as the characters it is (ADR-FE-162).
 */
export function NotificationLogTable({
  items,
  isLoading,
  isError,
  isFetching = false,
  onRetry,
  timeZoneId,
  pageNumber,
  pageSize,
  totalCount,
  onPageChange,
}: {
  items: NotificationLogItem[] | undefined;
  isLoading: boolean;
  isError: boolean;
  /** A request is in flight while `items` (possibly the previous page's or filter's) are shown. */
  isFetching?: boolean;
  onRetry: () => void;
  timeZoneId: string;
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  onPageChange: (page: number) => void;
}) {
  if (isLoading) return <LoadingState variant="table" rows={6} label="Loading messages…" />;
  if (isError && !items) {
    return (
      <ErrorState
        title="We couldn't load the message log."
        description="This does not mean there are no messages — please try again."
        onRetry={onRetry}
      />
    );
  }
  if (!items || items.length === 0) {
    return (
      <EmptyState
        title="No messages match these filters"
        description="Try a wider date range or clear a filter."
      />
    );
  }

  return (
    <div className="flex flex-col gap-3" aria-busy={isFetching}>
      {isError && (
        <div
          role="alert"
          className="flex flex-wrap items-center gap-3 rounded-[var(--radius-sm)] border border-danger-ink bg-danger-bg p-3 text-sm font-semibold text-danger-ink"
        >
          <span>
            We couldn&rsquo;t refresh the message log &mdash; these rows may be out of date.
          </span>
          <Button type="button" variant="secondary" size="sm" onClick={onRetry}>
            Retry
          </Button>
        </div>
      )}
      <p role="status" className="min-h-5 text-xs text-ink-muted">
        {isFetching ? 'Updating…' : ''}
      </p>
      <div
        role="region"
        aria-label="Message log (scrolls horizontally)"
        tabIndex={0}
        className="hidden w-full overflow-x-auto rounded-[var(--radius-md)] border border-border md:block"
      >
        <table className="w-full border-collapse bg-surface text-sm">
          <caption className="sr-only">Message log</caption>
          <thead>
            <tr className="text-left text-xs font-semibold text-ink-muted">
              {[
                'Time',
                'Template',
                'Channel',
                'Destination',
                'Recipient',
                'Status',
                'Attempts',
                'Tags',
                'Last provider response',
              ].map((heading) => (
                <th key={heading} scope="col" className="border-b border-border p-2">
                  {heading}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {items.map((item) => (
              <tr key={item.id} className="align-top">
                <td className="border-b border-border p-2">
                  <Link
                    href={`/staff/notifications/${item.id}`}
                    className="font-semibold text-accent-ink underline underline-offset-2"
                  >
                    {formatLogTime(item.createdOn, timeZoneId)}
                  </Link>
                </td>
                <td className="border-b border-border p-2">{item.templateName}</td>
                <td className="border-b border-border p-2">{channelTitle(item.channel)}</td>
                <td className="border-b border-border p-2">{item.destinationMasked ?? '—'}</td>
                <td className="border-b border-border p-2">{item.recipientName}</td>
                <td className="border-b border-border p-2">
                  <StatusText item={item} timeZoneId={timeZoneId} />
                </td>
                <td className="border-b border-border p-2">{item.attemptCount}</td>
                <td className="border-b border-border p-2">
                  <Tags item={item} />
                </td>
                <td className="max-w-[16rem] border-b border-border p-2">
                  <span
                    className="block truncate text-ink-muted"
                    title={item.lastProviderResponse ?? ''}
                  >
                    {item.lastProviderResponse ?? '—'}
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <ul className="m-0 flex list-none flex-col gap-3 p-0 md:hidden" aria-label="Message log">
        {items.map((item) => (
          <li
            key={item.id}
            className="flex flex-col gap-1.5 rounded-[var(--radius-md)] border border-border bg-surface p-3"
          >
            <div className="flex flex-wrap items-start justify-between gap-2">
              <span className="font-semibold text-ink">{item.templateName}</span>
              <StatusText item={item} timeZoneId={timeZoneId} />
            </div>
            <p className="text-xs text-ink-muted">{formatLogTime(item.createdOn, timeZoneId)}</p>
            <p className="break-words text-sm text-ink">
              {channelTitle(item.channel)} · {item.destinationMasked ?? '—'}
            </p>
            <p className="text-sm text-ink-muted">
              {item.recipientName} · {item.attemptCount}{' '}
              {item.attemptCount === 1 ? 'attempt' : 'attempts'}
            </p>
            <Tags item={item} />
            {item.lastProviderResponse && (
              <p className="line-clamp-2 break-words text-xs text-ink-muted">
                {item.lastProviderResponse}
              </p>
            )}
            <Link
              href={`/staff/notifications/${item.id}`}
              className="w-fit text-sm font-semibold text-accent-ink underline underline-offset-2"
            >
              View details
            </Link>
          </li>
        ))}
      </ul>

      <SimplePager
        pageNumber={pageNumber}
        pageSize={pageSize}
        totalCount={totalCount}
        onPageChange={onPageChange}
      />
    </div>
  );
}
