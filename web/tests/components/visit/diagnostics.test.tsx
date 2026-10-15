import { describe, expect, it } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '../../../mocks/server';
import { FlagBadge } from '@/components/visit/FlagBadge';
import { AttachmentList } from '@/components/visit/panels/AttachmentList';
import { DiagnosticOrderView } from '@/components/visit/panels/DiagnosticOrderView';
import { OrderDiagnosticDialog } from '@/components/visit/panels/OrderDiagnosticDialog';
import { formatFlag, summariseFlagsField } from '@/lib/format/clinical';
import { previewNumericFlag, previewQualitativeFlag } from '@/lib/validation/clinical';
import { getDiagnosticOrder } from '@/lib/api/diagnostics';
import { ids, renderWithClient, freshClinicBeforeEach } from './helpers';

freshClinicBeforeEach();

describe('Result flags — UI-6', () => {
  it('U-C2: every flag is words plus an icon, never colour alone', () => {
    const { container } = render(
      <>
        {(['High', 'Low', 'Abnormal', 'Normal', 'NotAssessed'] as const).map((flag) => (
          <FlagBadge key={flag} flag={flag} />
        ))}
      </>,
    );
    const text = container.textContent;
    for (const expected of ['High ↑', 'Low ↓', 'Abnormal', 'Normal', 'Not assessed']) {
      expect(text).toContain(expected);
    }
    expect(container.querySelectorAll('svg[aria-hidden="true"]')).toHaveLength(5);
    expect(formatFlag('NotAssessed')).toBe('Not assessed');
  });

  it('summarises the flags of a lab history item in words', () => {
    expect(summariseFlagsField('High:2,Normal:3,Abnormal:1')).toBe('2 High, 1 Abnormal');
    expect(summariseFlagsField('Normal:2')).toBe('');
  });

  it('the preview rule is the server’s: bounds are inclusive, no bound = Not assessed', () => {
    expect(previewNumericFlag(212, 71, 212)).toBe('Normal');
    expect(previewNumericFlag(70.9, 71, 212)).toBe('Low');
    expect(previewNumericFlag(213, 71, 212)).toBe('High');
    expect(previewNumericFlag(5, null, null)).toBe('NotAssessed');
    expect(previewQualitativeFlag(' negative ', 'Negative')).toBe('Normal');
    expect(previewQualitativeFlag('Positive', 'Negative')).toBe('Abnormal');
    expect(previewQualitativeFlag('Positive', '')).toBe('NotAssessed');
  });
});

