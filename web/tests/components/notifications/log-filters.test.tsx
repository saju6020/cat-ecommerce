import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import {
  resetNotificationsMockData,
  notificationFixtureIds as ids,
} from '@/mocks/handlers/notifications';
import NotificationLogPage from '@/app/(staff)/staff/notifications/page';

/**
 * U-E2 — the log page with its filters wired to the URL. `next/navigation` is mocked with a tiny
 * address bar: `push`/`replace` record every URL, `useSearchParams` reads the current one, and a
 * test can "press Back" by restoring an earlier URL and re-rendering.
 */
let currentSearch = '';
const history: string[] = [];
const push = vi.fn((url: string) => {
  history.push(url);
  currentSearch = url.includes('?') ? url.slice(url.indexOf('?') + 1) : '';
});
const replace = vi.fn((url: string) => {
  history[history.length - 1] = url;
  currentSearch = url.includes('?') ? url.slice(url.indexOf('?') + 1) : '';
});

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push, replace }),
  usePathname: () => '/staff/notifications',
  useSearchParams: () => new URLSearchParams(currentSearch),
}));

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  // A NEW element each time: React skips re-rendering an identical element, and the mocked
  // address bar is only re-read during a render.
  const make = () => (
    <QueryClientProvider client={client}>
      <NotificationLogPage />
    </QueryClientProvider>
  );
  const utils = render(make());
  return { ...utils, again: () => utils.rerender(make()) };
}

/** Records the query string of every log request. */
function recordLogRequests() {
  const urls: URL[] = [];
  server.use(
    http.get('*/api/admin/notifications', ({ request }) => {
      urls.push(new URL(request.url));
      return HttpResponse.json({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0 });
    }),
  );
  return urls;
}

beforeEach(() => {
  resetNotificationsMockData();
  currentSearch = '';
  history.length = 0;
  push.mockClear();
  replace.mockClear();
});

