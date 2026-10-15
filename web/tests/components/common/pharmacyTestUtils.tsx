import * as React from 'react';
import { render } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { vi } from 'vitest';

/**
 * Shared helpers for the HO-PHM-002 component tests. `next/navigation` is stubbed by each test file
 * (`vi.mock('next/navigation', …)` reading `navigation` below) — the components use `useRouter`,
 * `usePathname` and `useSearchParams`. Every render gets its own `QueryClient`, so nothing leaks
 * between cases.
 */
export function renderWithClient(ui: React.ReactElement) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } },
  });
  const result = render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>);
  return { queryClient, ...result };
}

export const navigation = {
  push: vi.fn(),
  replace: vi.fn(),
  back: vi.fn(),
  pathname: '/staff/pharmacy',
  search: new URLSearchParams(),
};

export function resetNavigation(): void {
  navigation.push.mockReset();
  navigation.replace.mockReset();
  navigation.back.mockReset();
  navigation.pathname = '/staff/pharmacy';
  navigation.search = new URLSearchParams();
}

/** The `vi.mock('next/navigation')` factory every pharmacy test file uses. */
export async function navigationMock() {
  return {
    useRouter: () => ({
      push: navigation.push,
      replace: navigation.replace,
      back: navigation.back,
      refresh: vi.fn(),
    }),
    usePathname: () => navigation.pathname,
    useSearchParams: () => navigation.search,
  };
}

/**
 * A route `params` Promise that React's `use()` reads synchronously (a pre-resolved thenable) — the
 * pages are `use(params)` components, and a plain `Promise.resolve` would suspend them forever under
 * Testing Library.
 */
export function resolvedParams<T>(value: T): Promise<T> {
  return Object.assign(Promise.resolve(value), { status: 'fulfilled', value }) as Promise<T>;
}
