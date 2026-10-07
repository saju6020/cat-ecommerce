'use client';

import { Suspense } from 'react';
import { usePathname, useRouter, useSearchParams } from 'next/navigation';
import { InboxList, type InboxFilter } from '@/components/inbox/InboxList';
import { LoadingState } from '@/components/common/LoadingState';
import { useInboxList, useMarkInboxItemRead } from '@/lib/hooks/useInbox';
import { useFilterUrlUpdate } from '@/lib/hooks/useFilterUrlUpdate';
import { INBOX_PAGE_SIZE } from '@/lib/api/inbox';

/** `/portal/inbox` — US-NTF-01. The filter and page live in the URL (`?filter=unread&page=2`), so
 *  Back restores them. Opening an item marks it read (OQ-3) and invalidates the badge count. */
function InboxContent() {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const setFilterParam = useFilterUrlUpdate();

  const filter: InboxFilter = searchParams.get('filter') === 'unread' ? 'unread' : 'all';
  const pageNumber = Math.max(1, Number(searchParams.get('page') ?? '1') || 1);

  const { data, isLoading, isError, refetch } = useInboxList({
    unreadOnly: filter === 'unread',
    pageNumber,
    pageSize: INBOX_PAGE_SIZE,
  });
  const markRead = useMarkInboxItemRead();

  function goToPage(page: number) {
    const params = new URLSearchParams(searchParams.toString());
    if (page <= 1) params.delete('page');
    else params.set('page', String(page));
    const query = params.toString();
    router.push(query ? `${pathname}?${query}` : pathname);
  }

  return (
    <div className="flex flex-col gap-4">
      <h1 className="font-display text-2xl font-semibold text-ink">Inbox</h1>
      <InboxList
        items={data?.items}
        totalCount={data?.totalCount ?? 0}
        pageNumber={pageNumber}
        pageSize={INBOX_PAGE_SIZE}
        filter={filter}
        isLoading={isLoading}
        isError={isError}
        onRetry={() => refetch()}
        onFilterChange={(next) => setFilterParam('filter', next === 'unread' ? 'unread' : '')}
        onPageChange={goToPage}
        onOpenItem={(item) => markRead.mutate(item.id)}
      />
    </div>
  );
}

export default function InboxPage() {
  return (
    <Suspense fallback={<LoadingState variant="table" rows={5} label="Loading your messages…" />}>
      <InboxContent />
    </Suspense>
  );
}
