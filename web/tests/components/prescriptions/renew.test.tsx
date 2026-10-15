import { beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { PrescriptionComposer } from '@/components/prescriptions/PrescriptionComposer';
import { pharmacyFixtureIds as ids, resetPharmacyMockData } from '@/mocks/handlers/pharmacy';
import { renderWithClient, resetNavigation } from '../common/pharmacyTestUtils';

vi.mock('next/navigation', async () =>
  (await import('../common/pharmacyTestUtils')).navigationMock(),
);
vi.mock('@/lib/hooks/useStaffRoles', () => ({
  useStaffRoles: () => ({
    roleIds: ['Vet'],
    ready: true,
    has: (...wanted: string[]) => wanted.includes('Vet'),
  }),
}));
vi.setConfig({ testTimeout: 30_000 });

const API = 'https://localhost:7099';

beforeEach(() => {
  resetPharmacyMockData();
  resetNavigation();
});

describe('U-A13 — remote renewal (owner decision 2026-10-10)', () => {
  it('needs no open visit, says so, prefills from the prescription and sends no visit', async () => {
    const user = userEvent.setup();
    let body: Record<string, unknown> | null = null;
    server.events.on('request:start', async ({ request }) => {
      if (request.url.endsWith(`/prescriptions/${ids.rxMittensActiveId}/renew`)) {
        body = (await request.clone().json()) as Record<string, unknown>;
      }
    });
    const onIssued = vi.fn();
    renderWithClient(
      <PrescriptionComposer
        catId={ids.catMittensId}
        visitId={null}
        renewFrom={ids.rxMittensActiveId}
        onIssued={onIssued}
      />,
    );

    expect(await screen.findByText('Remote renewal')).toBeInTheDocument();
    expect(
      screen.getByText('All checks run again on today’s weight and records.'),
    ).toBeInTheDocument();
    // Prefilled from RX-2026-000101: 0.30 ml once daily for 7 days, 10.00 ml.
    await waitFor(() =>
      expect(screen.getByRole('textbox', { name: /^dose/i })).toHaveValue('0.30'),
    );
    expect(screen.getByRole('combobox', { name: 'Dose unit' })).toHaveValue('Ml');
    expect(screen.getByLabelText(/^frequency/i)).toHaveValue('OnceDaily');
    expect(screen.getByRole('textbox', { name: /^quantity per supply/i })).toHaveValue('10.00');
    expect((screen.getByLabelText(/^instructions/i) as HTMLTextAreaElement).value).toContain(
      'Give 0.3 ml by mouth once daily',
    );

    // The renewal needs a repeat window like the original (repeatable), so give the date.
    await user.type(screen.getByLabelText(/^valid until/i), '2027-03-01');
    await screen.findByText('Above range');
    for (const box of await screen.findAllByRole('checkbox', { name: /I have reviewed/i }))
      await user.click(box);
    await user.click(screen.getByRole('button', { name: 'Issue renewal' }));

    await waitFor(() => expect(onIssued).toHaveBeenCalledTimes(1));
    expect(body).not.toBeNull();
    expect((body as unknown as { visitId?: string | null }).visitId ?? null).toBeNull();
  });

  it('a medicine that cannot be matched in the formulary is said out loud', async () => {
    server.use(
      http.get(`${API}/api/staff/formulary`, () =>
        HttpResponse.json({ items: [], pageNumber: 1, pageSize: 50, totalCount: 0 }),
      ),
    );
    renderWithClient(
      <PrescriptionComposer
        catId={ids.catMittensId}
        visitId={null}
        renewFrom={ids.rxMittensActiveId}
        onIssued={vi.fn()}
      />,
    );
    expect(
      await screen.findByText(/could not match Meloxicam oral suspension in the formulary/),
    ).toBeInTheDocument();
  });
});
