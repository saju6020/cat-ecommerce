import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { AnaesthesiaTimeline } from '@/components/anaesthesia/AnaesthesiaTimeline';
import { RescheduleDialog } from '@/components/surgery/RescheduleDialog';
import { DrugEntryDialog } from '@/components/anaesthesia/DrugEntryDialog';
import { SurgeryBookingForm } from '@/components/surgery/SurgeryBookingForm';
import { SurgeryCampaignForm } from '@/components/surgery-campaigns/SurgeryCampaignForm';
import { SurgeryDayList } from '@/components/surgery/SurgeryDayList';
import { SurgicalReportEditor } from '@/components/surgical-report/SurgicalReportEditor';
import { getAnaesthesiaLog } from '@/lib/api/anaesthesia-log';
import { getSurgery, getSurgeryDay } from '@/lib/api/surgeries';
import { listSurgeryCampaigns } from '@/lib/api/surgery-campaigns';
import { addDaysIso } from '@/lib/format/appointment';
import { todayInZone } from '@/lib/format/time';
import { drugFingerprint, instantFromTime } from '@/lib/validation/anaesthesia';
import { surgeryFixtureIds as ids } from '@/mocks/handlers/surgeries';
import { server } from '@/mocks/server';
import { navigation, renderWithClient, resetSurgeryTest, signInAs, surgeryAs } from './helpers';

vi.mock('next/navigation', async () =>
  (await import('../common/pharmacyTestUtils')).navigationMock(),
);
vi.setConfig({ testTimeout: 40_000 });

beforeEach(() => {
  resetSurgeryTest();
  signInAs(['Reception']);
});
afterEach(() => vi.useRealTimers());

const ZONE = 'Asia/Dhaka';

describe('CODE HIGH — report text survives a tab switch', () => {
  const props = {
    surgeryId: ids.surgeryRecoveryId,
    zone: ZONE,
    report: null,
    procedureName: 'Mass removal',
    surgeonId: ids.surgeonRahmanId,
    assistantId: ids.nurseTanvirId,
    surgeonName: 'Dr Rahman',
    assistantName: 'Nurse Tanvir',
    canFinalise: true,
    onReload: vi.fn(),
  };

  it('saves nothing before 5 s, saves at 5 s, and flushes an unsaved change when the editor unmounts', async () => {
    signInAs(['Vet']);
    const puts: Array<Record<string, unknown>> = [];
    server.events.on('request:start', async ({ request }) => {
      if (request.method === 'PUT' && request.url.endsWith('/report')) {
        puts.push((await request.clone().json()) as Record<string, unknown>);
      }
    });
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    const view = renderWithClient(<SurgicalReportEditor {...props} />);
    fireEvent.change(screen.getByLabelText('Findings'), {
      target: { value: 'Small lipoma, left flank' },
    });
    await act(async () => {
      vi.advanceTimersByTime(4_000);
    });
    expect(puts).toHaveLength(0); // still inside the 5 s window
    // The vet switches tab: the editor unmounts with the text still unsaved.
    view.unmount();
    vi.useRealTimers();
    await waitFor(() => expect(puts).toHaveLength(1));
    expect(puts[0]).toMatchObject({ findings: 'Small lipoma, left flank' });
    server.events.removeAllListeners();
  });

  it('does not save again on unmount when nothing is waiting', async () => {
    signInAs(['Vet']);
    const puts: unknown[] = [];
    server.events.on('request:start', ({ request }) => {
      if (request.method === 'PUT' && request.url.endsWith('/report')) puts.push(1);
    });
    const view = renderWithClient(<SurgicalReportEditor {...props} />);
    view.unmount();
    await new Promise((resolve) => setTimeout(resolve, 200));
    expect(puts).toHaveLength(0);
    server.events.removeAllListeners();
  });
});

