import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { ClinicalHistoryView } from '@/components/clinical-history/ClinicalHistoryView';
import { DischargeSummaryView } from '@/components/visit/DischargeSummaryView';
import { PhotosPanel } from '@/components/visit/panels/PhotosPanel';
import { VisitRecordView } from '@/components/visit/VisitRecordView';
import { VitalsForm } from '@/components/visit/vitals/VitalsForm';
import { DiagnosticOrderView } from '@/components/visit/panels/DiagnosticOrderView';
import { getDischargeSummary, getVisit, type VisitDetail } from '@/lib/api/visits';
import { ids, renderWithClient, freshClinicBeforeEach } from './helpers';

freshClinicBeforeEach();
afterEach(() => vi.restoreAllMocks());

const MEASUREMENT_LABEL =
  /^(latest )?(weight|temperature|heart rate|respiratory rate|body condition|pain)/i;
const HAS_UNIT = /\d[\d.,]*\s*(kg|°C|bpm|breaths\/min|\/9|\/10)\b/;

/** UI-8 (U-X3): next to every clinical-measurement label there is a value that carries its unit. */
function expectEveryMeasurementHasAUnit(root: ParentNode): number {
  let checked = 0;
  for (const term of root.querySelectorAll('dt')) {
    if (!MEASUREMENT_LABEL.test(term.textContent?.trim() ?? '')) continue;
    const value = term.nextElementSibling?.textContent ?? '';
    if (/\d/.test(value)) {
      expect(value, `"${term.textContent}" is shown without its unit`).toMatch(HAS_UNIT);
      checked += 1;
    }
  }
  return checked;
}

describe('UI-8 — no clinical number without its unit (U-X3)', () => {
  it('in the record view’s vitals, the summary’s weight and the history’s last vitals', async () => {
    const record = await getVisit(ids.visitOpenId);
    const { container, unmount } = render(<VisitRecordView visit={record} />);
    expect(expectEveryMeasurementHasAUnit(container)).toBeGreaterThanOrEqual(6);
    unmount();

    const summary = render(
      <DischargeSummaryView
        summary={await getDischargeSummary(ids.visitFinalisedId)}
        pdfUrl="/x"
        showPrint
      />,
    );
    expect(expectEveryMeasurementHasAUnit(summary.container)).toBeGreaterThanOrEqual(1);
    summary.unmount();

    const history = renderWithClient(
      <ClinicalHistoryView catId={ids.catWhiskersId} layout="page" />,
    );
    await screen.findByRole('heading', { name: 'Last vitals' });
    await waitFor(() =>
      expect(expectEveryMeasurementHasAUnit(history.container)).toBeGreaterThanOrEqual(6),
    );
  });

  it('every vitals input names its unit', async () => {
    renderWithClient(<VitalsForm zone="Asia/Dhaka" save={async () => undefined} />);
    for (const name of [
      'Weight, kilograms',
      'Temperature, degrees Celsius',
      'Heart rate, beats per minute',
      'Respiratory rate, breaths per minute',
      'Body condition score, out of 9',
      'Pain score, out of 10',
    ]) {
      expect(await screen.findByLabelText(name)).toBeInTheDocument();
    }
    expect(screen.getAllByText(/^(kg|°C|bpm|breaths\/min|\/9|\/10)$/)).toHaveLength(6);
  });
});

describe('ADR-FE-058 — clinical text is plain text (U-X2)', () => {
  it('SOAP text with <script> and **bold** renders literally in the record', async () => {
    const base = await getVisit(ids.visitOpenId);
    const visit: VisitDetail = {
      ...base,
      note: {
        ...base.note,
        subjective: '<script>alert(1)</script> **not bold**',
        plan: '<img src=x onerror=alert(2)>',
      },
    };
    const { container } = render(<VisitRecordView visit={visit} />);
    expect(screen.getByText('<script>alert(1)</script> **not bold**')).toBeInTheDocument();
    expect(screen.getByText('<img src=x onerror=alert(2)>')).toBeInTheDocument();
    expect(container.querySelector('script')).toBeNull();
    expect(container.querySelector('img[src="x"]')).toBeNull();
    expect(container.querySelector('strong')).toBeNull();
    expect(screen.getByText('<script>alert(1)</script> **not bold**')).toHaveClass(
      'whitespace-pre-wrap',
    );
  });
});

describe('Signed URLs — NFR-SEC-5 (U-X4)', () => {
  it('are never in a query key, in browser storage or in the console', async () => {
    const setItem = vi.spyOn(Storage.prototype, 'setItem');
    const consoleCalls: string[] = [];
    for (const method of ['log', 'info', 'warn', 'error', 'debug'] as const) {
      vi.spyOn(console, method).mockImplementation(
        (...args: unknown[]) => void consoleCalls.push(args.map(String).join(' ')),
      );
    }
    const base = await getVisit(ids.visitOpenId);
    const { client } = renderWithClient(
      <>
        <PhotosPanel visit={base} />
        <DiagnosticOrderView orderId={ids.orderImagingId} canWrite={false} zone="Asia/Dhaka" />
      </>,
    );
    await screen.findByText('stifle-study.dcm');
    // The URL is in the payload (so the page can use it) …
    expect(
      JSON.stringify(
        client
          .getQueryCache()
          .getAll()
          .map((q) => q.state.data),
      ),
    ).toContain('sig=');
    // … but never in anything that identifies, stores or logs it.
    const keys = JSON.stringify(
      client
        .getQueryCache()
        .getAll()
        .map((q) => q.queryKey),
    );
    expect(keys).not.toMatch(/sig=|\/content\?|exp=/);
    expect(setItem.mock.calls.map(String).join('\n')).not.toMatch(/sig=|\/content\?/);
    expect(consoleCalls.join('\n')).not.toMatch(/sig=|\/content\?/);
  });
});
