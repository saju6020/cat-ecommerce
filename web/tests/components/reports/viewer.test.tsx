import { beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { ReportViewer } from '@/components/reports/ReportViewer';
import { ReportCatalogue } from '@/components/reports/ReportCatalogue';
import { ReportErrorView } from '@/components/reports/ReportErrorView';
import { catalogueFor } from '@/mocks/handlers/reports/fixtures';
import { ApiForbiddenError } from '@/lib/api/client';
import { resetReportsMockData, setMockReportRoles } from '@/mocks/handlers/reports';
import { ReportTable } from '@/components/reports/ReportTable';
import { ReportCardList } from '@/components/reports/ReportCardList';
import { col, renderWithClient } from './helpers';

const nav = vi.hoisted(() => ({ search: '', push: vi.fn() }));
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: nav.push, replace: nav.push }),
  usePathname: () => '/staff/reports/x',
  useSearchParams: () => new URLSearchParams(nav.search),
}));

beforeEach(() => {
  nav.search = '';
  nav.push.mockReset();
  resetReportsMockData();
});

const admin = ['Administrator'];
const shopManager = ['ShopManager'];

function useCatalogue(roles: string[]) {
  const entries = catalogueFor(roles);
  setMockReportRoles(roles);
  server.use(http.get('*/api/staff/reports', () => HttpResponse.json(entries)));
  return entries;
}

