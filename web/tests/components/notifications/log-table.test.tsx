import { describe, expect, it, vi } from 'vitest';
import userEvent from '@testing-library/user-event';
import { render, screen, within } from '@testing-library/react';
import { NotificationLogTable } from '@/components/notifications/NotificationLogTable';
import { describeStatus } from '@/components/notifications/statusText';
import type { NotificationLogItem, NotificationStatus } from '@/lib/api/notifications-log';

function row(over: Partial<NotificationLogItem> = {}): NotificationLogItem {
  return {
    id: 'n-1',
    createdOn: '2026-10-04T05:00:00Z',
    templateKey: 'care.vaccination-due',
    templateName: 'Vaccination due',
    category: 'Transactional',
    channel: 'Sms',
    destinationMasked: '+88017•••••678',
    recipientKind: 'Customer',
    recipientId: 'c-1',
    recipientName: 'Sam Rahman',
    status: 'Sent',
    attemptCount: 1,
    sentOn: null,
    lastProviderResponse: null,
    deferredUntil: null,
    isResend: false,
    isFallback: false,
    ...over,
  };
}

function renderTable(items: NotificationLogItem[], timeZoneId = 'Asia/Dhaka') {
  return render(
    <NotificationLogTable
      items={items}
      isLoading={false}
      isError={false}
      onRetry={() => {}}
      timeZoneId={timeZoneId}
      pageNumber={1}
      pageSize={25}
      totalCount={items.length}
      onPageChange={() => {}}
    />,
  );
}

describe('NotificationLogTable — HO-NTF-002 §11.5, UI-6', () => {
  it('U-E3: every status is shown as text', () => {
    const statuses: Array<[NotificationStatus, string]> = [
      ['Queued', 'Queued'],
      ['Sending', 'Sending'],
      ['Sent', 'Sent'],
      ['RetryPending', 'Retrying'],
      ['Undeliverable', 'Undeliverable'],
      ['Expired', 'Expired'],
      ['Cancelled', 'Cancelled'],
      ['InboxDelivered', 'Delivered to inbox'],
    ];
    renderTable(statuses.map(([status], i) => row({ id: `n-${i}`, status })));
    const table = screen.getByRole('table', { name: 'Message log' });
    for (const [, label] of statuses) {
      expect(within(table).getByText(label)).toBeInTheDocument();
    }
  });

  it('U-E3: a held row shows the release time in the BRANCH zone', () => {
    const future = new Date(Date.now() + 3 * 3600_000);
    future.setUTCHours(2, 0, 0, 0); // 02:00Z == 08:00 in Asia/Dhaka (+06:00)
    if (future.getTime() < Date.now()) future.setUTCDate(future.getUTCDate() + 1);
    const held = row({ status: 'Queued', deferredUntil: future.toISOString() });

    expect(describeStatus(held, 'Asia/Dhaka')).toBe('Held until 08:00');
    expect(describeStatus(held, 'Europe/London')).not.toBe('Held until 08:00');

    renderTable([held], 'Asia/Dhaka');
    const table = screen.getByRole('table', { name: 'Message log' });
    expect(within(table).getByText('Held until 08:00')).toBeInTheDocument();
    expect(within(table).getByText('(Asia/Dhaka)')).toBeInTheDocument();
  });

  it('a queued row whose hold has passed is just Queued', () => {
    const past = row({ status: 'Queued', deferredUntil: '2020-01-01T02:00:00Z' });
    expect(describeStatus(past, 'Asia/Dhaka')).toBe('Queued');
  });

  it('shows Resend and Fallback tags, masked destinations and a link to the detail', () => {
    renderTable([row({ isResend: true, isFallback: true })]);
    const table = screen.getByRole('table', { name: 'Message log' });
    expect(within(table).getByText('Resend')).toBeInTheDocument();
    expect(within(table).getByText('Fallback')).toBeInTheDocument();
    expect(within(table).getByText('+88017•••••678')).toBeInTheDocument();
    expect(within(table).getAllByRole('link')[0]).toHaveAttribute(
      'href',
      '/staff/notifications/n-1',
    );
  });

  it('renders a card list alongside the table (CSS shows one or the other at 768 px)', () => {
    const { container } = renderTable([row()]);
    const tableWrap = container.querySelector('[role="region"]')!;
    expect(tableWrap.className).toContain('hidden');
    expect(tableWrap.className).toContain('md:block');
    const cards = container.querySelector('ul')!;
    expect(cards.className).toContain('md:hidden');
    expect(within(cards).getByText('Vaccination due')).toBeInTheDocument();
    expect(within(cards).getByRole('link', { name: 'View details' })).toHaveAttribute(
      'href',
      '/staff/notifications/n-1',
    );
  });

  it('U-E5: a provider response containing markup is shown as literal text', () => {
    const { container } = renderTable([
      row({ lastProviderResponse: '550 <script>alert(1)</script> unavailable' }),
    ]);
    expect(screen.getAllByText('550 <script>alert(1)</script> unavailable').length).toBeGreaterThan(
      0,
    );
    expect(container.querySelector('script')).toBeNull();
  });

  it('has loading, error and empty states', () => {
    const base = {
      onRetry: () => {},
      timeZoneId: 'Asia/Dhaka',
      pageNumber: 1,
      pageSize: 25,
      totalCount: 0,
      onPageChange: () => {},
    };
    const { rerender } = render(
      <NotificationLogTable items={undefined} isLoading isError={false} {...base} />,
    );
    expect(screen.getByText('Loading messages…')).toBeInTheDocument();
    rerender(<NotificationLogTable items={undefined} isLoading={false} isError {...base} />);
    expect(screen.getByText("We couldn't load the message log.")).toBeInTheDocument();
    rerender(<NotificationLogTable items={[]} isLoading={false} isError={false} {...base} />);
    expect(screen.getByText('No messages match these filters')).toBeInTheDocument();
  });

  it('N13: shows an updating indicator while old rows are on screen, and an error with Retry if the refetch failed', async () => {
    const onRetry = vi.fn();
    const base = {
      onRetry,
      timeZoneId: 'Asia/Dhaka',
      pageNumber: 1,
      pageSize: 25,
      totalCount: 1,
      onPageChange: () => {},
    };
    const { rerender } = render(
      <NotificationLogTable items={[row()]} isLoading={false} isError={false} {...base} />,
    );
    expect(screen.queryByText('Updating…')).not.toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();

    rerender(
      <NotificationLogTable
        items={[row()]}
        isLoading={false}
        isError={false}
        isFetching
        {...base}
      />,
    );
    expect(screen.getByRole('status')).toHaveTextContent('Updating…');

    rerender(<NotificationLogTable items={[row()]} isLoading={false} isError {...base} />);
    expect(screen.getByRole('alert')).toHaveTextContent(/couldn.t refresh the message log/);
    expect(screen.getAllByText('Sam Rahman').length).toBeGreaterThan(0);
    await userEvent.click(screen.getByRole('button', { name: 'Retry' }));
    expect(onRetry).toHaveBeenCalledTimes(1);
  });
});
