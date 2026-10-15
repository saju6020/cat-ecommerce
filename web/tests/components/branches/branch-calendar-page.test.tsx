import { Suspense } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { administrationFixtureIds } from '@/mocks/handlers/admin';
import BranchCalendarPage from '@/app/(staff)/staff/admin/branches/[branchId]/calendar/page';

const push = vi.fn();
let search = 'month=2027-02';
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push, replace: vi.fn() }),
  usePathname: () => '/staff/admin/branches/x/calendar',
  useSearchParams: () => new URLSearchParams(search),
}));

const { mainBranchId } = administrationFixtureIds;

/**
 * U-CAL11 — HO-ADM-002-FU1 §11.3 page states. Renders the real page module against the MSW
 * handlers, overriding one endpoint per case.
 */
const FORBIDDEN = {
  errors: [{ errorCode: 'ActionNotPermitted', errorMessage: 'Not permitted.' }],
};

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  // `use()` reads a thenable already marked fulfilled synchronously (see detail-pages-403.test).
  const params = Object.assign(Promise.resolve({ branchId: mainBranchId }), {
    status: 'fulfilled',
    value: { branchId: mainBranchId },
  }) as Promise<{ branchId: string }>;
  return render(
    <QueryClientProvider client={queryClient}>
      <Suspense fallback={<p>suspended</p>}>
        <BranchCalendarPage params={params} />
      </Suspense>
    </QueryClientProvider>,
  );
}

const WAIT = { timeout: 5000 };

describe('Calendar page states — U-CAL11', () => {
  it('ready: a month grid in the branch zone, with the half-day closure reduced to the morning', async () => {
    search = 'month=2027-02';
    renderPage();
    expect(
      await screen.findByRole(
        'gridcell',
        {
          name: /Monday 15 February 2027\. Reduced hours: 09:00–13:00\. Closed 13:00–18:00: Staff training \(half day\)/,
        },
        WAIT,
      ),
    ).toBeInTheDocument();
    expect(screen.getByText(/Times in Asia\/Dhaka\./)).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Calendar' })).toBeInTheDocument();
  });

  it('an invalid month param falls back to the current month in the branch zone', async () => {
    search = 'month=2027-13';
    renderPage();
    const heading = await screen.findByRole('heading', { level: 3 }, WAIT);
    expect(heading.textContent).toMatch(/^[A-Z][a-z]+ \d{4}$/);
    expect(heading.textContent).not.toContain('2027');
  });

  it('Next month links to ?month= with the month shifted', async () => {
    search = 'month=2027-12';
    renderPage();
    expect(await screen.findByRole('link', { name: 'Next month' }, WAIT)).toHaveAttribute(
      'href',
      `/staff/admin/branches/${mainBranchId}/calendar?month=2028-01`,
    );
  });

  it('PageDown pushes the next month into the URL', async () => {
    search = 'month=2027-02';
    push.mockClear();
    renderPage();
    const cell = await screen.findByRole('gridcell', { name: /Monday 1 February 2027/ }, WAIT);
    cell.focus();
    await userEvent.setup().keyboard('{PageDown}');
    expect(push).toHaveBeenCalledWith(
      `/staff/admin/branches/${mainBranchId}/calendar?month=2027-03`,
    );
  });

  it('a 403 on the calendar shows the permission refusal', async () => {
    search = 'month=2027-02';
    server.use(
      http.get('*/api/admin/branches/:branchId/calendar', () =>
        HttpResponse.json(FORBIDDEN, { status: 403 }),
      ),
    );
    renderPage();
    expect(
      await screen.findByText(/you don.t have permission to do this/i, {}, WAIT),
    ).toBeInTheDocument();
    expect(screen.queryByRole('gridcell')).not.toBeInTheDocument();
  });

  it('while the calendar is pending it shows the loading state', async () => {
    search = 'month=2027-02';
    server.use(http.get('*/api/admin/branches/:branchId/calendar', () => new Promise(() => {})));
    renderPage();
    expect(await screen.findByText('Loading calendar…', {}, WAIT)).toBeInTheDocument();
    expect(screen.queryByRole('gridcell')).not.toBeInTheDocument();
  });

  it('a calendar failure shows the plain error with Retry, no grid, no codes', async () => {
    search = 'month=2027-02';
    server.use(
      http.get('*/api/admin/branches/:branchId/calendar', () =>
        HttpResponse.json({ errors: [{ errorCode: 'Boom' }] }, { status: 500 }),
      ),
    );
    const { container } = renderPage();
    expect(
      await screen.findByText("We couldn't load the calendar just now.", {}, WAIT),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Retry' })).toBeInTheDocument();
    expect(screen.queryByRole('gridcell')).not.toBeInTheDocument();
    expect(container.textContent).not.toMatch(/500|Boom|errorCode/);
  });

  it('a closures failure keeps the grid and adds a status with Retry', async () => {
    search = 'month=2027-02';
    server.use(
      http.get('*/api/admin/branches/:branchId/closures', () =>
        HttpResponse.json({}, { status: 500 }),
      ),
    );
    const { container } = renderPage();
    expect(
      await screen.findByText("Reasons for closed days couldn't be loaded.", {}, WAIT),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Retry' })).toBeInTheDocument();
    // The server's intervals still decide the day: 15 Feb is open 09:00-13:00 with no reason known.
    expect(
      screen.getByRole('gridcell', { name: /Monday 15 February 2027\. Open 09:00–13:00/ }),
    ).toBeInTheDocument();
    expect(container.textContent).not.toMatch(/500|errorCode/);
  });

  it('a branch with no weekly hours shows the notice with a link to the hours tab', async () => {
    search = 'month=2027-02';
    server.use(
      http.get('*/api/admin/branches/:branchId', ({ params }) =>
        HttpResponse.json({
          id: params.branchId,
          code: 'EMPTY',
          name: 'Empty branch',
          timeZoneId: 'Asia/Dhaka',
          isActive: true,
          addressLine: null,
          phoneNumber: null,
          email: null,
          weeklyHours: [],
        }),
      ),
      http.get('*/api/admin/branches/:branchId/calendar', () => HttpResponse.json([])),
    );
    renderPage();
    expect(
      await screen.findByText(
        /This branch has no opening hours yet, so every day shows as closed\./,
        {},
        WAIT,
      ),
    ).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Set opening hours' })).toHaveAttribute(
      'href',
      `/staff/admin/branches/${mainBranchId}/hours`,
    );
    expect(
      screen.getByRole('gridcell', {
        name: /Tuesday 16 February 2027\. Closed \(regular weekly closure\)/,
      }),
    ).toBeInTheDocument();
  });
});
