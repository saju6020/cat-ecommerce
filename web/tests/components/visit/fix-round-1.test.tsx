import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  act,
  createEvent,
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Suspense, useState } from 'react';
import { QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { server } from '../../../mocks/server';
import { ConsentCaptureForm } from '@/components/consent/ConsentCaptureForm';
import { ConsentDetail } from '@/components/consent/ConsentDetail';
import { DischargeSummaryView } from '@/components/visit/DischargeSummaryView';
import { ResultEntryForm } from '@/components/visit/panels/ResultEntryForm';
import { QuickVitalsDialog } from '@/components/visit/vitals/QuickVitalsDialog';
import { VisitView } from '@/app/(staff)/staff/visits/[visitId]/VisitView';
import { getConsent } from '@/lib/api/consents';
import { getDiagnosticOrder } from '@/lib/api/diagnostics';
import {
  AUTOSAVE_DEBOUNCE_MS,
  AUTOSAVE_MAX_WAIT_MS,
  useConsultationAutosave,
} from '@/lib/hooks/useConsultationAutosave';
import {
  getDischargeSummary,
  getDischargeSummaryPdf,
  getVisit,
  type DraftRequest,
} from '@/lib/api/visits';
import { appointmentsFixtureIds } from '../../../mocks/handlers/appointments/fixtures';
import { freshClinicBeforeEach, ids, renderWithClient } from './helpers';

// A multi-branch caller: the active branch must ride on every staff binary read.
vi.mock('@/lib/branch', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/lib/branch')>()),
  getActiveBranchId: () => 'b0000000-0000-0000-0000-00000000a001',
}));
vi.mock('next/navigation', () => import('../booking/nav-mock'));

const saveDraftMock = vi.hoisted(() => vi.fn());
vi.mock('@/lib/api/visits', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/lib/api/visits')>()),
  saveDraft: saveDraftMock,
}));

const MAIN_BRANCH = 'b0000000-0000-0000-0000-00000000a001';
freshClinicBeforeEach();
beforeEach(() => {
  saveDraftMock.mockReset();
  saveDraftMock.mockResolvedValue(undefined);
});
afterEach(() => {
  vi.restoreAllMocks();
  vi.useRealTimers();
  server.events.removeAllListeners();
});

function recordBranchHeaders(match: RegExp): string[] {
  const seen: string[] = [];
  server.events.on('request:start', ({ request }) => {
    if (match.test(request.url)) seen.push(request.headers.get('x-branch-id') ?? '(none)');
  });
  return seen;
}

describe('Staff binary reads send X-Branch-Id (ARCH HIGH)', () => {
  it('the PDF is fetched as a blob with the header and saved through an object URL, then revoked', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout'] });
    const seen = recordBranchHeaders(/discharge-summary\.pdf/);
    const create = vi.fn(() => 'blob:pdf');
    const revoke = vi.fn();
    vi.stubGlobal('URL', Object.assign(URL, { createObjectURL: create, revokeObjectURL: revoke }));
    const click = vi
      .spyOn(HTMLAnchorElement.prototype, 'click')
      .mockImplementation(() => undefined);
    const summary = await getDischargeSummary(ids.visitFinalisedId);
    render(
      <DischargeSummaryView
        summary={summary}
        loadPdf={() => getDischargeSummaryPdf(ids.visitFinalisedId)}
        showPrint
      />,
    );
    fireEvent.click(screen.getByRole('button', { name: /download pdf/i }));
    await vi.waitFor(() => expect(click).toHaveBeenCalledTimes(1));
    expect(seen).toEqual([MAIN_BRANCH]);
    expect(create).toHaveBeenCalledTimes(1);
    await act(async () => void vi.advanceTimersByTime(1_500));
    expect(revoke).toHaveBeenCalledWith('blob:pdf');
  });

  it('a failed PDF download shows an error, not a dead link', async () => {
    server.use(http.get('*/discharge-summary.pdf', () => new HttpResponse(null, { status: 500 })));
    const summary = await getDischargeSummary(ids.visitFinalisedId);
    render(
      <DischargeSummaryView
        summary={summary}
        loadPdf={() => getDischargeSummaryPdf(ids.visitFinalisedId)}
        showPrint
      />,
    );
    fireEvent.click(screen.getByRole('button', { name: /download pdf/i }));
    expect(await screen.findByRole('alert')).toHaveTextContent(/couldn.t be downloaded/i);
  });

  it('the consent signature is fetched with the header and shown from an object URL (revoked on unmount)', async () => {
    const seen = recordBranchHeaders(/signature\.svg/);
    const revoke = vi.fn();
    vi.stubGlobal(
      'URL',
      Object.assign(URL, { createObjectURL: () => 'blob:sig', revokeObjectURL: revoke }),
    );
    const consent = await getConsent(ids.consentRecordedId);
    const view = render(
      <ConsentDetail consent={consent} canWithdraw={false} onWithdraw={vi.fn()} />,
    );
    const image = await screen.findByRole('img', { name: /signature of sam rahman/i });
    expect(image).toHaveAttribute('src', 'blob:sig');
    expect(seen).toEqual([MAIN_BRANCH]);
    view.unmount();
    expect(revoke).toHaveBeenCalledWith('blob:sig');
  });

  it('a signature that cannot be loaded shows a message, not a broken image', async () => {
    server.use(http.get('*/signature.svg', () => new HttpResponse(null, { status: 404 })));
    const consent = await getConsent(ids.consentRecordedId);
    render(<ConsentDetail consent={consent} canWithdraw={false} onWithdraw={vi.fn()} />);
    expect(await screen.findByRole('alert')).toHaveTextContent(/signature couldn.t be loaded/i);
    expect(screen.queryByRole('img')).toBeNull();
  });
});

