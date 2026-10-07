import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider, focusManager } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { InboxBadge } from '@/components/inbox/InboxBadge';

function renderBadge() {
  const queryClient = new QueryClient();
  return render(
    <QueryClientProvider client={queryClient}>
      <InboxBadge />
    </QueryClientProvider>,
  );
}

function serveCount(count: number) {
  const calls = { n: 0 };
  server.use(
    http.get('*/api/customers/me/inbox/unread-count', () => {
      calls.n += 1;
      return HttpResponse.json({ count });
    }),
  );
  return calls;
}

describe('InboxBadge — HO-NTF-002 §11.1, ADR-FE-159', () => {
  afterEach(() => {
    vi.useRealTimers();
    Object.defineProperty(document, 'visibilityState', { value: 'visible', configurable: true });
    focusManager.setFocused(undefined);
  });

  it('U-A1: hides the count at 0, shows 3 with its accessible name, and caps at 99+', async () => {
    serveCount(0);
    const zero = renderBadge();
    const link = await screen.findByRole('link', { name: 'Inbox' });
    expect(link).toHaveAttribute('href', '/portal/inbox');
    await waitFor(() => expect(screen.queryByTestId('inbox-unread-count')).not.toBeInTheDocument());
    zero.unmount();

    serveCount(3);
    const three = renderBadge();
    expect(
      await screen.findByRole('link', { name: 'Inbox, 3 unread messages' }),
    ).toBeInTheDocument();
    expect(screen.getByTestId('inbox-unread-count')).toHaveTextContent('3');
    three.unmount();

    serveCount(1);
    const one = renderBadge();
    expect(
      await screen.findByRole('link', { name: 'Inbox, 1 unread message' }),
    ).toBeInTheDocument();
    one.unmount();

    serveCount(150);
    renderBadge();
    expect(
      await screen.findByRole('link', { name: 'Inbox, more than 99 unread messages' }),
    ).toBeInTheDocument();
    expect(screen.getByTestId('inbox-unread-count')).toHaveTextContent('99+');
  });

  it('N15: count changes are announced through a polite live region', async () => {
    serveCount(0);
    const queryClient = new QueryClient();
    render(
      <QueryClientProvider client={queryClient}>
        <InboxBadge />
      </QueryClientProvider>,
    );
    const region = await screen.findByRole('status');
    expect(region).toHaveAttribute('aria-live', 'polite');
    await waitFor(() => expect(region).toHaveTextContent('No unread messages'));

    act(() => queryClient.setQueryData(['inbox', 'unread-count'], { count: 4 }));
    await waitFor(() => expect(region).toHaveTextContent('4 unread messages'));
    act(() => queryClient.setQueryData(['inbox', 'unread-count'], { count: 1 }));
    await waitFor(() => expect(region).toHaveTextContent('1 unread message'));
  });

  it('U-A2: refetches every 60 s and on focus, and not while the document is hidden', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const calls = serveCount(2);
    renderBadge();
    await waitFor(() => expect(calls.n).toBe(1));

    // 60 s later: one more poll.
    await act(async () => {
      await vi.advanceTimersByTimeAsync(60_000);
    });
    await waitFor(() => expect(calls.n).toBe(2));

    // Window focus regained: a refetch (staleTime 0).
    await act(async () => {
      focusManager.setFocused(false);
      focusManager.setFocused(true);
    });
    await waitFor(() => expect(calls.n).toBe(3));

    // Tab hidden: five minutes pass and nothing is requested. (Hand control back to the real
    // document visibility, which `setFocused(true)` above had overridden.)
    Object.defineProperty(document, 'visibilityState', { value: 'hidden', configurable: true });
    await act(async () => {
      focusManager.setFocused(undefined);
      await vi.advanceTimersByTimeAsync(5 * 60_000);
    });
    expect(calls.n).toBe(3);
  });
});

beforeEach(() => {
  focusManager.setFocused(undefined);
});
