import * as React from 'react';
import { Suspense } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { act, render, screen } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import ProductEditorLayout from '@/app/(staff)/staff/shop/products/[productId]/layout';
import CatFoodPage from '@/app/(portal)/portal/cats/[catId]/food/page';

class NotFoundSignal extends Error {}
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
  usePathname: () => '/x',
  useSearchParams: () => new URLSearchParams(),
  notFound: () => {
    throw new NotFoundSignal('NEXT_NOT_FOUND');
  },
}));

class Boundary extends React.Component<{ children: React.ReactNode }, { missing: boolean }> {
  state = { missing: false };
  static getDerivedStateFromError(error: unknown) {
    if (error instanceof NotFoundSignal) return { missing: true };
    throw error;
  }
  render() {
    return this.state.missing ? <p>not-found-page</p> : this.props.children;
  }
}

async function renderBoundary(ui: React.ReactElement) {
  vi.spyOn(console, 'error').mockImplementation(() => {});
  await act(async () => {
    render(
      <Boundary>
        <Suspense fallback={null}>{ui}</Suspense>
      </Boundary>,
    );
  });
}

describe('non-GUID route ids are a 404 and send no request — S10', () => {
  it('the staff product editor', async () => {
    const calls: string[] = [];
    server.use(
      http.get('*/api/staff/shop/products/*', ({ request }) => {
        calls.push(request.url);
        return HttpResponse.json({});
      }),
    );
    await renderBoundary(
      <ProductEditorLayout params={Promise.resolve({ productId: '..%2Fadmin' })}>
        <p>child</p>
      </ProductEditorLayout>,
    );
    expect(await screen.findByText('not-found-page')).toBeInTheDocument();
    expect(calls).toEqual([]);
  });

  it("a customer's cat food page", async () => {
    const calls: string[] = [];
    server.use(
      http.get('*/api/customers/me/cats/*', ({ request }) => {
        calls.push(request.url);
        return HttpResponse.json({});
      }),
    );
    await renderBoundary(<CatFoodPage params={Promise.resolve({ catId: 'not-a-guid' })} />);
    expect(await screen.findByText('not-found-page')).toBeInTheDocument();
    expect(calls).toEqual([]);
  });
});
