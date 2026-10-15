import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { CorrectEntryDialog } from '@/components/anaesthesia/CorrectEntryDialog';
import { DrugEntryDialog, type DrugDialogContext } from '@/components/anaesthesia/DrugEntryDialog';
import { getAnaesthesiaLog } from '@/lib/api/anaesthesia-log';
import { surgeryFixtureIds as ids } from '@/mocks/handlers/surgeries';
import { server } from '@/mocks/server';
import { renderWithClient, resetSurgeryTest, signInAs } from '../surgery/helpers';

/**
 * HO-SRG-002 TEST gate (2026-10-14), mutation spot-checks. Two mutants survived the build's own tests:
 *  - the correction dialog passing NOW (instead of the corrected entry's instant) as the reference for the typed
 *    time, which `instantFromTime`'s own unit test cannot see because it passes the reference itself; and
 *  - the idempotency fingerprint widened by the route, which the build's resend test never exercised because its
 *    medicine has a single route (the `if (route.options.length > 2)` guard skipped the edit).
 */
vi.mock('next/navigation', async () =>
  (await import('../common/pharmacyTestUtils')).navigationMock(),
);
vi.setConfig({ testTimeout: 40_000 });

beforeEach(() => {
  resetSurgeryTest();
  signInAs(['Vet']);
});

const ZONE = 'Asia/Dhaka';
const context: DrugDialogContext = {
  surgeryId: ids.surgeryInTheatreId,
  catId: ids.catBiscuitId,
  zone: ZONE,
  weight: null,
  administrators: [{ id: ids.surgeonAkterId, name: 'Dr Akter' }],
};

describe('the correction dialog places the typed time around the ORIGINAL entry', () => {
  it('23:55 corrected to 00:05 is the next day of the entry, however far it is from now', async () => {
    const log = await getAnaesthesiaLog(ids.surgeryInTheatreId);
    const vitals = log.entries.find((e) => e.kind === 'Vitals' && !e.isCorrected)!;
    // An entry days in the past: "nearest to NOW" would put 00:05 on a different day from "nearest to the entry".
    const entry = { ...vitals, occurredOn: '2026-10-11T17:55:00Z' }; // 23:55 on 11 Oct in Dhaka
    const posted: Array<{ occurredOn: string }> = [];
    server.use(
      http.post(
        '*/api/staff/surgeries/:id/anaesthesia-log/:entryId/corrections',
        async ({ request }) => {
          posted.push((await request.json()) as { occurredOn: string });
          return HttpResponse.json({ entryId: 'new-correction' }, { status: 201 });
        },
      ),
    );
    const user = userEvent.setup();
    renderWithClient(<CorrectEntryDialog entry={entry} context={context} onClose={vi.fn()} />);
    await user.type(
      await screen.findByLabelText('Reason for the correction'),
      'The reading was taken after midnight',
    );
    fireEvent.change(screen.getByLabelText(/^Time \(/), { target: { value: '00:05' } });
    await user.click(screen.getByRole('button', { name: 'Review correction' }));
    await user.click(await screen.findByRole('button', { name: 'Add correction' }));
    await waitFor(() => expect(posted).toHaveLength(1));
    expect(posted[0]!.occurredOn).toBe('2026-10-11T18:05:00.000Z'); // 00:05 on 12 Oct: five minutes after the original
  });
});

describe('the idempotency key survives edits to every field the server does not compare', () => {
  it('a resend after changing the ROUTE and the time reuses the same entryId (a medicine with several routes)', async () => {
    const sent: string[] = [];
    let call = 0;
    server.use(
      http.post('*/api/staff/surgeries/:id/anaesthesia-log/drugs', async ({ request }) => {
        const body = (await request.json()) as { entryId: string; route: string };
        sent.push(`${body.entryId}|${body.route}`);
        call += 1;
        return call === 1
          ? HttpResponse.error() // committed on the server, the answer never arrives
          : HttpResponse.json(
              { entryId: body.entryId, registerEntryId: null, stockShortfall: false },
              { status: 201 },
            );
      }),
    );
    const user = userEvent.setup();
    renderWithClient(<DrugEntryDialog onClose={vi.fn()} context={context} />);
    await user.type(await screen.findByRole('combobox', { name: /^medicine/i }), 'Fusidic');
    await user.click(await screen.findByRole('option', { name: /Fusidic/ }));
    const unit = await screen.findByLabelText('Dose unit');
    await user.selectOptions(unit, (unit as HTMLSelectElement).options[1]!.value);
    await user.type(screen.getByRole('textbox', { name: 'Dose' }), '1');
    const route = screen.getByLabelText('Route') as HTMLSelectElement;
    expect(
      route.options.length,
      'the medicine has at least two routes, so the edit below really happens',
    ).toBeGreaterThan(2);
    await user.selectOptions(route, route.options[1]!.value);
    const radios = screen.queryAllByRole('radio');
    if (radios.length > 0) await user.click(radios[0]!);
    const draw = screen.queryByLabelText(/^Quantity drawn/);
    if (draw) await user.type(draw, '1');
    await user.click(screen.getByRole('button', { name: 'Save drug entry' }));
    await waitFor(() => expect(sent).toHaveLength(1));

    await user.selectOptions(route, route.options[2]!.value);
    fireEvent.change(screen.getByLabelText(/^Time given/), { target: { value: '03:15' } });
    await user.click(await screen.findByRole('button', { name: 'Save drug entry' }));
    await waitFor(() => expect(sent).toHaveLength(2));
    const [firstId, firstRoute] = sent[0]!.split('|');
    const [secondId, secondRoute] = sent[1]!.split('|');
    expect(secondRoute, 'the route really was edited between the two sends').not.toBe(firstRoute);
    expect(secondId).toBe(firstId);
  });
});
