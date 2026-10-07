import { Suspense } from 'react';
import { describe, expect, it } from 'vitest';
import { act, render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import PublicPageRenderer from '@/app/(auth)/pages/[slug]/page';

describe('public /pages/[slug] not-found — ADM 2', () => {
  it('a 404 renders "Page not found" as the page <h1>', async () => {
    server.use(
      http.get('*/api/content/page/missing', () =>
        HttpResponse.json({ errorCode: 'ContentNotFound' }, { status: 404 }),
      ),
    );
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const params = Promise.resolve({ slug: 'missing' });
    await act(async () => {
      render(
        <QueryClientProvider client={client}>
          <Suspense fallback={null}>
            <PublicPageRenderer params={params} />
          </Suspense>
        </QueryClientProvider>,
      );
    });
    expect(await screen.findByRole('heading', { level: 1, name: 'Page not found' })).toBeVisible();
  });
});
