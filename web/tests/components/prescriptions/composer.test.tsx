import { beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { PrescriptionComposer } from '@/components/prescriptions/PrescriptionComposer';
import { emrFixtureIds } from '@/mocks/handlers/visits';
import { pharmacyFixtureIds as ids, resetPharmacyMockData } from '@/mocks/handlers/pharmacy';
import { renderWithClient, resetNavigation } from '../common/pharmacyTestUtils';

vi.mock('next/navigation', async () =>
  (await import('../common/pharmacyTestUtils')).navigationMock(),
);

vi.setConfig({ testTimeout: 30_000 });

let roles = ['Vet'];
vi.mock('@/lib/hooks/useStaffRoles', () => ({
  useStaffRoles: () => ({
    roleIds: roles,
    ready: true,
    has: (...wanted: string[]) => wanted.some((role) => roles.includes(role)),
  }),
}));

const API = 'https://localhost:7099';

beforeEach(() => {
  resetPharmacyMockData();
  resetNavigation();
  roles = ['Vet'];
});

type User = ReturnType<typeof userEvent.setup>;

async function pickMedicine(user: User, term: string, optionName: RegExp) {
  await user.type(await screen.findByRole('combobox', { name: /^medicine/i }), term);
  await user.click(await screen.findByRole('option', { name: optionName }, { timeout: 4000 }));
}

/** Fill the whole form for a per-ml suspension; the dose goes in LAST so one check follows. */
async function fillMeloxicam(user: User, dose = '0.4') {
  await pickMedicine(user, 'melox', /Meloxicam oral suspension/);
  await screen.findByText(/oral liquid/i);
  await user.selectOptions(screen.getByLabelText(/^frequency/i), 'OnceDaily');
  await user.type(screen.getByRole('textbox', { name: /^duration/i }), '7');
  await user.type(screen.getByRole('textbox', { name: /^quantity per supply/i }), '10');
  await user.type(screen.getByLabelText(/^instructions/i), 'Give by mouth once daily.');
  await user.selectOptions(screen.getByRole('combobox', { name: 'Dose unit' }), 'Ml');
  await user.type(screen.getByRole('textbox', { name: /^dose/i }), dose);
}

function renderComposer(props: Partial<React.ComponentProps<typeof PrescriptionComposer>> = {}) {
  const onIssued = vi.fn();
  renderWithClient(
    <PrescriptionComposer
      catId={ids.catWhiskersId}
      visitId={emrFixtureIds.visitOpenId}
      onIssued={onIssued}
      {...props}
    />,
  );
  return { onIssued };
}

describe('U-A1 — the visit', () => {
  it('without a visit there is no form and the vet is told why', () => {
    renderComposer({ visitId: null });
    expect(
      screen.getByText('Prescriptions are issued from an open visit. Open the cat’s visit first.'),
    ).toBeInTheDocument();
    expect(screen.queryByRole('form', { name: 'Prescription' })).not.toBeInTheDocument();
  });

  it('a finalised visit says so and offers no form', async () => {
    renderComposer({ catId: ids.catMittensId, visitId: emrFixtureIds.visitFinalisedId });
    expect(
      await screen.findByText(
        'This visit has been finalised — prescriptions can no longer be added to it.',
      ),
    ).toBeInTheDocument();
    expect(screen.queryByRole('form', { name: 'Prescription' })).not.toBeInTheDocument();
  });

  it('a 400 VisitNotOpen on issue shows the same message', async () => {
    const user = userEvent.setup();
    server.use(
      http.post(`${API}/api/staff/cats/:catId/prescriptions`, () =>
        HttpResponse.json(
          {
            isValid: false,
            errors: [
              {
                propertyName: 'VisitId',
                errorCode: 'VisitNotOpen',
                errorMessage: 'A prescription can only be written from an open visit.',
                resourceName: 'Pharmacy',
              },
            ],
          },
          { status: 400 },
        ),
      ),
    );
    renderComposer();
    await fillMeloxicam(user);
    await screen.findByText('Above range');
    for (const box of await screen.findAllByRole('checkbox', { name: /I have reviewed/i }))
      await user.click(box);
    await user.click(screen.getByRole('button', { name: 'Issue prescription' }));
    expect(
      await screen.findByText(
        'This visit has been finalised — prescriptions can no longer be added to it.',
      ),
    ).toBeInTheDocument();
  });
});

describe('U-A12 — units are chosen, never assumed', () => {
  it('a tablet medicine offers tablet and mass units and starts with none chosen', async () => {
    const user = userEvent.setup();
    renderComposer();
    await pickMedicine(user, 'amox', /Amoxicillin tablets/);
    await screen.findByText(/^Tablet$/);
    const select = screen.getByRole('combobox', { name: 'Dose unit' }) as HTMLSelectElement;
    await waitFor(() => expect(select.options.length).toBeGreaterThan(1));
    expect(select.value).toBe('');
    expect([...select.options].map((o) => o.textContent)).toEqual(
      expect.arrayContaining(['Choose unit', 'tablets', 'mg', 'mcg', 'g']),
    );
    expect([...select.options].map((o) => o.textContent)).not.toContain('ml');
  });
});

describe('the safety panel and acknowledgements (US-PHM-02, 03)', () => {
  it('U-A2 — one check per pause, not per keystroke; U-A3 — above range shows mg/kg and mg and needs a tick', async () => {
    const user = userEvent.setup();
    const checks: string[] = [];
    server.events.on('request:start', ({ request }) => {
      if (request.url.endsWith('/prescriptions/safety-check')) checks.push(request.url);
    });
    const { onIssued } = renderComposer();
    await fillMeloxicam(user, '0.4');
    expect(await screen.findByText('Above range')).toBeInTheDocument();
    expect(checks).toHaveLength(1);

    const panel = screen.getByRole('complementary', { name: 'Safety check' });
    expect(within(panel).getAllByText(/mg\/kg/).length).toBeGreaterThan(0);
    expect(within(panel).getByText('0.60 mg', { exact: false })).toBeInTheDocument();
    expect(panel).toHaveTextContent('Expected 0.05–0.10 mg/kg');

    await user.click(screen.getByRole('button', { name: 'Issue prescription' }));
    expect(
      (await screen.findAllByText('Tick to confirm you have reviewed this warning.')).length,
    ).toBeGreaterThan(0);
    expect(onIssued).not.toHaveBeenCalled();

    for (const box of screen.getAllByRole('checkbox', { name: /I have reviewed/i }))
      await user.click(box);
    await user.click(screen.getByRole('button', { name: 'Issue prescription' }));
    await waitFor(() => expect(onIssued).toHaveBeenCalledTimes(1));
    expect(onIssued.mock.calls[0]?.[1]).toMatch(/^RX-/);
  });

  it('U-A6 — Major and Minor interactions both show their severity; only the Major one needs a tick', async () => {
    const user = userEvent.setup();
    renderComposer();
    await fillMeloxicam(user);
    await screen.findByText('Above range');
    const panel = screen.getByRole('complementary', { name: 'Safety check' });
    expect(within(panel).getByText('Major')).toBeInTheDocument();
    expect(within(panel).getByText('Minor')).toBeInTheDocument();
    expect(within(panel).getByText(/No acknowledgement needed/)).toBeInTheDocument();
    const boxes = screen.getAllByRole('checkbox', { name: /I have reviewed/i });
    expect(boxes).toHaveLength(2); // the Major interaction and the dose — the Minor one needs no tick
    expect(boxes.map((b) => b.closest('label')?.textContent)).toEqual(
      expect.arrayContaining([
        expect.stringContaining('Interaction — Prednisolone'),
        expect.stringContaining('Dose above range'),
      ]),
    );
  });

  it('U-A7 — a new fingerprint clears the ticks and says so', async () => {
    const user = userEvent.setup();
    renderComposer();
    await fillMeloxicam(user, '0.4');
    await screen.findByText('Above range');
    for (const box of screen.getAllByRole('checkbox', { name: /I have reviewed/i }))
      await user.click(box);
    expect(
      screen
        .getAllByRole('checkbox', { name: /I have reviewed/i })
        .every((b) => (b as HTMLInputElement).checked),
    ).toBe(true);

    await user.type(screen.getByRole('textbox', { name: /^dose/i }), '5'); // 0.45 ml — different warning text
    await waitFor(() =>
      expect(screen.getByText('Warnings changed — review again.')).toBeInTheDocument(),
    );
    expect(
      screen
        .getAllByRole('checkbox', { name: /I have reviewed/i })
        .every((b) => !(b as HTMLInputElement).checked),
    ).toBe(true);
  });

  it('U-A8 — a 409 replaces the panel with the returned assessment and keeps the form', async () => {
    const user = userEvent.setup();
    server.use(
      http.post(`${API}/api/staff/cats/:catId/prescriptions`, () =>
        HttpResponse.json(
          {
            isValid: false,
            errors: [
              {
                propertyName: 'AcknowledgedWarningsFingerprint',
                errorCode: 'PrescriptionWarningsChanged',
                errorMessage: 'The warnings changed.',
                resourceName: 'Pharmacy',
              },
            ],
            assessment: {
              weight: {
                requirement: 'Satisfied',
                latest: {
                  weight: { amount: '4.20', unit: 'Kg' },
                  recordedOn: '2026-09-01',
                  ageDays: 20,
                },
              },
              doseCheck: {
                outcome: 'WithinRange',
                doseMg: { amount: '0.30', unit: 'Mg' },
                doseMgPerKg: '0.0714',
                expectedRange: null,
              },
              warnings: [
                {
                  kind: 'Interaction',
                  blocking: false,
                  requiresAcknowledgement: true,
                  severity: 'Major',
                  subject: 'Interaction — Newdrug',
                  detail: 'A brand new interaction.',
                },
              ],
              information: [],
              contraindications: '',
              hasRecordedAllergies: true,
              fingerprint: 'fp-new',
            },
          },
          { status: 409 },
        ),
      ),
    );
    const { onIssued } = renderComposer();
    await fillMeloxicam(user, '0.4');
    await screen.findByText('Above range');
    for (const box of screen.getAllByRole('checkbox', { name: /I have reviewed/i }))
      await user.click(box);
    await user.click(screen.getByRole('button', { name: 'Issue prescription' }));

    expect((await screen.findAllByText('Interaction — Newdrug')).length).toBeGreaterThan(0);
    expect(
      screen.getByText('Something changed — please review the warnings again.'),
    ).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: /^dose/i })).toHaveValue('0.4');
    expect(screen.getByLabelText(/^instructions/i)).toHaveValue('Give by mouth once daily.');
    expect(onIssued).not.toHaveBeenCalled();
  });

  it('U-A9 — when the check fails the panel says so and issue is blocked', async () => {
    const user = userEvent.setup();
    server.use(
      http.post(`${API}/api/staff/cats/:catId/prescriptions/safety-check`, () =>
        HttpResponse.json({ error: 'boom' }, { status: 500 }),
      ),
    );
    renderComposer();
    await fillMeloxicam(user);
    expect(
      await screen.findAllByText('The safety check could not run. You cannot issue until it does.'),
    ).not.toHaveLength(0);
    expect(screen.getByRole('button', { name: 'Issue prescription' })).toBeDisabled();
  });

  it('U-A10 and U-A11 — a fixed-dose medicine is not checkable (no tick) and the allergy limitation is stated', async () => {
    const user = userEvent.setup();
    renderComposer({ catId: ids.catMittensId, visitId: emrFixtureIds.visitTemplateId });
    await pickMedicine(user, 'fusid', /Fusidic acid gel/);
    await screen.findByText('FUS-GEL-1');
    await user.selectOptions(screen.getByLabelText(/^frequency/i), 'TwiceDaily');
    await user.type(screen.getByRole('textbox', { name: /^duration/i }), '10');
    await user.type(screen.getByRole('textbox', { name: /^quantity per supply/i }), '1');
    await user.type(screen.getByLabelText(/^instructions/i), 'Apply a thin layer.');
    await user.selectOptions(screen.getByRole('combobox', { name: 'Dose unit' }), 'Application');
    await user.type(screen.getByRole('textbox', { name: /^dose/i }), '1');
    const panel = screen.getByRole('complementary', { name: 'Safety check' });
    expect(await within(panel).findByText('Dose could not be checked')).toBeInTheDocument();
    expect(screen.queryByRole('checkbox', { name: /I have reviewed/i })).not.toBeInTheDocument();
    expect(panel).toHaveTextContent(
      'Allergy checks compare recorded allergies with this medicine’s ingredients and classes. A misspelt allergy may not be matched.',
    );
    expect(panel).toHaveTextContent('No allergies recorded.');
  });

  it('U-A5 — an allergy conflict is listed first as Blocking and needs a reason', async () => {
    const user = userEvent.setup();
    const original = (await (await fetch(`${API}/api/cats/${ids.catWhiskersId}`)).json()) as {
      allergies: Array<{
        allergen: string;
        reaction: string;
        severity: string;
        recordedOn: string;
      }>;
    };
    const put = (allergies: unknown[]) =>
      fetch(`${API}/api/cats/${ids.catWhiskersId}/medical-profile`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          allergies,
          chronicConditions: [],
          medications: [],
          dietaryNotes: null,
          behaviouralNotes: null,
        }),
      });
    await put([
      ...original.allergies,
      { allergen: 'Penicillin', reaction: 'Hives', severity: 'Severe', recordedOn: '2026-01-01' },
    ]);
    try {
      const { onIssued } = renderComposer();
      await pickMedicine(user, 'amox', /Amoxicillin tablets/);
      await screen.findByText(/^Tablet$/);
      await user.selectOptions(screen.getByLabelText(/^frequency/i), 'TwiceDaily');
      await user.type(screen.getByRole('textbox', { name: /^duration/i }), '5');
      await user.type(screen.getByRole('textbox', { name: /^quantity per supply/i }), '10');
      await user.type(screen.getByLabelText(/^instructions/i), 'One tablet twice daily.');
      await user.selectOptions(screen.getByRole('combobox', { name: 'Dose unit' }), 'Tablet');
      await user.type(screen.getByRole('textbox', { name: /^dose/i }), '1');

      const panel = screen.getByRole('complementary', { name: 'Safety check' });
      expect(await within(panel).findByText('Blocking')).toBeInTheDocument();
      const warnings = within(panel).getAllByRole('listitem');
      expect(warnings[0]).toHaveTextContent('Allergy — Penicillin');

      await user.click(screen.getByRole('checkbox', { name: /I have reviewed: Allergy/i }));
      await user.click(screen.getByRole('button', { name: 'Issue prescription' }));
      expect(
        await screen.findByText('Say why you are prescribing despite the allergy.'),
      ).toBeInTheDocument();
      expect(onIssued).not.toHaveBeenCalled();

      await user.type(
        screen.getByLabelText(/why are you prescribing despite the allergy/i),
        'Culture-proven; no alternative.',
      );
      await user.click(screen.getByRole('button', { name: 'Issue prescription' }));
      await waitFor(() => expect(onIssued).toHaveBeenCalledTimes(1));
    } finally {
      await put(original.allergies);
    }
  });
});

