import { render } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { resetPortalMock } from '@/mocks/handlers/portal';
import type { PortalDashboard } from '@/lib/api/portal';

/** Shared helpers for the portal component tests (HO-POR-002 U-series). */
export function newClient() {
  return new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
}

export function renderWithClient(ui: React.ReactElement, client = newClient()) {
  return { client, ...render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>) };
}

export function serveDashboard(body: PortalDashboard | (() => PortalDashboard)) {
  const calls = { n: 0 };
  server.use(
    http.get('*/api/portal/dashboard', () => {
      calls.n += 1;
      return HttpResponse.json(typeof body === 'function' ? body() : body);
    }),
  );
  return calls;
}

export { resetPortalMock };
