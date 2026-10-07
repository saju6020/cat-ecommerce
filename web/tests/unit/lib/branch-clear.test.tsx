import { describe, expect, it } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import type { ReactNode } from 'react';
import { server } from '@/mocks/server';
import {
  clearQueryCacheKeepingBranchFor,
  forgetPreservedBranch,
  getActiveBranchId,
  registerQueryClient,
  restorePreservedBranch,
} from '@/lib/branch';
import { apiFetch } from '@/lib/api/client';
import { useMyBranches } from '@/lib/hooks/useActiveBranch';

describe('branch preservation across a lapsed session — ADM 12', () => {
  async function branchHeaderOfNextRequest(): Promise<string | null> {
    let seen: string | null = 'unset';
    server.use(
      http.get('*/api/echo-branch', ({ request }) => {
        seen = request.headers.get('X-Branch-Id');
        return HttpResponse.json({});
      }),
    );
    await apiFetch('/api/echo-branch');
    return seen;
  }

  it('the same person signing back in after a lapsed session keeps their branch', async () => {
    const client = new QueryClient();
    registerQueryClient(client);
    client.setQueryData(['staff', 'active-branch'], 'branch-b');
    clearQueryCacheKeepingBranchFor(client, 'staff-a');
    expect(getActiveBranchId()).toBeNull(); // nothing stale is sent while signed out
    restorePreservedBranch(client, 'staff-a');
    expect(await branchHeaderOfNextRequest()).toBe('branch-b');
  });

  it("another person's sign-in sends no X-Branch-Id on their first request", async () => {
    const client = new QueryClient();
    registerQueryClient(client);
    client.setQueryData(['staff', 'active-branch'], 'branch-a-only');
    clearQueryCacheKeepingBranchFor(client, 'staff-a');
    restorePreservedBranch(client, 'staff-b');
    expect(await branchHeaderOfNextRequest()).toBeNull();
    // and it was forgotten: A cannot get it back later either
    restorePreservedBranch(client, 'staff-a');
    expect(getActiveBranchId()).toBeNull();
  });

  it('an explicit sign-out forgets the branch even for the same person', () => {
    const client = new QueryClient();
    registerQueryClient(client);
    client.setQueryData(['staff', 'active-branch'], 'branch-b');
    clearQueryCacheKeepingBranchFor(client, 'staff-a');
    forgetPreservedBranch(client);
    restorePreservedBranch(client, 'staff-a');
    expect(getActiveBranchId()).toBeNull();
  });

  it('useMyBranches replaces a preserved branch the signed-in person is not assigned to', async () => {
    server.use(
      http.get('*/api/staff/branches/mine', () =>
        HttpResponse.json({
          branchIds: ['branch-a'],
          primaryBranchId: 'branch-a',
          activeBranchId: null,
          activeBranchRefusal: null,
        }),
      ),
    );
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    client.setQueryData(['staff', 'active-branch'], 'branch-of-previous-person');
    const wrapper = ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    );
    renderHook(() => useMyBranches(), { wrapper });
    await waitFor(() => expect(client.getQueryData(['staff', 'active-branch'])).toBe('branch-a'));
  });

  it('useMyBranches keeps a preserved branch the person is assigned to', async () => {
    server.use(
      http.get('*/api/staff/branches/mine', () =>
        HttpResponse.json({
          branchIds: ['branch-a', 'branch-b'],
          primaryBranchId: 'branch-a',
          activeBranchId: null,
          activeBranchRefusal: null,
        }),
      ),
    );
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    client.setQueryData(['staff', 'active-branch'], 'branch-b');
    const wrapper = ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    );
    const { result } = renderHook(() => useMyBranches(), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(client.getQueryData(['staff', 'active-branch'])).toBe('branch-b');
  });
});
