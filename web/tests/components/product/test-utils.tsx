import * as React from 'react';
import { render } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { detailView, findBySlug } from '@/mocks/handlers/shop/store';
import type { ProductDetail } from '@/lib/api/shop-catalogue';

export function renderWithClient(ui: React.ReactElement) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: 0 }, mutations: { retry: false } },
  });
  // A wrapper (not a wrapped element) so `rerender(ui)` keeps the same client.
  return render(ui, {
    wrapper: ({ children }) => (
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    ),
  });
}

/** A realistic `ProductDetail`, produced by the same view builder the shop mock serves. */
export function mockDetail(slug: string, locale?: string): ProductDetail {
  const product = findBySlug(slug);
  if (!product) throw new Error(`No mock product ${slug}`);
  return detailView(product, locale) as unknown as ProductDetail;
}

/** Signed-in customer: a `pc_session` cookie (what the shop mock checks) and a session answer. */
export function signInForTest(): void {
  document.cookie = 'pc_session=test-session; path=/';
  server.use(
    http.get('*/api/auth/session', () =>
      HttpResponse.json({
        customerId: 'c0000000-0000-0000-0000-000000000001',
        fullName: 'Test Customer',
        expiresOn: '2030-01-01T00:00:00Z',
        recordStatus: 'Complete',
      }),
    ),
  );
}

export function signOutForTest(): void {
  document.cookie = 'pc_session=; path=/; max-age=0';
}