describe('catalogue — U-B1, U-B2', () => {
  it('U-B1: groups by plain-language category and omits what the payload omits', () => {
    renderWithClient(<ReportCatalogue reports={catalogueFor(['Reception'])} />);
    expect(screen.getByRole('heading', { name: 'Clinic operations' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Financial' })).not.toBeInTheDocument();
    expect(screen.getByText('Covers up to 31 days')).toBeInTheDocument();
  });

  it('filters by title and description on the client', async () => {
    renderWithClient(<ReportCatalogue reports={catalogueFor(admin)} />);
    await userEvent.type(screen.getByLabelText('Search reports'), 'expiry');
    expect(screen.getByRole('link', { name: /Near expiry/ })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Daily schedule/ })).not.toBeInTheDocument();
  });

  it('U-B2: a 403 renders PermissionDeniedState', () => {
    renderWithClient(
      <ReportErrorView
        error={
          new ApiForbiddenError({
            errors: [{ errorCode: 'ActionNotPermitted', errorMessage: 'no' }],
          })
        }
      />,
    );
    expect(screen.getByText(/have permission/i)).toBeInTheDocument();
  });
});

describe('ReportViewer — U-C series', () => {
  it('U-C2/U-C4/U-C7/U-C8: seeds from defaultRange, shows notes, totals, sort buttons, null cells', async () => {
    useCatalogue(admin);
    renderWithClient(<ReportViewer reportKey="payment-reconciliation" />);
    const table = await screen.findByRole('table', { name: /Payment reconciliation/ });
    expect(screen.getByLabelText(/From/)).toHaveValue('2026-10-13');
    expect(screen.getByText(/Card in store shows what was expected only/)).toBeInTheDocument();
    expect(within(table).getByRole('columnheader', { name: /Business date/ })).toHaveAttribute(
      'aria-sort',
      'none',
    );
    expect(within(table).getAllByText('Not recorded').length).toBeGreaterThan(0);
    expect(table.querySelector('tfoot')).toHaveTextContent('Total');
    // Over / Short / Matched come from the server's varianceDirection column.
    for (const word of ['Short', 'Matched', 'Over']) {
      expect(within(table).getAllByText(word).length).toBeGreaterThan(0);
    }
    expect(screen.getByRole('button', { name: 'Export CSV' })).toBeInTheDocument();
  });

  it('U-C7: sorting writes the URL; a non-sortable header has no button', async () => {
    useCatalogue(admin);
    renderWithClient(<ReportViewer reportKey="clinic-daily-schedule" />);
    await screen.findByRole('table');
    expect(screen.queryByRole('button', { name: /^Service/ })).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: /^Cat/ }));
    expect(nav.push).toHaveBeenCalledWith(expect.stringContaining('sort=cat'));
  });

  it('U-C3: Apply writes the filters to the URL', async () => {
    useCatalogue(admin);
    renderWithClient(<ReportViewer reportKey="clinic-daily-schedule" />);
    await screen.findByRole('table');
    const from = screen.getByLabelText(/From/);
    await userEvent.clear(from);
    await userEvent.type(from, '2026-10-01');
    await userEvent.click(screen.getByRole('button', { name: 'Apply' }));
    expect(nav.push).toHaveBeenCalledWith(expect.stringContaining('from=2026-10-01'));
  });

  it('U-C1: one control per parameter, typed and bounded', async () => {
    useCatalogue(admin);
    renderWithClient(<ReportViewer reportKey="stock-near-expiry" />);
    await screen.findByRole('table');
    const days = screen.getByLabelText('Expiring within (days)');
    expect(days).toHaveAttribute('type', 'number');
    expect(days).toHaveAttribute('max', '365');
    expect(screen.getByLabelText('Category').tagName).toBe('SELECT');
    expect(screen.getByLabelText('Include quarantined stock')).toHaveAttribute('type', 'checkbox');
    expect(screen.getByLabelText('Supplier id')).toHaveAttribute('type', 'text');
    expect(screen.getByText('Up to 366 days')).toBeInTheDocument();
  });

  it('U-C5: restricted columns are absent for a shop manager, present for an accountant', async () => {
    useCatalogue(shopManager);
    const first = renderWithClient(<ReportViewer reportKey="revenue-shop-by-category" />);
    await screen.findByRole('table');
    expect(screen.queryByRole('columnheader', { name: /Margin/ })).not.toBeInTheDocument();
    expect(screen.queryByText(/hidden|locked|restricted/i)).not.toBeInTheDocument();
    first.unmount();

    useCatalogue(['Accountant']);
    renderWithClient(<ReportViewer reportKey="revenue-shop-by-category" />);
    expect(await screen.findByRole('columnheader', { name: /Margin %/ })).toBeInTheDocument();
    expect(screen.getByText(/Margin covers 83.0 %/)).toBeInTheDocument();
  });

  it('U-C9: the chart is hidden by default and its hidden table matches the rows', async () => {
    useCatalogue(admin);
    renderWithClient(<ReportViewer reportKey="new-vs-returning" />);
    await screen.findByRole('table');
    expect(screen.queryByTestId('chart-data-table')).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Show chart' }));
    const hidden = await screen.findByTestId('chart-data-table');
    expect(within(hidden).getAllByRole('row')).toHaveLength(1 + 6);
    expect(within(hidden).getAllByRole('cell')[1]).toHaveTextContent('4');
  });

  it('U-C10: the empty state names the period and branch and suggests widening', async () => {
    useCatalogue(admin);
    nav.search = 'from=2026-01-01&to=2026-01-01';
    renderWithClient(<ReportViewer reportKey="clinic-daily-schedule" />);
    expect(
      await screen.findByText(
        /No results for Daily schedule from 01 Jan 2026 to 01 Jan 2026 at Main clinic/,
        {},
        { timeout: 10_000 },
      ),
    ).toBeInTheDocument();
    expect(screen.getByText(/wider date range/)).toBeInTheDocument();
  }, 20_000);

  it('shows the range message on the To field and keeps the typed values', async () => {
    useCatalogue(admin);
    nav.search = 'from=2026-01-01&to=2026-12-31';
    renderWithClient(<ReportViewer reportKey="clinic-daily-schedule" />);
    expect(await screen.findByText(/covers at most 31 days/)).toBeInTheDocument();
    expect(screen.getByLabelText(/^To/)).toHaveValue('2026-12-31');
    expect(screen.getByLabelText(/^To/)).toHaveAttribute('aria-invalid', 'true');
    expect(screen.queryByText('ReportRangeTooLong')).not.toBeInTheDocument();
  });

  it('maps a timeout to a page banner with advice', async () => {
    useCatalogue(admin);
    nav.search = 'from=2020-01-01&to=2020-01-02';
    renderWithClient(<ReportViewer reportKey="clinic-daily-schedule" />);
    expect(await screen.findByText(/took too long/)).toBeInTheDocument();
  });

  it('a key outside the caller catalogue shows the server 403', async () => {
    useCatalogue(['Reception']);
    renderWithClient(<ReportViewer reportKey="revenue-by-business-line" />);
    await waitFor(() => expect(screen.getByText(/have permission/i)).toBeInTheDocument());
  });

  it('an unknown key links back to the catalogue', async () => {
    useCatalogue(admin);
    renderWithClient(<ReportViewer reportKey="nope" />);
    expect(await screen.findByText(/does not exist|doesn.t exist/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'All reports' })).toHaveAttribute(
      'href',
      '/staff/reports',
    );
  });

  it('U-B3: no Schedule this report without canSchedule', async () => {
    useCatalogue(['Accountant']);
    renderWithClient(<ReportViewer reportKey="payment-reconciliation" />);
    await screen.findByRole('table');
    expect(screen.queryByRole('link', { name: 'Schedule this report' })).not.toBeInTheDocument();
  });

  it('offers Schedule this report to an administrator, prefilled', async () => {
    useCatalogue(admin);
    nav.search = 'category=Food';
    renderWithClient(<ReportViewer reportKey="stock-near-expiry" />);
    const link = await screen.findByRole('link', { name: 'Schedule this report' });
    expect(link.getAttribute('href')).toContain('reportKey=stock-near-expiry');
    expect(link.getAttribute('href')).toContain('category=Food');
  });
});