describe('U-A4 — a stale weight', () => {
  it('shows the refusal with the latest weight and date, records a weight from the panel and re-checks', async () => {
    const user = userEvent.setup();
    renderComposer({ catId: ids.catTigerId, visitId: emrFixtureIds.visitDraftNewerId });
    await pickMedicine(user, 'melox', /Meloxicam oral suspension/);
    await screen.findByText(/oral liquid/i);
    await user.selectOptions(screen.getByLabelText(/^frequency/i), 'OnceDaily');
    await user.selectOptions(screen.getByRole('combobox', { name: 'Dose unit' }), 'Ml');
    await user.type(screen.getByRole('textbox', { name: /^dose/i }), '0.3');

    const panel = screen.getByRole('complementary', { name: 'Safety check' });
    expect(await within(panel).findByText(/The latest weight is out of date/)).toBeInTheDocument();
    expect(panel).toHaveTextContent('5.80');
    expect(screen.getByRole('button', { name: 'Issue prescription' })).toBeDisabled();

    await user.click(within(panel).getByRole('button', { name: 'Record weight' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText(/^weight/i), '5.9');
    await user.click(within(dialog).getByRole('button', { name: 'Record weight' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(await within(panel).findByText(/Latest weight/)).toBeInTheDocument();
  });
});

describe('A15 — only a vet prescribes', () => {
  it('a non-vet sees the permission state before typing anything', () => {
    roles = ['Reception'];
    renderComposer();
    expect(screen.getByText(/You don.t have permission to do this/)).toBeInTheDocument();
    expect(screen.queryByRole('form', { name: 'Prescription' })).not.toBeInTheDocument();
  });
});