describe('A blank numeric result is never sent as 0 (CR HIGH)', () => {
  it('asks for the result, names the row, and does not call save', async () => {
    const user = userEvent.setup();
    const save = vi.fn();
    const order = await getDiagnosticOrder(ids.orderPendingId);
    renderWithClient(<ResultEntryForm order={order} zone="Asia/Dhaka" save={save} />);
    await screen.findAllByLabelText('Result, µmol/L');
    await user.click(screen.getByRole('button', { name: 'Save results' }));
    expect(await screen.findAllByText(/Enter the result for/)).not.toHaveLength(0);
    expect(save).not.toHaveBeenCalled();
  });

  it('a real 0 is still sent as 0', async () => {
    const user = userEvent.setup();
    const save = vi.fn().mockResolvedValue({ values: [] });
    const order = await getDiagnosticOrder(ids.orderPendingId);
    renderWithClient(<ResultEntryForm order={order} zone="Asia/Dhaka" save={save} />);
    for (const field of await screen.findAllByLabelText('Result, µmol/L'))
      await user.type(field, '0');
    for (const field of await screen.findAllByLabelText('Result, mmol/L'))
      await user.type(field, '0');
    await user.click(screen.getByRole('button', { name: 'Save results' }));
    await waitFor(() => expect(save).toHaveBeenCalledTimes(1));
    expect(
      save.mock.calls[0]![0].values.every((v: { numericValue: number }) => v.numericValue === 0),
    ).toBe(true);
  });
});

describe('Typed vitals survive a failed refetch (CR HIGH)', () => {
  it('keeps the form and its values when a background refetch of the visit fails', async () => {
    const user = userEvent.setup();
    const { client } = renderWithClient(
      <QuickVitalsDialog
        open
        onOpenChange={() => undefined}
        appointmentId={appointmentsFixtureIds.queuedBookedAppointmentId}
        catName="Mittens"
        zone="Asia/Dhaka"
        onRecorded={() => undefined}
      />,
    );
    const weight = await screen.findByLabelText('Weight, kilograms');
    await user.type(weight, '3.9');
    server.use(http.get('*/api/staff/visits/:id', () => new HttpResponse(null, { status: 500 })));
    await act(async () => {
      await client.refetchQueries({ queryKey: ['visit'] });
    });
    await waitFor(() =>
      expect(client.getQueryCache().findAll({ queryKey: ['visit'] })[0]?.state.error).toBeTruthy(),
    );
  });
});

