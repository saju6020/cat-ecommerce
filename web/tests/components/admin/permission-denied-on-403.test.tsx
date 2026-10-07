import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { useSettings } from '@/lib/hooks/useAdminSettings';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { LoadingState } from '@/components/common/LoadingState';
import { ApiForbiddenError } from '@/lib/api/client';

/** ARCH gate precedent (HO-ACC-002 `staff-account-detail.test.tsx`) — a stand-in for
 *  `app/(staff)/staff/admin/settings/page.tsx`'s own `error instanceof ApiForbiddenError` branch,
 *  reproduced here rather than importing the real Next.js page module into vitest. Every other
 *  admin list/detail page (`BranchesPage`, `ServicesPage`, `AuditLogPage`, …) follows the exact
 *  same `useX()` → `error instanceof ApiForbiddenError` → `<PermissionDeniedState />` shape, so one
 *  representative case proves the pattern rather than repeating it once per page. */
function SettingsPageStandIn() {
  const { isLoading, error } = useSettings();
  if (error instanceof ApiForbiddenError) return <PermissionDeniedState />;
  if (isLoading) return <LoadingState />;
  return <p>Settings loaded</p>;
}

function renderWithClient(children: React.ReactNode) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={queryClient}>{children}</QueryClientProvider>);
}

describe('U-A1 (second half): a server 403 renders PermissionDeniedState, not a UI-only block', () => {
  it('shows PermissionDeniedState — never mentions a role or action', async () => {
    server.use(
      http.get('*/api/admin/settings', () =>
        HttpResponse.json(
          { errors: [{ errorCode: 'ActionNotPermitted', errorMessage: 'Not permitted.' }] },
          { status: 403 },
        ),
      ),
    );
    renderWithClient(<SettingsPageStandIn />);
    expect(await screen.findByText(/you don.t have permission to do this/i)).toBeInTheDocument();
    expect(screen.queryByText(/ActionNotPermitted/)).not.toBeInTheDocument();
  });
});
