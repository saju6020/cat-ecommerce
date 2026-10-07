import { describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useAllVaccines } from '@/lib/hooks/useVaccines';

// CODE_REVIEW gate MEDIUM (2026-09-26): getVaccines now reports a real totalCount (owner
// decision, HO-CAT-001 §1 Amended) instead of a hasNextPage flag — a totalCount that needs more
// than 100 pages of 100 forces the ceiling deterministically, with no real network round trips.
vi.mock('@/lib/api/vaccines', () => ({
  getVaccines: vi
    .fn()
    .mockResolvedValue({ items: [], pageNumber: 1, pageSize: 100, totalCount: 10_100 }),
}));

function wrapper({ children }: { children: React.ReactNode }) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
}

/**
 * ARCHITECTURE gate LOW (carried since the original pass) / TEST gate gap (8th pass) —
 * `useAllVaccines`'s page ceiling (`MAX_VACCINE_CATALOGUE_PAGES = 100`) had no test proving it
 * actually throws once reached, rather than looping forever. This mocks `getVaccines` directly to
 * report a `totalCount` (10,100) that needs 101 pages of 100, one past the ceiling, and asserts
 * the query surfaces a clear, ceiling-naming error rather than hanging.
 */
describe('useAllVaccines — page ceiling', () => {
  it('throws once MAX_VACCINE_CATALOGUE_PAGES is exceeded, rather than looping forever', async () => {
    const { result } = renderHook(() => useAllVaccines(), { wrapper });

    await waitFor(() => expect(result.current.isError).toBe(true), { timeout: 10_000 });

    expect((result.current.error as Error).message).toMatch(
      /the catalogue reports 10100 vaccines, which needs 101 pages/,
    );
  });
});
