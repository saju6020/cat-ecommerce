import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '../../../mocks/server';
import { DischargeSummaryView } from '@/components/visit/DischargeSummaryView';
import { MyCatVisits } from '@/components/visit/MyCatVisits';
import { MyVisitSummary } from '@/components/visit/MyVisitSummary';
import { getDischargeSummary, type DischargeSummary } from '@/lib/api/visits';
import { ids, renderWithClient, freshClinicBeforeEach } from './helpers';

freshClinicBeforeEach();

async function summary(overrides: Partial<DischargeSummary> = {}): Promise<DischargeSummary> {
  return { ...(await getDischargeSummary(ids.visitFinalisedId)), ...overrides };
}

describe('DischargeSummaryView — US-CLN-16', () => {
  it('U-G1: with prescriptionsAvailable false it says the medicines are on the prescription label', async () => {
    render(<DischargeSummaryView summary={await summary()} pdfUrl="/x.pdf" showPrint />);
    expect(
      screen.getByText('Medicines to take home are on the prescription label.'),
    ).toBeInTheDocument();
  });

  it('with prescriptions available that sentence is not shown', async () => {
    render(
      <DischargeSummaryView
        summary={await summary({ prescriptionsAvailable: true })}
        pdfUrl="/x.pdf"
        showPrint
      />,
    );
    expect(screen.queryByText('Medicines to take home are on the prescription label.')).toBeNull();
  });

  it('writes empty lists out, shows corrections, the weight with its unit, and no price', async () => {
    render(<DischargeSummaryView summary={await summary()} pdfUrl="/x.pdf" showPrint={false} />);
    expect(screen.getAllByText('None recorded').length).toBeGreaterThanOrEqual(2); // treatments, medicines
    expect(screen.getByText('3.85 kg')).toBeInTheDocument();
    expect(screen.getByText('Owner phoned: vomiting has stopped.')).toBeInTheDocument();
    expect(screen.getByText(/Mild gastroenteritis|Vomiting/)).toBeInTheDocument();
    expect(document.body.textContent).not.toMatch(/৳|BDT|\$\d|price|payment/i);
  });

  it('Print only for staff; Download PDF is the server’s link; the print stylesheet hides the chrome', async () => {
    const view = render(
      <DischargeSummaryView summary={await summary()} pdfUrl="https://api/x.pdf" showPrint />,
    );
    expect(screen.getByRole('button', { name: /print/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /download pdf/i })).toHaveAttribute(
      'href',
      'https://api/x.pdf',
    );
    expect(view.container.querySelector('style')?.textContent).toContain('@media print');
    view.unmount();
    render(<DischargeSummaryView summary={await summary()} pdfUrl="/x.pdf" showPrint={false} />);
    expect(screen.queryByRole('button', { name: /print/i })).toBeNull();
  });

  it('shows clinical text literally (ADR-FE-058)', async () => {
    render(
      <DischargeSummaryView
        summary={await summary({ homeCareInstructions: '<b>Rest</b> & **fluids**' })}
        pdfUrl="/x.pdf"
        showPrint={false}
      />,
    );
    expect(screen.getByText('<b>Rest</b> & **fluids**')).toBeInTheDocument();
    expect(document.querySelector('b')).toBeNull();
  });
});

describe('MyVisitSummary — FR-POR-3', () => {
  it('U-G2: a 404 is one neutral sentence that hints at nothing', async () => {
    renderWithClient(<MyVisitSummary visitId={ids.visitOpenId} />);
    expect(await screen.findByText('We couldn’t find this visit summary.')).toBeInTheDocument();
    expect(screen.queryByText(/open|finalis|not yours|permission/i)).toBeNull();
  });

  it('shows the summary and its PDF link for the owner’s finalised visit', async () => {
    renderWithClient(<MyVisitSummary visitId={ids.visitFinalisedId} />);
    expect(
      await screen.findByRole('heading', { name: /visit summary for mittens/i }),
    ).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /download pdf/i }).getAttribute('href')).toContain(
      `/api/customers/me/visits/${ids.visitFinalisedId}/discharge-summary.pdf`,
    );
    expect(screen.queryByRole('button', { name: /print/i })).toBeNull();
  });
});

describe('MyCatVisits — FR-POR-3', () => {
  it('lists finalised visits linking to their summary, and lab results with units, ranges and flags as text', async () => {
    renderWithClient(<MyCatVisits catId={ids.portalCatId} />);
    expect(
      await screen.findByRole('link', { name: /read the summary of the/i }),
    ).toBeInTheDocument();
    const labs = (await screen.findByRole('heading', { name: 'Lab results' })).closest('section')!;
    expect(await within(labs).findByText('180 µmol/L')).toBeInTheDocument();
    expect(within(labs).getByText('71–212 µmol/L')).toBeInTheDocument();
    expect(within(labs).getByText('Normal')).toBeInTheDocument();
    // Open for a PDF; nothing for DICOM is offered to an owner.
    expect(within(labs).getByRole('link', { name: /open lab-report\.pdf/i })).toBeInTheDocument();
    expect(within(labs).queryByText(/recorded by/i)).toBeNull();
  });

  it('is an empty state, not a blank, when there are no finalised visits', async () => {
    server.use(
      http.get('*/api/customers/me/cats/:catId/visits', () => HttpResponse.json([])),
      http.get('*/api/customers/me/cats/:catId/lab-results', () => HttpResponse.json([])),
    );
    renderWithClient(<MyCatVisits catId={ids.portalCatId} />);
    expect(await screen.findByText('No visit summaries yet.')).toBeInTheDocument();
    expect(await screen.findByText('No lab results yet.')).toBeInTheDocument();
  });

  it('says plainly when the cat is not the owner’s', async () => {
    renderWithClient(<MyCatVisits catId="20000000-0000-0000-0000-000000000099" />);
    expect(await screen.findByText('We couldn’t find this cat.')).toBeInTheDocument();
  });
});

void vi;