describe('CODE MEDIUM — booking choices that cannot be shown are dropped', () => {
  function prefill(over: Record<string, string>) {
    navigation.pathname = '/staff/surgeries/new';
    navigation.search = new URLSearchParams({
      customerId: ids.customerFarhanaId,
      catId: ids.catPepperId,
      serviceId: ids.serviceSpayId,
      date: addDaysIsoFromToday(5),
      ...over,
    });
  }
  const lastUrl = () => String(navigation.replace.mock.calls.at(-1)?.[0] ?? '');

  it('choosing the assistant as surgeon clears the assistant', async () => {
    prefill({ assistantId: ids.surgeonRahmanId });
    const user = userEvent.setup();
    renderWithClient(<SurgeryBookingForm zone={ZONE} />);
    const surgeon = await screen.findByLabelText('Surgeon');
    await waitFor(() => expect(within(surgeon).getAllByRole('option').length).toBeGreaterThan(1));
    await user.selectOptions(surgeon, ids.surgeonRahmanId);
    await waitFor(() => expect(lastUrl()).toContain(`surgeonId=${ids.surgeonRahmanId}`));
    expect(lastUrl()).not.toContain('assistantId');
  });

  it('a deep-linked assistant who is also the surgeon is dropped', async () => {
    prefill({ surgeonId: ids.surgeonRahmanId, assistantId: ids.surgeonRahmanId });
    renderWithClient(<SurgeryBookingForm zone={ZONE} />);
    await waitFor(() => expect(navigation.replace).toHaveBeenCalled());
    expect(lastUrl()).not.toContain('assistantId');
    expect(lastUrl()).toContain('surgeonId');
  });

  it('a deep-linked theatre that is not a theatre room is dropped', async () => {
    prefill({ theatreId: ids.surgeonAkterId });
    renderWithClient(<SurgeryBookingForm zone={ZONE} />);
    await waitFor(() => expect(navigation.replace).toHaveBeenCalled(), { timeout: 15_000 });
    expect(lastUrl()).not.toContain('theatreId');
  });
});

function addDaysIsoFromToday(days: number): string {
  return addDaysIso(todayInZone(ZONE), days);
}

describe('CODE MEDIUM — the idempotency key', () => {
  it('the fingerprint is exactly the four fields the server compares', () => {
    const base = { medicineId: 'm', batchId: 'b', quantityDrawn: 1, doseAmount: 0.2 };
    expect(drugFingerprint(base)).toBe(drugFingerprint({ ...base }));
    for (const change of [
      { medicineId: 'n' },
      { batchId: 'c' },
      { quantityDrawn: 2 },
      { doseAmount: 0.3 },
    ]) {
      expect(drugFingerprint({ ...base, ...change })).not.toBe(drugFingerprint(base));
    }
  });

  it('a lost-response resend after editing the route and the time reuses the same entryId', async () => {
    signInAs(['Vet']);
    const user = userEvent.setup();
    const ids_: string[] = [];
    let call = 0;
    server.use(
      http.post('*/api/staff/surgeries/:id/anaesthesia-log/drugs', async ({ request }) => {
        const body = (await request.json()) as { entryId: string };
        ids_.push(body.entryId);
        call += 1;
        // The first attempt commits on the server but its response never arrives.
        return call === 1
          ? HttpResponse.error()
          : HttpResponse.json(
              { entryId: body.entryId, registerEntryId: null, stockShortfall: false },
              { status: 201 },
            );
      }),
    );
    renderWithClient(
      <DrugEntryDialog
        onClose={vi.fn()}
        context={{
          surgeryId: ids.surgeryInTheatreId,
          catId: ids.catBiscuitId,
          zone: ZONE,
          weight: null,
          administrators: [{ id: ids.surgeonAkterId, name: 'Dr Akter' }],
        }}
      />,
    );
    await user.type(await screen.findByRole('combobox', { name: /^medicine/i }), 'Melox');
    await user.click(await screen.findByRole('option', { name: /Meloxicam/ }));
    await user.selectOptions(await screen.findByLabelText('Dose unit'), 'Mg');
    await user.type(screen.getByRole('textbox', { name: 'Dose' }), '0.2');
    const route = screen.getByLabelText('Route') as HTMLSelectElement;
    await user.selectOptions(route, route.options[1]!.value);
    await user.click((await screen.findAllByRole('radio'))[0]!);
    await user.type(await screen.findByLabelText(/^Quantity drawn/), '0.1');
    await user.click(screen.getByRole('button', { name: 'Save drug entry' }));
    await waitFor(() => expect(ids_).toHaveLength(1));
    // The vet changes the route and the time, and sends it again.
    if (route.options.length > 2) await user.selectOptions(route, route.options[2]!.value);
    fireEvent.change(screen.getByLabelText(/^Time given/), { target: { value: '03:15' } });
    await user.click(await screen.findByRole('button', { name: 'Save drug entry' }));
    await waitFor(() => expect(ids_).toHaveLength(2));
    expect(ids_[1]).toBe(ids_[0]);
  });
});