describe('varianceDirection rendering (HO-RPT-001-FU1)', () => {
  const columns = [
    col('method', 'Method', 'Text'),
    col('variance', 'Variance', 'Money'),
    col('varianceDirection', 'Variance direction', 'Text'),
  ];
  // The signs are deliberately inconsistent: only the word may drive the badge.
  const rows = [
    {
      method: 'COD',
      variance: { amount: '350.00', currencyCode: 'BDT' },
      varianceDirection: 'Short',
    },
    {
      method: 'Bank',
      variance: { amount: '-120.00', currencyCode: 'BDT' },
      varianceDirection: 'Over',
    },
    {
      method: 'Cash',
      variance: { amount: '0.00', currencyCode: 'BDT' },
      varianceDirection: 'Matched',
    },
    { method: 'Card', variance: null, varianceDirection: null },
  ];

  it('table: Short, Over, Matched as words; null is a dash with Not recorded, never Matched', () => {
    renderWithClient(
      <ReportTable
        caption="x"
        columns={columns}
        rows={rows}
        totals={null}
        timeZoneId="Asia/Dhaka"
        sort=""
        onSort={() => {}}
      />,
    );
    const body = screen.getAllByRole('row').slice(1);
    expect(within(body[0]!).getByText('Short')).toBeInTheDocument();
    expect(within(body[1]!).getByText('Over')).toBeInTheDocument();
    expect(within(body[2]!).getByText('Matched')).toBeInTheDocument();
    expect(within(body[3]!).queryByText(/Over|Short|Matched/)).not.toBeInTheDocument();
    expect(within(body[3]!).getAllByText('Not recorded').length).toBeGreaterThan(0);
  });

  it('card list shows the same words', () => {
    renderWithClient(
      <ReportCardList
        label="x"
        columns={columns}
        rows={rows}
        totals={null}
        timeZoneId="Asia/Dhaka"
      />,
    );
    expect(screen.getByText('Short')).toBeInTheDocument();
    expect(screen.getByText('Over')).toBeInTheDocument();
    expect(screen.getByText('Matched')).toBeInTheDocument();
    expect(screen.getAllByText('Not recorded').length).toBeGreaterThan(0);
  });

  it('a column description is optional and absent on other columns', () => {
    expect(columns[0]!.description ?? null).toBeNull();
  });
});