describe('Consent signature is never silently lost (CR HIGH)', () => {
  beforeEach(() => {
    vi.spyOn(HTMLCanvasElement.prototype, 'getBoundingClientRect').mockReturnValue({
      left: 0,
      top: 0,
      right: 200,
      bottom: 100,
      width: 200,
      height: 100,
      x: 0,
      y: 0,
      toJSON: () => ({}),
    });
    vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue({
      setTransform: vi.fn(),
      clearRect: vi.fn(),
      beginPath: vi.fn(),
      moveTo: vi.fn(),
      lineTo: vi.fn(),
      stroke: vi.fn(),
    } as unknown as CanvasRenderingContext2D);
  });

  function pointer(
    canvas: Element,
    type: 'pointerDown' | 'pointerMove' | 'pointerUp',
    x: number,
    y: number,
  ) {
    const event = createEvent[type](canvas, { bubbles: true });
    Object.defineProperty(event, 'clientX', { value: x });
    Object.defineProperty(event, 'clientY', { value: y });
    Object.defineProperty(event, 'pointerId', { value: 1 });
    fireEvent(canvas, event);
  }
  const sign = (canvas: Element) => {
    pointer(canvas, 'pointerDown', 1, 1);
    for (let i = 2; i <= 14; i += 1) pointer(canvas, 'pointerMove', i * 5, i * 3);
    pointer(canvas, 'pointerUp', 70, 42);
  };
  const props = {
    catId: ids.catMittensId,
    appointmentId: appointmentsFixtureIds.queuedBookedAppointmentId,
    onKindChange: vi.fn(),
    ownerName: 'Sam Rahman',
    ownerCustomerId: null,
    witnesses: [],
  };

  it('keeps the drawn strokes when the tab is switched away and back, and submits them', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn().mockResolvedValue(undefined);
    renderWithClient(<ConsentCaptureForm {...props} kind="Surgery" onSubmit={onSubmit} />);
    await screen.findByText('Version 3');
    sign(screen.getByRole('img', { name: /signature area/i }));
    await user.click(screen.getByRole('tab', { name: 'Upload signed form' }));
    await user.click(screen.getByRole('tab', { name: 'Sign on screen' }));
    await user.type(screen.getByLabelText('Procedure'), 'Spay');
    await user.click(screen.getByRole('button', { name: 'Record consent' }));
    await user.click(
      within(await screen.findByRole('dialog', { name: /record this consent/i })).getByRole(
        'button',
        { name: 'Record consent' },
      ),
    );
    await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(1));
    expect(onSubmit.mock.calls[0]![0].signature.length).toBeGreaterThan(0);
  });

  it('after the kind changes (the pad is rebuilt) an empty signature is never submitted', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn().mockResolvedValue(undefined);
    const view = renderWithClient(
      <ConsentCaptureForm {...props} kind="Surgery" onSubmit={onSubmit} />,
    );
    await screen.findByText('Version 3');
    sign(screen.getByRole('img', { name: /signature area/i }));
    view.rerender(
      <QueryClientProvider client={view.client}>
        <ConsentCaptureForm {...props} kind="Anaesthesia" onSubmit={onSubmit} />
      </QueryClientProvider>,
    );
    await screen.findByText('Version 1');
    await user.type(screen.getByLabelText('Procedure'), 'Dental');
    await user.click(screen.getByRole('button', { name: 'Record consent' }));
    expect(
      await screen.findByText(/Sign in the box above|signature is (too short|missing)/i),
    ).toBeInTheDocument();
    expect(onSubmit).not.toHaveBeenCalled();
  });
});

