import type { ReactNode } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { administrationFixtureIds } from '@/mocks/handlers/admin';
import { useCancelBranchClosure, useCreateBranchClosure } from '@/lib/hooks/useAdminBranches';

const { mainBranchId } = administrationFixtureIds;

function setup() {
  const queryClient = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
  const spy = vi.spyOn(queryClient, 'invalidateQueries');
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
  return { spy, wrapper };
}

/** U-CAL12 — HO-ADM-002-FU1 §12.1/F10: a closure change must also refresh the calendar. */
describe('U-CAL12 — closure mutations invalidate the calendar', () => {
  it('useCreateBranchClosure invalidates closures and calendar', async () => {
    const { spy, wrapper } = setup();
    const { result } = renderHook(() => useCreateBranchClosure(mainBranchId), { wrapper });
    result.current.mutate({
      startsOn: '2029-05-02',
      endsOn: '2029-05-02',
      kind: 'Closure',
      reason: 'U-CAL12',
      partialFrom: null,
      partialTo: null,
    });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    const keys = spy.mock.calls.map((c) => c[0]?.queryKey);
    expect(keys).toContainEqual(['admin', 'branches', mainBranchId, 'closures']);
    expect(keys).toContainEqual(['admin', 'branches', mainBranchId, 'calendar']);
  });

  it('useCancelBranchClosure invalidates closures and calendar', async () => {
    const { spy, wrapper } = setup();
    const { result } = renderHook(() => useCancelBranchClosure(mainBranchId), { wrapper });
    result.current.mutate(administrationFixtureIds.closureHalfDayId);
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    const keys = spy.mock.calls.map((c) => c[0]?.queryKey);
    expect(keys).toContainEqual(['admin', 'branches', mainBranchId, 'closures']);
    expect(keys).toContainEqual(['admin', 'branches', mainBranchId, 'calendar']);
  });
});
