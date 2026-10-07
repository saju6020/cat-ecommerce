import { describe, expect, it, beforeEach, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import AuditLogPage from '@/app/(staff)/staff/admin/audit-log/page';
import { defaultAuditRange } from '@/lib/api/admin-audit';

const push = vi.fn();
const replace = vi.fn();
let currentSearch = '';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push, replace }),
  usePathname: () => '/staff/admin/audit-log',
  useSearchParams: () => new URLSearchParams(currentSearch),
}));

let requests: URLSearchParams[] = [];

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <AuditLogPage />
    </QueryClientProvider>,
  );
}

describe('AuditLogPage query — ADM 31 / 32', () => {
  beforeEach(() => {
    push.mockClear();
    replace.mockClear();
    requests = [];
    server.use(
      http.get('*/api/admin/audit-entries', ({ request }) => {
        requests.push(new URL(request.url).searchParams);
        return HttpResponse.json({ items: [], pageNumber: 1, pageSize: 50, totalCount: 0 });
      }),
    );
  });

  it('ADM 32: a burst of keystrokes is one request, at page 1, not one per character with the stale URL page', async () => {
    currentSearch = 'page=3';
    renderPage();
    const input = await screen.findByLabelText('action');
    await waitFor(() => expect(requests.length).toBeGreaterThan(0));
    expect(requests[0]!.get('pageNumber')).toBe('3');
    requests = [];

    for (const value of ['a', 'ab', 'abc']) fireEvent.change(input, { target: { value } });

    await waitFor(() => expect(requests.some((r) => r.get('action') === 'abc')).toBe(true), {
      timeout: 2000,
    });
    expect(requests.every((r) => r.get('action') === 'abc')).toBe(true);
    expect(requests.every((r) => r.get('pageNumber') === '1')).toBe(true);
    expect(requests).toHaveLength(1);
  });

  it('ADM 31: clearing a date queries the default range, never from=/to=', async () => {
    currentSearch = 'from=2027-01-01&to=2027-01-05';
    renderPage();
    const from = await screen.findByLabelText('From');
    await waitFor(() => expect(requests.length).toBeGreaterThan(0));
    expect(requests[0]!.get('from')).toBe('2027-01-01');
    requests = [];

    fireEvent.change(from, { target: { value: '' } });

    const defaults = defaultAuditRange();
    await waitFor(() => expect(requests.length).toBeGreaterThan(0), { timeout: 2000 });
    expect(requests.at(-1)!.get('from')).toBe(defaults.from);
    expect(requests.at(-1)!.get('to')).toBe('2027-01-05');
    expect(screen.getByRole('link', { name: /export csv/i })).toHaveAttribute(
      'href',
      expect.stringContaining(`from=${defaults.from}`),
    );
  });
});