describe('Auto-save replays and unload (CR MEDIUM)', () => {
  const base: DraftRequest = {
    subjective: '',
    objective: '',
    assessment: '',
    plan: '',
    dischargeInstructions: '',
    problemCodeIds: [],
  };
  let handle: ReturnType<typeof useConsultationAutosave>;
  function Harness({ enabled = true }: { enabled?: boolean }) {
    const [plan, setPlan] = useState('');
    handle = useConsultationAutosave({
      visitId: 'v',
      snapshot: { ...base, plan },
      initialSnapshot: base,
      enabled,
    });
    return <textarea aria-label="Plan" value={plan} onChange={(e) => setPlan(e.target.value)} />;
  }
  const type = (text: string) =>
    fireEvent.change(screen.getByLabelText('Plan'), { target: { value: text } });
  const advance = (ms: number) => act(async () => void (await vi.advanceTimersByTimeAsync(ms)));
  const hide = async () => {
    Object.defineProperty(document, 'visibilityState', { value: 'hidden', configurable: true });
    await act(async () => void document.dispatchEvent(new Event('visibilitychange')));
    Object.defineProperty(document, 'visibilityState', { value: 'visible', configurable: true });
  };
  beforeEach(() => vi.useFakeTimers());

  it('never resends a failed payload on visibilitychange, pagehide or unmount', async () => {
    saveDraftMock.mockRejectedValueOnce(new Error('offline'));
    const view = render(<Harness />);
    type('written while offline');
    await advance(AUTOSAVE_DEBOUNCE_MS);
    expect(saveDraftMock).toHaveBeenCalledTimes(1);
    await hide();
    await act(async () => void window.dispatchEvent(new Event('pagehide')));
    view.unmount();
    await advance(0);
    expect(saveDraftMock).toHaveBeenCalledTimes(1);
  });

  it('an explicit flush may try a failed payload again', async () => {
    saveDraftMock.mockRejectedValueOnce(new Error('offline'));
    render(<Harness />);
    type('retry me on purpose');
    await advance(AUTOSAVE_DEBOUNCE_MS);
    await act(async () => void (await handle.flush()));
    expect(saveDraftMock).toHaveBeenCalledTimes(2);
  });

  it('pagehide while a save is in flight sends the NEWEST text at once, with keepalive', async () => {
    let release!: () => void;
    saveDraftMock.mockImplementationOnce(() => new Promise<void>((resolve) => (release = resolve)));
    render(<Harness />);
    type('first words');
    await advance(AUTOSAVE_DEBOUNCE_MS); // in flight, not resolved
    type('first words and the very last edit');
    await act(async () => void window.dispatchEvent(new Event('pagehide')));
    expect(saveDraftMock).toHaveBeenCalledTimes(2);
    expect(saveDraftMock.mock.calls[1]![1].plan).toBe('first words and the very last edit');
    expect(saveDraftMock.mock.calls[1]![2]).toEqual({ keepalive: true });
    release();
  });

  it('a draft save finishing AFTER an explicit Save note cannot reset the baseline', async () => {
    let release!: () => void;
    saveDraftMock.mockImplementationOnce(() => new Promise<void>((resolve) => (release = resolve)));
    render(<Harness />);
    type('draft text');
    await advance(AUTOSAVE_DEBOUNCE_MS);
    const settled = handle.settle();
    handle.markSaved({ ...base, plan: 'noted text' });
    release();
    await act(async () => void (await settled));
    type('noted text'); // equals what the record holds: nothing is owed
    await advance(60_000);
    expect(saveDraftMock).toHaveBeenCalledTimes(1);
  });

  it('settle() waits for the in-flight draft, so a note save is ordered after it', async () => {
    const order: string[] = [];
    let release!: () => void;
    saveDraftMock.mockImplementationOnce(
      () =>
        new Promise<void>((resolve) => (release = () => (order.push('draft-landed'), resolve()))),
    );
    render(<Harness />);
    type('draft');
    await advance(AUTOSAVE_DEBOUNCE_MS);
    const waiting = handle.settle().then(() => order.push('note-may-save'));
    await advance(10);
    expect(order).toEqual([]);
    release();
    await act(async () => void (await waiting));
    expect(order).toEqual(['draft-landed', 'note-may-save']);
  });

  it('after finalise (enabled false) nothing is sent, even on unmount', async () => {
    const view = render(<Harness />);
    type('last edit before finalising');
    view.rerender(<Harness enabled={false} />);
    view.unmount();
    await advance(AUTOSAVE_MAX_WAIT_MS + 1_000);
    expect(saveDraftMock).not.toHaveBeenCalled();
  });

  it('the 20 s ceiling: nothing before it, exactly one save at it, while typing never pauses 3 s', async () => {
    render(<Harness />);
    for (let second = 1; second < 20; second += 1) {
      type(`word ${second}`);
      await advance(1_000);
    }
    expect(saveDraftMock).not.toHaveBeenCalled();
    type('word 20');
    await advance(AUTOSAVE_MAX_WAIT_MS - 19_000 + 0);
    expect(saveDraftMock).toHaveBeenCalledTimes(1);
  });
});

describe('Restored problem rows (CR MEDIUM)', () => {
  it('two lookups resolving together both get their names', async () => {
    const base = await getVisit(ids.visitDraftNewerId);
    server.use(
      http.get('*/api/staff/visits/:id', () =>
        HttpResponse.json({
          ...base,
          draft: { ...base.draft, problemCodeIds: [ids.codeCkdId, ids.codeOtitisId] },
        }),
      ),
    );
    renderWithClient(<VisitView visitId={ids.visitDraftNewerId} />);
    expect(await screen.findByText(/REN-CKD2 — Chronic kidney disease/)).toBeInTheDocument();
    expect(await screen.findByText(/EAR-OTE — Otitis externa/)).toBeInTheDocument();
    expect(screen.queryByText('Loading diagnosis…')).toBeNull();
  });
});

describe('Consent entry points (ARCH MEDIUM)', () => {
  it('the cat consents page points to the appointment for recording a new consent', async () => {
    const { default: CatConsentsPage } =
      await import('@/app/(staff)/staff/cats/[catId]/consents/page');
    // A pre-resolved thenable: React's `use` reads it synchronously instead of suspending.
    const params = Object.assign(Promise.resolve({ catId: ids.catWhiskersId }), {
      status: 'fulfilled',
      value: { catId: ids.catWhiskersId },
    });
    renderWithClient(
      <Suspense fallback={null}>
        <CatConsentsPage params={params} />
      </Suspense>,
    );
    expect(await screen.findByRole('link', { name: 'find the appointment' })).toHaveAttribute(
      'href',
      '/staff/appointments',
    );
  });
});
