import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { StaffShell, canSeeNavItem } from '@/components/common/staff-shell';
import { REPORT_ROLES } from '@/nav/areas/reporting';

vi.mock('next/navigation', () => ({
  usePathname: () => '/staff',
  useRouter: () => ({ push: () => {}, replace: () => {} }),
}));

function asRoles(roleIds: string[]) {
  server.use(
    http.get('*/api/staff/session', () =>
      HttpResponse.json({
        staffAccountId: 's1',
        fullName: 'Test Person',
        roleIds,
        issuedOn: '2026-10-14T00:00:00Z',
        lastActivityOn: '2026-10-14T00:00:00Z',
        idleExpiresOn: '2026-10-14T01:00:00Z',
        absoluteExpiresOn: '2026-10-14T08:00:00Z',
        idleTimeoutMinutes: 60,
        serverTime: '2026-10-14T00:00:00Z',
      }),
    ),
  );
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={client}>
      <StaffShell>content</StaffShell>
    </QueryClientProvider>,
  );
}

describe('NavItem roles — Reports entry (owner 2026-10-14)', () => {
  it.each([['Vet'], ['Accountant'], ['Administrator'], ['Reception']])(
    'shows Reports to %s',
    async (role) => {
      asRoles([role]);
      expect((await screen.findAllByRole('link', { name: 'Reports' })).length).toBeGreaterThan(0);
    },
  );

  it('hides Reports from a Nurse, who still has Home', async () => {
    asRoles(['Nurse']);
    expect((await screen.findAllByRole('link', { name: 'Home' })).length).toBeGreaterThan(0);
    await screen.findByText(/Test Person/);
    expect(screen.queryByRole('link', { name: 'Reports' })).not.toBeInTheDocument();
  });

  it('copies the roles of Reports.Any and not the Nurse', () => {
    expect([...REPORT_ROLES].sort()).toEqual(
      [
        'Accountant',
        'Administrator',
        'Caretaker',
        'Inventory',
        'Reception',
        'ShopManager',
        'Vet',
      ].sort(),
    );
  });

  it('an item with no roles is for everyone; one with roles needs a match', () => {
    expect(canSeeNavItem({}, [])).toBe(true);
    expect(canSeeNavItem({ roles: ['Vet'] }, [])).toBe(false);
    expect(canSeeNavItem({ roles: ['Vet'] }, ['Nurse', 'Vet'])).toBe(true);
  });
});