describe('CODE LOW — a time just after or before midnight', () => {
  it('logging 23:55 at 00:05 is last night, not tomorrow', () => {
    const now = Date.parse('2026-10-12T18:05:00Z'); // 00:05 on 13 Oct in Dhaka
    expect(instantFromTime('23:55', ZONE, now)).toBe('2026-10-12T17:55:00.000Z');
    expect(instantFromTime('00:03', ZONE, now)).toBe('2026-10-12T18:03:00.000Z');
  });

  it('a correction takes the nearest instant around the ORIGINAL entry', () => {
    const entry = Date.parse('2026-10-11T17:50:00Z'); // 23:50 on 11 Oct in Dhaka
    expect(instantFromTime('00:10', ZONE, entry)).toBe('2026-10-11T18:10:00.000Z'); // 00:10 on the 12th
    expect(instantFromTime('23:30', ZONE, entry)).toBe('2026-10-11T17:30:00.000Z');
    const evening = Date.parse('2026-10-11T16:30:00Z'); // 22:30 on 11 Oct
    expect(instantFromTime('22:30', ZONE, evening)).toBe('2026-10-11T16:30:00.000Z');
  });
});

describe('CODE MEDIUM — reschedule with a procedure that cannot be found', () => {
  it('says so instead of showing a picker with no procedure control', async () => {
    const surgery = await surgeryAs(ids.surgeryTomorrowId, ['Reception']);
    server.use(http.get('*/api/staff/surgeries/services', () => HttpResponse.json([])));
    renderWithClient(<RescheduleDialog surgery={surgery} onClose={vi.fn()} />);
    expect(
      await screen.findByText(/can no longer be found in the list of theatre procedures/),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Review' })).toBeDisabled();
    expect(screen.queryByRole('group', { name: /available starts/i })).not.toBeInTheDocument();
  });
});

describe('CODE MEDIUM — campaign prices are parsed strictly', () => {
  it.each(['1e3', '800.505', '-5', '8,00', ' '])('refuses %j', async (value) => {
    signInAs(['Administrator']);
    const user = userEvent.setup();
    const page = await listSurgeryCampaigns({});
    const campaign = page.items.find((c) => c.id === ids.campaignExhaustedId)!;
    let put = 0;
    server.use(
      http.put('*/api/staff/surgery-campaigns/:id', () => {
        put += 1;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    renderWithClient(<SurgeryCampaignForm campaign={campaign} onClose={vi.fn()} />);
    const price = await screen.findByLabelText('Spay');
    await user.clear(price);
    await user.type(price, value);
    await user.click(screen.getByRole('button', { name: 'Save campaign' }));
    expect(
      await screen.findByText(/Enter prices as plain amounts such as 800 or 800\.50/),
    ).toBeInTheDocument();
    expect(put).toBe(0);
  });

  it('accepts 800 and 800.50', async () => {
    signInAs(['Administrator']);
    const user = userEvent.setup();
    const page = await listSurgeryCampaigns({});
    const campaign = page.items.find((c) => c.id === ids.campaignExhaustedId)!;
    const bodies: Array<{ prices: Array<{ amount: number }> }> = [];
    server.use(
      http.put('*/api/staff/surgery-campaigns/:id', async ({ request }) => {
        bodies.push((await request.json()) as (typeof bodies)[number]);
        return new HttpResponse(null, { status: 204 });
      }),
    );
    renderWithClient(<SurgeryCampaignForm campaign={campaign} onClose={vi.fn()} />);
    const price = await screen.findByLabelText('Spay');
    await user.clear(price);
    await user.type(price, '800.50');
    await user.click(screen.getByRole('button', { name: 'Save campaign' }));
    await waitFor(() => expect(bodies).toHaveLength(1));
    expect(bodies[0]!.prices[0]!.amount).toBe(800.5);
  });
});

describe('SEC LOW — path ids are encoded', () => {
  it('a crafted id cannot change the API path', async () => {
    const urls: string[] = [];
    server.events.on('request:start', ({ request }) => urls.push(request.url));
    server.use(http.get('*/api/staff/surgeries/*', () => new HttpResponse(null, { status: 404 })));
    await getSurgery('a/../b?x=1#y').catch(() => undefined);
    expect(urls.some((u) => u.includes('/api/staff/surgeries/a%2F..%2Fb%3Fx%3D1%23y'))).toBe(true);
    server.events.removeAllListeners();
  });
});

describe('CODE LOW — timeline and day list', () => {
  it('"Last vitals" ignores a struck-through entry', async () => {
    signInAs(['Vet']);
    const log = await getAnaesthesiaLog(ids.surgeryInTheatreId);
    const vitals = log.entries.filter((e) => e.kind === 'Vitals');
    const latest = vitals.at(-1)!;
    // Strike out the newest vitals: the figure must come from the one before it.
    const struck = log.entries.map((e) => (e.id === latest.id ? { ...e, isCorrected: true } : e));
    const nowMs = new Date(latest.occurredOn).getTime() + 60 * 60_000; // an hour after the newest
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(nowMs);
    renderWithClient(
      <AnaesthesiaTimeline
        log={{ ...log, entries: struck }}
        canCorrect={() => false}
        onCorrect={vi.fn()}
      />,
    );
    const previous = vitals.filter((e) => e.id !== latest.id).at(-1)!;
    const expected = Math.round((nowMs - new Date(previous.occurredOn).getTime()) / 60_000);
    expect(screen.getByText(new RegExp(`Last vitals ${expected} min ago`))).toBeInTheDocument();
  });

  it('two theatres with the same name stay two groups', async () => {
    const day = await getSurgeryDay(todayInZone(ZONE));
    const first = day.surgeries[0]!;
    const second = {
      ...day.surgeries.find((s) => s.theatre.id !== first.theatre.id)!,
      theatre: { id: 'other-theatre', name: first.theatre.name },
    };
    renderWithClient(
      <SurgeryDayList
        surgeries={[first, second]}
        zone={ZONE}
        isLoading={false}
        isError={false}
        onRetry={vi.fn()}
      />,
    );
    expect(screen.getAllByRole('region', { name: first.theatre.name })).toHaveLength(2);
  });
});

describe('R5 — a failed services fetch is an error, not "no longer found"', () => {
  it('shows the load error with Try again', async () => {
    const surgery = await surgeryAs(ids.surgeryTomorrowId, ['Reception']);
    server.use(
      http.get('*/api/staff/surgeries/services', () => new HttpResponse(null, { status: 500 })),
    );
    renderWithClient(<RescheduleDialog surgery={surgery} onClose={vi.fn()} />);
    expect(
      await screen.findByText(/couldn’t load the list of theatre procedures/, undefined, {
        timeout: 10_000,
      }),
    ).toBeInTheDocument();
    expect(screen.queryByText(/can no longer be found/)).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Try again' })).toBeInTheDocument();
  });
});
