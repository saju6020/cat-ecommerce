import { http, HttpResponse, type HttpHandler } from 'msw';
import type { Dashboard, DashboardFigure, DashboardTile } from '@/lib/api/dashboard';
import { branchFor, rolesOf, violation } from './fixtures';

/** Test controls: how often the dashboard has been polled (a second poll has changed numbers). */
export const dashboardControl = { polls: 0, refreshSeconds: 60 };

export function resetDashboardMock(): void {
  dashboardControl.polls = 0;
  dashboardControl.refreshSeconds = 60;
}

const count = (
  key: string,
  label: string,
  value: number | null,
  unit?: string,
): DashboardFigure => ({
  key,
  label,
  value,
  type: 'Integer',
  unit: unit ?? null,
});
const amount = (key: string, label: string, value: string | null): DashboardFigure => ({
  key,
  label,
  value: value === null ? null : { amount: value, currencyCode: 'BDT' },
  type: 'Money',
  unit: null,
});

function tile(
  key: string,
  title: string,
  figures: DashboardFigure[],
  reportKey: string | null,
  extra: Partial<DashboardTile> = {},
): DashboardTile {
  return {
    key,
    title,
    figures,
    drillDown: reportKey
      ? { reportKey, from: '2026-10-14', to: '2026-10-14', parameters: {} }
      : null,
    note: null,
    ...extra,
  };
}

function tilesFor(roles: string[], poll: number): DashboardTile[] {
  const tiles: DashboardTile[] = [];
  const has = (...r: string[]) => r.some((x) => roles.includes(x));
  if (has('Reception', 'Vet', 'Nurse', 'Administrator')) {
    tiles.push(
      tile(
        'clinic.appointments-today',
        'Appointments today',
        [count('total', 'Booked', 11 + poll), count('remaining', 'Remaining', 6)],
        'clinic-daily-schedule',
      ),
    );
  }
  if (has('Reception', 'Vet', 'Nurse')) {
    tiles.push(
      tile(
        'clinic.queue-now',
        'Queue now',
        [count('waiting', 'Waiting', 3), count('longestWait', 'Longest wait', 14, 'min')],
        null,
      ),
    );
  }
  if (has('Vet')) {
    tiles.push(
      tile(
        'clinic.consultations-unfinalised',
        'Consultations to finalise',
        [count('open', 'Completed, not finalised', 3), count('oldestAge', 'Oldest', null, 'h')],
        null,
      ),
    );
  }
  if (has('Inventory', 'Administrator')) {
    tiles.push(
      tile(
        'stock.expiry',
        'Expiring stock',
        [count('batches', 'Batches within 30 days', 4)],
        'stock-near-expiry',
      ),
    );
  }
  if (has('Accountant', 'Administrator')) {
    tiles.push(
      tile(
        'payments.today',
        'Payments today',
        [
          amount('CashOnDelivery', 'Cash on delivery', '4200.00'),
          amount('CashInStore', 'Cash in store', '9800.00'),
          amount('CardInStore', 'Card in store', '3100.00'),
          amount('BankTransfer', 'Bank transfer', '0.00'),
        ],
        'payment-reconciliation',
      ),
    );
  }
  if (has('Accountant')) {
    tiles.push(
      tile(
        'billing.cash-up',
        'Cash-up today',
        [
          { key: 'status', label: 'Today', value: 'Not closed', type: 'Text', unit: null },
          amount('variance', 'Variance', '350.00'),
        ],
        null,
      ),
    );
  }
  if (has('Administrator')) {
    tiles.push(
      tile(
        'billing.receivables',
        'Receivables',
        [amount('outstanding', 'Outstanding', '58000.00'), amount('over90', 'Over 90 days', null)],
        'revenue-by-business-line',
      ),
    );
  }
  return tiles;
}

export const dashboardHandlers: HttpHandler[] = [
  http.get('*/api/staff/dashboard', ({ request }) => {
    const branch = branchFor(request);
    dashboardControl.polls += 1;
    const roles = rolesOf(request);
    if (roles.includes('NoBranch'))
      return violation(403, 'BranchNotAssigned', 'You are not assigned to that branch.', null);
    const body: Dashboard = {
      branchId: branch.id,
      branchName: branch.name,
      localDate: branch.timeZoneId === 'Asia/Dhaka' ? '2026-10-14' : '2026-10-13',
      timeZoneId: branch.timeZoneId,
      generatedOn: `2026-10-14T0${Math.min(9, 3 + dashboardControl.polls)}:42:00+00:00`,
      refreshSeconds: dashboardControl.refreshSeconds,
      tiles: tilesFor(roles, dashboardControl.polls - 1),
    };
    return HttpResponse.json(body, { headers: { 'Cache-Control': 'no-store' } });
  }),
];
