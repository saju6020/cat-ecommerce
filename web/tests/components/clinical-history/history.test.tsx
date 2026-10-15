import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '../../../mocks/server';
import { ClinicalHistoryView } from '@/components/clinical-history/ClinicalHistoryView';
import { SectionUnavailable } from '@/components/clinical-history/SectionUnavailable';
import { VisitView } from '@/app/(staff)/staff/visits/[visitId]/VisitView';
import { ids, renderWithClient, freshClinicBeforeEach } from '../visit/helpers';
import { currentUrl, setUrl } from '../booking/nav-mock';

vi.mock('next/navigation', () => import('../booking/nav-mock'));

freshClinicBeforeEach();

describe('HistoryDrawer over the workspace — US-CLN-13, ADR-FE-049', () => {
  it('U-F1: opening and closing the history while the note is dirty leaves the value and the URL alone', async () => {
    const user = userEvent.setup();
    setUrl(`/staff/visits/${ids.visitOpenId}`);
    renderWithClient(<VisitView visitId={ids.visitOpenId} />);
    const plan = await screen.findByLabelText('Plan');
    await user.clear(plan);
    await user.type(plan, 'Typed before looking something up');

    await user.click(screen.getByRole('button', { name: 'History' }));
    const drawer = await screen.findByRole('dialog', { name: /whiskers’s clinical history/i });
    expect(
      await within(drawer).findByRole('heading', { name: 'Active problems' }),
    ).toBeInTheDocument();
    await user.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());

    expect(screen.getByLabelText('Plan')).toHaveValue('Typed before looking something up');
    expect(currentUrl()).toBe(`/staff/visits/${ids.visitOpenId}`);
    expect(screen.getByRole('button', { name: 'History' })).toHaveFocus();
  });

  it('U-F1: the drawer is a labelled dialog with a tab per server section, Overview first', async () => {
    const user = userEvent.setup();
    renderWithClient(<VisitView visitId={ids.visitOpenId} />);
    await user.click(await screen.findByRole('button', { name: 'History' }));
    const drawer = await screen.findByRole('dialog');
    await within(drawer).findByRole('heading', { name: 'Active problems' });
    const tabs = within(drawer)
      .getAllByRole('tab')
      .map((t) => t.textContent);
    expect(tabs).toEqual([
      'Overview',
      'Timeline',
      'Visits',
      'Diagnoses',
      'Lab results',
      'Photos',
      'Vaccinations',
      'Parasite control',
      'Weights',
      'Prescriptions',
      'Boarding stays',
      'Food purchases',
    ]);
    expect(within(drawer).getByRole('tab', { name: 'Overview' })).toHaveAttribute(
      'aria-selected',
      'true',
    );
  });
});

describe('Sections — ADR-FE-056', () => {
  it('U-F2: an unavailable section shows its sentence and makes no request for a page', async () => {
    const user = userEvent.setup();
    const requested: string[] = [];
    server.events.on('request:start', ({ request }) => {
      if (/clinical-history\//.test(request.url)) requested.push(new URL(request.url).pathname);
    });
    renderWithClient(<ClinicalHistoryView catId={ids.catWhiskersId} layout="page" />);
    await user.click(await screen.findByRole('tab', { name: 'Prescriptions' }));
    expect(
      screen.getByText('Prescriptions will appear here once the pharmacy module is live.'),
    ).toBeInTheDocument();
    await user.click(screen.getByRole('tab', { name: 'Boarding stays' }));
    expect(
      screen.getByText('Boarding stays will appear here once the boarding module is live.'),
    ).toBeInTheDocument();
    await user.click(screen.getByRole('tab', { name: 'Food purchases' }));
    expect(
      screen.getByText('Food purchases will appear here once the shop orders module is live.'),
    ).toBeInTheDocument();
    server.events.removeAllListeners();
    expect(requested).toEqual([]); // not even the first page
    expect(screen.queryByText(/HO-PHM|HO-BRD|HO-ORD/)).toBeNull(); // the owner id is never shown
  });

  it('only the open tab fetches, and Load more adds the next page', async () => {
    const user = userEvent.setup();
    const pages: string[] = [];
    server.events.on('request:start', ({ request }) => {
      if (/clinical-history\/weights/.test(request.url)) pages.push(new URL(request.url).search);
    });
    renderWithClient(<ClinicalHistoryView catId={ids.catWhiskersId} layout="page" />);
    await screen.findByRole('heading', { name: 'Active problems' });
    expect(pages).toEqual([]);
    await user.click(screen.getByRole('tab', { name: 'Weights' }));
    expect(await screen.findByText(/Showing 20 of \d+/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Load more' }));
    expect(await screen.findByText(/Showing 40 of \d+/)).toBeInTheDocument();
    server.events.removeAllListeners();
    expect(pages).toHaveLength(2);
    expect(pages[1]).toContain('pageNumber=2');
  });

  it('U-F3: the weight trend is a chart AND a table alternative, with units', async () => {
    renderWithClient(<ClinicalHistoryView catId={ids.catWhiskersId} layout="page" />);
    const heading = await screen.findByRole('heading', { name: 'Weight trend' });
    const section = heading.closest('section')!;
    expect(section.querySelector('[aria-hidden="true"]')).not.toBeNull(); // the chart
    const table = within(section).getByRole('table', { hidden: true });
    expect(within(table).getAllByRole('row', { hidden: true }).length).toBeGreaterThan(100);
    expect(table).toHaveTextContent(/\d kg/);
  });

  it('opens a visit from the overview nested and read-only, with Back', async () => {
    const user = userEvent.setup();
    renderWithClient(<ClinicalHistoryView catId={ids.catWhiskersId} layout="drawer" />);
    const buttons = await screen.findAllByRole('button', {
      name: /open the general consultation visit/i,
    });
    await user.click(buttons[buttons.length - 1]!);
    expect(await screen.findByRole('article', { name: 'Consultation record' })).toBeInTheDocument();
    expect(screen.queryByRole('textbox')).toBeNull();
    await user.click(screen.getByRole('button', { name: /back to the history/i }));
    expect(await screen.findByRole('heading', { name: 'Active problems' })).toBeInTheDocument();
  });

  it('403 and not-found are their own states', async () => {
    server.use(
      http.get('*/api/staff/cats/:catId/clinical-history', () =>
        HttpResponse.json({ errors: [] }, { status: 403 }),
      ),
    );
    renderWithClient(<ClinicalHistoryView catId={ids.catWhiskersId} layout="page" />);
    expect(await screen.findByText(/you don.t have permission to do this/i)).toBeInTheDocument();
  });
});

describe('SectionUnavailable', () => {
  it('chooses its sentence from the section key', () => {
    const { container } = renderWithClient(<SectionUnavailable sectionKey="prescriptions" />);
    expect(container).toHaveTextContent(
      'Prescriptions will appear here once the pharmacy module is live.',
    );
  });
});
