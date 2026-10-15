import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactElement } from 'react';
import { server } from '../../../mocks/server';
import { violations } from '../../../mocks/handlers/ordering/fixtures';
import { ReturnDecisionPanel } from '@/components/returns/ReturnDecisionPanel';
import { ReceiveReturnForm } from '@/components/returns/ReceiveReturnForm';
import type { StaffReturnDetail, StaffReturnLine } from '@/lib/api/staff-returns';

vi.mock('next/navigation', () => import('../booking/nav-mock'));

function renderWithClient(ui: ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: 0 }, mutations: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

function line(overrides: Partial<StaffReturnLine> = {}): StaffReturnLine {
  return {
    orderLineId: 'ln1',
    productName: 'Royal Canin Kitten',
    variantLabel: '2 kg',
    quantity: 2,
    reason: 'QualityDefect',
    condition: 'Opened',
    customerNote: null,
    requiresDefectConfirmation: true,
    defectConfirmed: false,
    defectNote: null,
    quantityReceived: 0,
    anyBatchRecalled: false,
    batchNumbers: ['B-1'],
    ...overrides,
  };
}

function ret(lines: StaffReturnLine[], status: StaffReturnDetail['status'] = 'Requested'): StaffReturnDetail {
  return {
    id: 'r1',
    version: 1,
    orderId: 'o1',
    orderNumber: 'ORD-2026-000150',
    customerName: 'Sam Rahman',
    kind: 'CustomerReturn',
    status,
    requestedOn: '2026-10-10T04:00:00Z',
    requestedBy: 'Sam Rahman',
    decidedOn: null,
    decidedBy: null,
    decisionNote: null,
    receivedOn: null,
    lines,
    branchTimeZoneId: 'Asia/Dhaka',
  };
}

describe('Approving a return that needs a defect - U-E3', () => {
  it('Approve stays disabled, with the reason, until the defect is confirmed with a note', async () => {
    const user = userEvent.setup();
    renderWithClient(<ReturnDecisionPanel ret={ret([line()])} />);
    const approve = screen.getByRole('button', { name: 'Approve return' });
    expect(approve).toBeDisabled();
    expect(screen.getByText(/Approve is unavailable until a defect is confirmed, with a note, on: Royal Canin Kitten\./)).toBeInTheDocument();

    await user.click(screen.getByRole('checkbox', { name: /Defect confirmed/ }));
    expect(approve).toBeDisabled(); // still no note
    await user.type(screen.getByLabelText(/Defect note/), 'Torn bag, kibble stale');
    expect(approve).toBeEnabled();
    expect(screen.queryByText(/Approve is unavailable/)).not.toBeInTheDocument();
  });

  it('sends the confirmation and note with the approval', async () => {
    let body: unknown;
    server.use(
      http.post('*/api/staff/returns/r1/approve', async ({ request }) => {
        body = await request.json();
        return new HttpResponse(null, { status: 204 });
      }),
    );
    const user = userEvent.setup();
    renderWithClient(<ReturnDecisionPanel ret={ret([line()])} />);
    await user.click(screen.getByRole('checkbox', { name: /Defect confirmed/ }));
    await user.type(screen.getByLabelText(/Defect note/), 'Torn bag');
    await user.click(screen.getByRole('button', { name: 'Approve return' }));
    await waitFor(() =>
      expect(body).toMatchObject({ lines: [{ orderLineId: 'ln1', defectConfirmed: true, defectNote: 'Torn bag' }] }),
    );
  });

  it('a return with no flagged line can be approved at once and shows no defect controls', () => {
    renderWithClient(<ReturnDecisionPanel ret={ret([line({ requiresDefectConfirmation: false, condition: 'Sealed', reason: 'ChangedMind' })])} />);
    expect(screen.getByRole('button', { name: 'Approve return' })).toBeEnabled();
    expect(screen.queryByRole('checkbox', { name: /Defect confirmed/ })).not.toBeInTheDocument();
  });

  it('says when a batch was recalled, and shows the server refusal on the line it names', async () => {
    server.use(
      http.post('*/api/staff/returns/r1/approve', () =>
        violations([{ propertyName: 'lines[0].defectNote', errorCode: 'DefectNoteRequired', errorMessage: 'Describe the defect on this line.' }]),
      ),
    );
    const user = userEvent.setup();
    renderWithClient(<ReturnDecisionPanel ret={ret([line({ anyBatchRecalled: true })])} />);
    expect(screen.getByText(/A batch of this item has been recalled\./)).toBeInTheDocument();
    await user.click(screen.getByRole('checkbox', { name: /Defect confirmed/ }));
    await user.type(screen.getByLabelText(/Defect note/), 'x');
    await user.click(screen.getByRole('button', { name: 'Approve return' }));
    const message = await screen.findByText('Describe the defect on this line.');
    expect(message.closest('li')).toHaveTextContent('Royal Canin Kitten');
  });
});

describe('Receiving a return - U-E4', () => {
  it('pre-fills each line with its approved quantity, caps the input and states the quarantine rule', () => {
    renderWithClient(<ReceiveReturnForm ret={ret([line({ requiresDefectConfirmation: false, quantity: 3 })], 'Approved')} />);
    const input = screen.getByLabelText(/Received: Royal Canin Kitten/);
    expect(input).toHaveValue(3);
    expect(input).toHaveAttribute('max', '3');
    expect(screen.getByText('Approved quantity: 3')).toBeInTheDocument();
    expect(screen.getByText(/Received items go to quarantine until Inventory decides what happens to them\./)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /restock|write off/i })).not.toBeInTheDocument();
  });

  it('sends the quantities typed', async () => {
    let body: unknown;
    server.use(
      http.post('*/api/staff/returns/r1/receive', async ({ request }) => {
        body = await request.json();
        return new HttpResponse(null, { status: 204 });
      }),
    );
    const user = userEvent.setup();
    renderWithClient(<ReceiveReturnForm ret={ret([line({ quantity: 3 })], 'Approved')} />);
    const input = screen.getByLabelText(/Received: Royal Canin Kitten/);
    await user.clear(input);
    await user.type(input, '2');
    await user.click(screen.getByRole('button', { name: 'Record receipt' }));
    await waitFor(() => expect(body).toEqual({ lines: [{ orderLineId: 'ln1', quantityReceived: 2 }] }));
  });

  it('shows the server limit message on the line, not at the top', async () => {
    server.use(
      http.post('*/api/staff/returns/r1/receive', () =>
        violations([{ propertyName: 'lines[0].quantityReceived', errorCode: 'QuantityExceedsApproved', errorMessage: 'You cannot receive more than 3.' }]),
      ),
    );
    const user = userEvent.setup();
    renderWithClient(<ReceiveReturnForm ret={ret([line({ quantity: 3 })], 'Approved')} />);
    await user.click(screen.getByRole('button', { name: 'Record receipt' }));
    const message = await screen.findByText('You cannot receive more than 3.');
    expect(message.closest('li')).toHaveTextContent('Received: Royal Canin Kitten');
    expect(within(screen.getByRole('list', { name: 'Lines to receive' })).getByRole('alert')).toBe(message);
  });
});