describe('Result entry — US-CLN-14 AC2', () => {
  it('U-C1: a live preview flag per row, replaced by the server’s flag after saving', async () => {
    const user = userEvent.setup();
    // The server judges the value differently from the preview (e.g. a range updated meanwhile).
    let posted = false;
    server.use(
      http.post('*/api/staff/diagnostics/:id/results', () => {
        posted = true;
        return HttpResponse.json({ resultSetId: 'x', values: [] }, { status: 201 });
      }),
      http.get('*/api/staff/diagnostics/:id', async ({ request, params }) => {
        void request;
        const order = await getDiagnosticOrderFromStore(params.id as string);
        if (!posted) return HttpResponse.json(order);
        return HttpResponse.json({
          ...order,
          status: 'Resulted',
          resultSets: [
            {
              id: 'set-1',
              reportedOn: '2026-10-07',
              note: null,
              supersedesResultSetId: null,
              recordedBy: 'Dr Akter',
              recordedOn: new Date().toISOString(),
              values: [
                {
                  id: 'v1',
                  name: 'Creatinine',
                  valueKind: 'Numeric',
                  numericValue: '150',
                  textValue: null,
                  unit: 'µmol/L',
                  referenceLow: '71',
                  referenceHigh: '212',
                  normalText: null,
                  flag: 'Abnormal',
                },
              ],
            },
          ],
        });
      }),
    );
    renderWithClient(
      <DiagnosticOrderView orderId={ids.orderPendingId} canWrite zone="Asia/Dhaka" />,
    );
    await user.click(await screen.findByText('Enter results'));
    const value = (await screen.findAllByLabelText('Result, µmol/L'))[0]!;
    await user.type(value, '300');
    expect(await screen.findAllByText('High ↑')).not.toHaveLength(0); // the preview
    await user.clear(value);
    await user.type(value, '150');
    expect(screen.getAllByText('Normal').length).toBeGreaterThan(0); // preview says Normal
    await user.type((await screen.findAllByLabelText('Result, mmol/L'))[0]!, '8');
    await user.click(screen.getByRole('button', { name: 'Save results' }));
    // After the save the record shows the SERVER's flag.
    const table = await screen.findByRole('table');
    expect(await within(table).findByText('Abnormal')).toBeInTheDocument();
  });

  it('a qualitative row previews Normal / Abnormal from the normal result', async () => {
    const user = userEvent.setup();
    renderWithClient(
      <DiagnosticOrderView orderId={ids.orderPendingId} canWrite zone="Asia/Dhaka" />,
    );
    await user.click(await screen.findByText('Enter results'));
    await user.click(await screen.findByRole('button', { name: /add another value/i }));
    const rows = await screen.findAllByRole('group', { name: /Value \d/ });
    const last = rows[rows.length - 1]!;
    await user.click(within(last).getByRole('radio', { name: 'Text result' }));
    await user.type(within(last).getByLabelText('Result'), 'Positive');
    await user.type(within(last).getByLabelText('Normal result'), 'Negative');
    expect(within(last).getByText('Abnormal')).toBeInTheDocument();
  });

  it('a read-only caller sees results and attachments but no entry or attach controls', async () => {
    renderWithClient(
      <DiagnosticOrderView orderId={ids.orderResultedId} canWrite={false} zone="Asia/Dhaka" />,
    );
    await screen.findAllByRole('table');
    expect(screen.queryByText('Enter results')).toBeNull();
    expect(screen.queryByRole('button', { name: 'Attach file' })).toBeNull();
    // The superseded first set is collapsed under its own summary.
    expect(screen.getByText(/Superseded on/)).toBeInTheDocument();
  });
});

async function getDiagnosticOrderFromStore(id: string) {
  const { orderViewFor } = await import('../../../mocks/handlers/visits/diagnostics');
  const { findOrder } = await import('../../../mocks/handlers/visits/store');
  return orderViewFor(findOrder(id)!);
}

describe('Attachments — U-C3', () => {
  it('a DICOM attachment has Download and no inline Open; a PDF has Open in a new tab', async () => {
    const order = await getDiagnosticOrder(ids.orderImagingId);
    render(<AttachmentList attachments={order.attachments} />);
    expect(screen.getByRole('link', { name: /download stifle-study\.dcm/i })).toHaveAttribute(
      'download',
    );
    expect(screen.queryByRole('link', { name: /open stifle-study\.dcm/i })).toBeNull();
    const open = screen.getByRole('link', { name: /open radiology-report\.pdf in a new tab/i });
    expect(open).toHaveAttribute('target', '_blank');
    expect(open).toHaveAttribute('rel', expect.stringContaining('noopener'));
  });
});

describe('OrderDiagnosticDialog — US-CLN-14 AC1', () => {
  it('Imaging requires a description (and says so on the field); a blood-work order offers its analytes', async () => {
    const user = userEvent.setup();
    const placed: unknown[] = [];
    renderWithClient(
      <OrderDiagnosticDialog
        open
        onOpenChange={() => undefined}
        catName="Whiskers"
        order={async (request) => void placed.push(request)}
      />,
    );
    expect(await screen.findByLabelText('Creatinine')).toBeInTheDocument();
    await user.selectOptions(screen.getByLabelText('Kind of test'), 'Imaging');
    expect(screen.queryByLabelText('Creatinine')).toBeNull();
    await user.click(screen.getByRole('button', { name: 'Place order' }));
    expect(await screen.findByText('Describe the Imaging study to be done.')).toBeInTheDocument();
    expect(placed).toHaveLength(0);
    await user.type(screen.getByLabelText(/Description/), 'Left stifle');
    await user.click(screen.getByRole('button', { name: 'Place order' }));
    await waitFor(() =>
      expect(placed).toEqual([{ kind: 'Imaging', description: 'Left stifle', analyteIds: [] }]),
    );
  });
});