describe('Notification log page — HO-NTF-002 §11.5, UI-4', () => {
  it('defaults to the last 7 days, shows the summary and the log, and states the 93-day maximum', async () => {
    renderPage();
    expect(await screen.findByRole('heading', { level: 1, name: 'Messages' })).toBeInTheDocument();
    expect(await screen.findByRole('table', { name: 'Message log' })).toBeInTheDocument();
    expect(screen.getByText('Choose a date range of up to 93 days.')).toBeInTheDocument();
    const from = screen.getByLabelText('From') as HTMLInputElement;
    const to = screen.getByLabelText('To') as HTMLInputElement;
    const days = (new Date(to.value).getTime() - new Date(from.value).getTime()) / 86_400_000 + 1;
    expect(days).toBe(7);
    // The mock summary has SMS unconnected: the warning is on the page.
    expect(
      (await screen.findAllByText(/SMS is not connected to a provider. Text messages/)).length,
    ).toBeGreaterThan(0);
  });

  it('U-E2: filters round-trip through the URL params, and Back restores them', async () => {
    const urls = recordLogRequests();
    const user = userEvent.setup();
    const { again } = renderPage();
    await screen.findByLabelText('Channel');

    await user.selectOptions(screen.getByLabelText('Channel'), 'Sms');
    await user.selectOptions(screen.getByLabelText('Status'), 'Undeliverable');
    expect(push).toHaveBeenCalled();
    expect(currentSearch).toContain('channel=Sms');
    expect(currentSearch).toContain('status=Undeliverable');
    again();

    await waitFor(() => {
      const last = urls.at(-1)!;
      expect(last.searchParams.get('channel')).toBe('Sms');
      expect(last.searchParams.get('status')).toBe('Undeliverable');
    });

    // "Back": the address bar returns to the earlier URL (channel only).
    currentSearch = 'channel=Sms';
    again();
    await waitFor(() =>
      expect((screen.getByLabelText('Status') as HTMLSelectElement).value).toBe(''),
    );
    expect((screen.getByLabelText('Channel') as HTMLSelectElement).value).toBe('Sms');
  });

  it('U-E2: a deep link (?recipientId=…) filters by that customer and shows who it is', async () => {
    currentSearch = `recipientId=${ids.customerId}`;
    const urls = recordLogRequests();
    renderPage();
    await waitFor(() => expect(urls.length).toBeGreaterThan(0));
    expect(urls[0]!.searchParams.get('recipientId')).toBe(ids.customerId);
    // The customer's name comes from HO-ACC-002's customer read; without a session it falls back.
    expect(await screen.findByRole('button', { name: 'Clear customer' })).toBeInTheDocument();
  });

  it('U-E2: a range over 93 days is refused before any request, in words', async () => {
    currentSearch = 'from=2026-01-01&to=2026-06-30';
    const urls = recordLogRequests();
    renderPage();
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'The date range is 181 days — the log can show at most 93. Narrow it to see results.',
    );
    await new Promise((r) => setTimeout(r, 80));
    expect(urls).toHaveLength(0);
    expect(screen.queryByRole('table', { name: 'Message log' })).not.toBeInTheDocument();
  });

  it('U-E2: from/to are sent as day-boundary instants and every other filter reaches the API', async () => {
    currentSearch =
      'from=2026-10-01&to=2026-10-07&channel=Email&status=Sent&templateKey=care.vaccination-due';
    const urls = recordLogRequests();
    renderPage();
    fireEvent.change(await screen.findByLabelText('Destination (mobile number or email)'), {
      target: { value: 'a@b.com' },
    });
    await waitFor(() => expect(urls.at(-1)!.searchParams.get('destination')).toBe('a@b.com'));
    await waitFor(() => expect(urls.length).toBeGreaterThan(0));
    const q = urls.at(-1)!.searchParams;
    expect(q.get('from')).toBe('2026-10-01T00:00:00.000Z');
    expect(q.get('to')).toBe('2026-10-07T23:59:59.999Z');
    expect(q.get('channel')).toBe('Email');
    expect(q.get('status')).toBe('Sent');
    expect(q.get('templateKey')).toBe('care.vaccination-due');
    expect(q.get('pageSize')).toBe('25');
  });

  it('N10: the destination term is sent to the API but never written to the URL', async () => {
    const urls = recordLogRequests();
    renderPage();
    const input = await screen.findByLabelText('Destination (mobile number or email)');
    fireEvent.change(input, { target: { value: 'sam@example.com' } });
    fireEvent.blur(input);
    await waitFor(() =>
      expect(urls.at(-1)!.searchParams.get('destination')).toBe('sam@example.com'),
    );
    expect(input).toHaveValue('sam@example.com');
    expect(currentSearch).not.toContain('destination');
    expect(currentSearch).not.toContain('sam');
    expect(history.join(' ')).not.toContain('sam');
  });

  it('N2: clearing From shows a choose-a-date-range prompt, no table, and keeps from= in the URL', async () => {
    const urls = recordLogRequests();
    const { again } = renderPage();
    await screen.findByRole('table', { name: 'Message log' }).catch(() => undefined);
    const from = screen.getByLabelText('From');
    fireEvent.change(from, { target: { value: '' } });
    expect(await screen.findByText('Choose a date range')).toBeInTheDocument();
    expect(screen.queryByRole('table', { name: 'Message log' })).not.toBeInTheDocument();
    expect(new URLSearchParams(currentSearch).get('from')).toBe('');
    expect(currentSearch).toContain('from=');
    const before = urls.length;

    // A reload / deep link of that URL shows the same prompt, not the default range.
    again();
    expect((screen.getByLabelText('From') as HTMLInputElement).value).toBe('');
    expect(screen.getByText('Choose a date range')).toBeInTheDocument();
    await new Promise((r) => setTimeout(r, 80));
    expect(urls).toHaveLength(before);
  });

  it('N12: paging flushes a template-key edit still inside its debounce window', async () => {
    server.use(
      http.get('*/api/admin/notification-templates', () =>
        HttpResponse.json(
          { errors: [{ errorCode: 'ActionNotPermitted', errorMessage: 'no' }] },
          { status: 403 },
        ),
      ),
      http.get('*/api/admin/notifications', () =>
        HttpResponse.json({
          items: [
            {
              id: 'n-1',
              createdOn: '2026-10-04T05:00:00Z',
              templateKey: 'k',
              templateName: 'T',
              category: 'Transactional',
              channel: 'Sms',
              destinationMasked: null,
              recipientKind: 'Customer',
              recipientId: 'c-1',
              recipientName: 'Sam',
              status: 'Sent',
              attemptCount: 1,
              sentOn: null,
              lastProviderResponse: null,
              deferredUntil: null,
              isResend: false,
              isFallback: false,
            },
          ],
          pageNumber: 1,
          pageSize: 25,
          totalCount: 60,
        }),
      ),
    );
    const user = userEvent.setup();
    renderPage();
    const key = await screen.findByLabelText('Template key');
    const next = await screen.findByRole('button', { name: /Next/ });
    fireEvent.change(key, { target: { value: 'care.vaccination-due' } });
    await user.click(next); // well inside the 300 ms debounce
    const last = new URLSearchParams(currentSearch);
    expect(last.get('templateKey')).toBe('care.vaccination-due');
    expect(last.get('page')).toBe('2');
  });

  it('finds a customer by mobile number and stores the id in the URL', async () => {
    const user = userEvent.setup();
    renderPage();
    await user.type(
      await screen.findByLabelText('Customer (mobile number or email)'),
      '01799999999',
    );
    await user.click(screen.getByRole('button', { name: 'Find' }));
    server.use(
      http.get('*/api/staff/customers', () =>
        HttpResponse.json([
          {
            id: ids.customerId,
            fullName: 'Sam Rahman',
            mobileNumber: '01799999999',
            emailAddress: null,
            recordStatus: 'Complete',
            status: 'Active',
            mobileMatchedOn: 'Primary',
          },
        ]),
      ),
    );
    await user.click(screen.getByRole('button', { name: 'Find' }));
    await user.click(await screen.findByRole('button', { name: /Sam Rahman/ }));
    expect(currentSearch).toContain(`recipientId=${ids.customerId}`);
  });

  it('a server 403 renders PermissionDeniedState (E-E2)', async () => {
    server.use(
      http.get('*/api/admin/notifications', () =>
        HttpResponse.json(
          { errors: [{ errorCode: 'ActionNotPermitted', errorMessage: 'no' }] },
          { status: 403 },
        ),
      ),
      http.get('*/api/admin/notifications/summary', () =>
        HttpResponse.json(
          { errors: [{ errorCode: 'ActionNotPermitted', errorMessage: 'no' }] },
          { status: 403 },
        ),
      ),
    );
    renderPage();
    expect(await screen.findByText(/You don.t have permission to do this/)).toBeInTheDocument();
    expect(screen.queryByRole('table', { name: 'Message log' })).not.toBeInTheDocument();
  });

  it('the template filter is a select when templates can be listed, a plain key field when they cannot', async () => {
    const { unmount } = renderPage();
    const select = (await screen.findByLabelText('Template')) as HTMLSelectElement;
    await waitFor(() => expect(within(select).getAllByRole('option').length).toBeGreaterThan(1));
    unmount();

    server.use(
      http.get('*/api/admin/notification-templates', () =>
        HttpResponse.json(
          { errors: [{ errorCode: 'ActionNotPermitted', errorMessage: 'no' }] },
          { status: 403 },
        ),
      ),
    );
    renderPage();
    const input = await screen.findByLabelText('Template key');
    fireEvent.change(input, { target: { value: 'care.vaccination-due' } });
    expect(input).toHaveValue('care.vaccination-due');
  });
});
