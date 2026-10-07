import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { resetNotificationsMockData, notificationFixtureIds } from '@/mocks/handlers/notifications';
import { InboxItem } from '@/components/inbox/InboxItem';
import { InboxList } from '@/components/inbox/InboxList';
import { InboxBadge } from '@/components/inbox/InboxBadge';
import { useInboxList, useMarkInboxItemRead } from '@/lib/hooks/useInbox';
import type { InboxItem as InboxItemModel } from '@/lib/api/inbox';

function item(over: Partial<InboxItemModel> = {}): InboxItemModel {
  return {
    id: 'i-1',
    title: 'A title',
    body: 'First line\nSecond line',
    linkPath: null,
    category: 'Transactional',
    createdOn: new Date().toISOString(),
    readOn: null,
    ...over,
  };
}

/** Stand-in for app/(portal)/portal/inbox/page.tsx — real hooks, real list, real badge. */
function InboxWithBadge() {
  const { data, isLoading, isError, refetch } = useInboxList({ pageSize: 20 });
  const markRead = useMarkInboxItemRead();
  return (
    <>
      <InboxBadge />
      <InboxList
        items={data?.items}
        totalCount={data?.totalCount ?? 0}
        pageNumber={1}
        pageSize={20}
        filter="all"
        isLoading={isLoading}
        isError={isError}
        onRetry={() => refetch()}
        onFilterChange={() => {}}
        onPageChange={() => {}}
        onOpenItem={(i) => markRead.mutate(i.id)}
      />
    </>
  );
}

describe('InboxItem / InboxList — HO-NTF-002 §11.1, ADR-FE-162', () => {
  beforeEach(() => resetNotificationsMockData());

  it('U-A3: opening an unread item POSTs read once and the badge count is invalidated', async () => {
    const reads: string[] = [];
    // The count is served from a variable the read handler lowers, as the real API would.
    let unread = 3;
    server.use(
      http.get('*/api/customers/me/inbox/unread-count', () => HttpResponse.json({ count: unread })),
      http.post('*/api/customers/me/inbox/:id/read', ({ params }) => {
        reads.push(String(params.id));
        unread = 2;
        return new HttpResponse(null, { status: 204 });
      }),
    );

    const user = userEvent.setup();
    render(
      <QueryClientProvider
        client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}
      >
        <InboxWithBadge />
      </QueryClientProvider>,
    );

    expect(
      await screen.findByRole('link', { name: 'Inbox, 3 unread messages' }),
    ).toBeInTheDocument();
    const header = await screen.findByRole('button', { name: /Momo is due a vaccination/i });
    await user.click(header); // open
    await user.click(header); // close
    await user.click(header); // open again — still only one POST

    await waitFor(() => expect(reads).toHaveLength(1));
    expect(reads[0]).toBe(notificationFixtureIds.inboxLinkedUnreadId);
    expect(
      await screen.findByRole('link', { name: 'Inbox, 2 unread messages' }),
    ).toBeInTheDocument();
  });

  it('opening an already-read item sends nothing', async () => {
    const onOpen = vi.fn();
    const user = userEvent.setup();
    render(
      <ul>
        <InboxItem item={item({ readOn: new Date().toISOString() })} onOpen={onOpen} />
      </ul>,
    );
    await user.click(screen.getByRole('button', { name: /A title/ }));
    expect(onOpen).not.toHaveBeenCalled();
  });

  it('U-A4: markup and Markdown in title and body are shown as literal text', async () => {
    const user = userEvent.setup();
    const { container } = render(
      <ul>
        <InboxItem
          item={item({ title: '<i>T</i>', body: '<b>hi</b> and **x**' })}
          onOpen={() => {}}
        />
      </ul>,
    );
    await user.click(screen.getByRole('button', { name: /<i>T<\/i>/ }));
    expect(within(container).getAllByText('<b>hi</b> and **x**').length).toBeGreaterThan(0);
    expect(container.querySelector('b')).toBeNull();
    expect(container.querySelector('i')).toBeNull();
    expect(container.querySelector('strong')).toBeNull();
  });

  it.each([
    ['/portal/cats/1', true],
    ['//evil.example', false],
    ['https://evil.example', false],
    ['/\\evil', false],
  ])('U-A5: linkPath %s -> link present: %s', async (linkPath, expected) => {
    const user = userEvent.setup();
    render(
      <ul>
        <InboxItem item={item({ linkPath })} onOpen={() => {}} />
      </ul>,
    );
    await user.click(screen.getByRole('button', { name: /A title/ }));
    const view = screen.queryByRole('link', { name: 'View' });
    if (expected) {
      expect(view).toHaveAttribute('href', linkPath);
    } else {
      expect(view).not.toBeInTheDocument();
    }
  });

  it('shows unread as text, and the warm empty state when there are no messages', () => {
    const { rerender } = render(
      <ul>
        <InboxItem item={item()} onOpen={() => {}} />
      </ul>,
    );
    expect(screen.getByText('Unread')).toBeInTheDocument();

    rerender(
      <InboxList
        items={[]}
        totalCount={0}
        pageNumber={1}
        pageSize={20}
        filter="all"
        isLoading={false}
        isError={false}
        onRetry={() => {}}
        onFilterChange={() => {}}
        onPageChange={() => {}}
        onOpenItem={() => {}}
      />,
    );
    expect(screen.getByText('No messages yet.')).toBeInTheDocument();
    expect(
      screen.getByText('Reminders and updates about your cats will appear here.'),
    ).toBeInTheDocument();
  });

  it('shows a retryable error state when the list cannot load', async () => {
    const onRetry = vi.fn();
    const user = userEvent.setup();
    render(
      <InboxList
        items={undefined}
        totalCount={0}
        pageNumber={1}
        pageSize={20}
        filter="all"
        isLoading={false}
        isError
        onRetry={onRetry}
        onFilterChange={() => {}}
        onPageChange={() => {}}
        onOpenItem={() => {}}
      />,
    );
    await user.click(screen.getByRole('button', { name: 'Try again' }));
    expect(onRetry).toHaveBeenCalled();
  });
});
