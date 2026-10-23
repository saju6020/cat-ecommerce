import { readdirSync, readFileSync, statSync } from 'node:fs';
import path from 'node:path';
import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { SignOutButton } from '@/components/auth/SignOutButton';
import { PORTAL_DASHBOARD_QUERY_KEY } from '@/lib/hooks/usePortalDashboard';
import {
  DEFAULT_CUSTOMER_LANDING,
  PORTAL_HOME,
  bookVaccinationHref,
  portalCatHref,
  portalCatVisitsHref,
  portalInvoiceHref,
  portalOrderHref,
} from '@/lib/routing/portal-links';
import { portalNav } from '@/nav/areas/portal';
import { newClient, renderWithClient } from './helpers';

const replace = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ replace }),
  usePathname: () => '/portal',
}));

describe('portal-links (ADR-FE-121)', () => {
  it('U-L1: builders return the documented paths and the landing constant is /portal', () => {
    expect(DEFAULT_CUSTOMER_LANDING).toBe('/portal/orders'); // owner, 2026-10-17
    expect(PORTAL_HOME).toBe('/portal');
    expect(portalCatHref('x')).toBe('/portal/cats/x');
    expect(portalCatVisitsHref('x')).toBe('/portal/cats/x/visits');
    expect(portalInvoiceHref('i')).toBe('/portal/billing/invoices/i');
    expect(portalOrderHref('o')).toBe('/portal/orders/o');
    expect(bookVaccinationHref('c 1')).toBe('/book?catId=c+1&category=Vaccination');
  });

  it('the portal nav registers Home -> /portal for the portal audience', () => {
    expect(portalNav.items).toContainEqual(
      expect.objectContaining({ label: 'Home', href: '/portal', audience: 'portal' }),
    );
  });

  it('no portal component writes a path of its own — links come from portal-links (U-A10)', () => {
    const root = path.resolve(import.meta.dirname, '../../../components/portal');
    const offenders: string[] = [];
    const walk = (dir: string) => {
      for (const entry of readdirSync(dir)) {
        const full = path.join(dir, entry);
        if (statSync(full).isDirectory()) walk(full);
        else if (/\.tsx?$/.test(entry) && !full.endsWith('CustomerSessionGuard.tsx')) {
          const source = readFileSync(full, 'utf8');
          if (/href=["'{]*[`'"]\/(portal|book|cart|shop)/.test(source)) offenders.push(entry);
        }
      }
    };
    walk(path.join(root, 'dashboard'));
    walk(path.join(root, 'reorder'));
    expect(offenders).toEqual([]);
  });
});

describe('source guards', () => {
  it('U-X4: nothing under app/, components/ or lib/ imports @/mocks (HO-CAT-002 §5.1)', () => {
    const web = path.resolve(import.meta.dirname, '../../..');
    const offenders: string[] = [];
    const walk = (dir: string) => {
      for (const entry of readdirSync(dir)) {
        if (entry === 'node_modules' || entry === '.next') continue;
        const full = path.join(dir, entry);
        if (statSync(full).isDirectory()) walk(full);
        else if (/\.tsx?$/.test(entry)) {
          if (/from ['"]@\/mocks|from ['"].*\/mocks\//.test(readFileSync(full, 'utf8'))) {
            offenders.push(path.relative(web, full));
          }
        }
      }
    };
    // `app/providers.tsx` is the one dynamic-import seam HO-CAT-002 owns; the portal files are ours.
    for (const dir of ['components/portal', 'lib/api', 'lib/hooks', 'lib/routing']) {
      walk(path.join(web, dir));
    }
    expect(offenders).toEqual([]);
  });
});

describe('sign-out clears the dashboard (U-X2)', () => {
  it('the whole cache goes, so the previous customer’s dashboard is not left in memory', async () => {
    server.use(http.post('*/api/auth/sign-out', () => new HttpResponse(null, { status: 204 })));
    const client = newClient();
    client.setQueryData(PORTAL_DASHBOARD_QUERY_KEY, {
      generatedOn: 'x',
      customer: { state: 'Ready' },
    });

    renderWithClient(<SignOutButton audience="customer" />, client);
    await userEvent.setup().click(screen.getByRole('button', { name: /sign out/i }));

    await waitFor(() => expect(replace).toHaveBeenCalledWith('/sign-in'));
    expect(client.getQueryData(PORTAL_DASHBOARD_QUERY_KEY)).toBeUndefined();
  });
});
