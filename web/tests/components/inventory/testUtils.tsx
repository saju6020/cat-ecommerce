import * as React from 'react';
import { render } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { vi } from 'vitest';

/**
 * Shared helpers for the HO-IMP-002 component tests. `next/navigation` is stubbed by each test file
 * with `mockNavigation()` (hoisted by vitest) — components here use `useRouter`/`usePathname`/
 * `useSearchParams`. Every render gets its own `QueryClient`, so nothing leaks between cases.
 */
export function renderWithClient(ui: React.ReactElement) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
  const result = render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>);
  return { queryClient, ...result };
}

export const navigation = {
  push: vi.fn(),
  replace: vi.fn(),
  pathname: '/staff/inventory',
  search: new URLSearchParams(),
};

export function resetNavigation(): void {
  navigation.push.mockReset();
  navigation.replace.mockReset();
  navigation.pathname = '/staff/inventory';
  navigation.search = new URLSearchParams();
}
