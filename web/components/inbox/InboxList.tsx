'use client';

import { EmptyCatIllustration, EmptyState } from '@/components/common/EmptyState';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { SimplePager } from '@/components/notifications/SimplePager';
import { InboxItem } from './InboxItem';
import { cn } from '@/lib/utils';
import type { InboxItem as InboxItemModel } from '@/lib/api/inbox';

export type InboxFilter = 'all' | 'unread';

/**
 * `components/inbox/InboxList.tsx` — ⭐ HO-NTF-002 §11.1, US-NTF-01. Newest first, 20 per page (OQ-2),
 * filtered **All / Unread**. Takes data and callbacks as props (the page wires the hooks), with
 * distinct loading, error and empty states. The empty state is the portal's warm one and says what
 * will appear here, not just "nothing".
 */
export interface InboxListProps {
  items: InboxItemModel[] | undefined;
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  filter: InboxFilter;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
  onFilterChange: (filter: InboxFilter) => void;
  onPageChange: (page: number) => void;
  onOpenItem: (item: InboxItemModel) => void;
}

const FILTERS: Array<{ id: InboxFilter; label: string }> = [
  { id: 'all', label: 'All' },
  { id: 'unread', label: 'Unread' },
];

export function InboxList({
  items,
  totalCount,
  pageNumber,
  pageSize,
  filter,
  isLoading,
  isError,
  onRetry,
  onFilterChange,
  onPageChange,
  onOpenItem,
}: InboxListProps) {
  return (
    <div className="flex flex-col gap-4">
      <div role="group" aria-label="Show messages" className="flex gap-2">
        {FILTERS.map((option) => (
          <button
            key={option.id}
            type="button"
            aria-pressed={filter === option.id}
            onClick={() => onFilterChange(option.id)}
            className={cn(
              'rounded-full border px-4 py-1.5 text-sm font-semibold focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent',
              filter === option.id
                ? 'border-warm-ink bg-warm-soft text-warm-ink'
                : 'border-border-strong bg-surface text-ink-muted hover:bg-surface-sunken',
            )}
          >
            {option.label}
          </button>
        ))}
      </div>

      {isLoading ? (
        <LoadingState variant="table" rows={5} label="Loading your messages…" />
      ) : isError && !items ? (
        <ErrorState
          title="We couldn't load your messages just now."
          description="Your messages are safe — please try again."
          onRetry={onRetry}
        />
      ) : !items || items.length === 0 ? (
        filter === 'unread' ? (
          <EmptyState
            title="No unread messages"
            description="You're all caught up."
            icon={<EmptyCatIllustration />}
          />
        ) : (
          <EmptyState
            title="No messages yet."
            description="Reminders and updates about your cats will appear here."
            icon={<EmptyCatIllustration />}
          />
        )
      ) : (
        <>
          <ul className="m-0 flex list-none flex-col gap-3 p-0">
            {items.map((item) => (
              <InboxItem key={item.id} item={item} onOpen={onOpenItem} />
            ))}
          </ul>
          <SimplePager
            pageNumber={pageNumber}
            pageSize={pageSize}
            totalCount={totalCount}
            onPageChange={onPageChange}
          />
        </>
      )}
    </div>
  );
}
