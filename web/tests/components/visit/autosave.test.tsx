import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen } from '@testing-library/react';
import { useState } from 'react';
import { ApiUnauthorizedError } from '@/lib/api/client';
import { VisitHeaderBar } from '@/components/visit/VisitHeaderBar';
import {
  AUTOSAVE_DEBOUNCE_MS,
  AUTOSAVE_MAX_WAIT_MS,
  useConsultationAutosave,
} from '@/lib/hooks/useConsultationAutosave';
import type { DraftRequest, VisitDetail } from '@/lib/api/visits';

const saveDraftMock = vi.hoisted(() => vi.fn());
vi.mock('@/lib/api/visits', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/lib/api/visits')>()),
  saveDraft: saveDraftMock,
}));

const base: DraftRequest = {
  subjective: '',
  objective: '',
  assessment: '',
  plan: '',
  dischargeInstructions: '',
  problemCodeIds: [],
};

function Harness({ enabled = true }: { enabled?: boolean }) {
  const [plan, setPlan] = useState('');
  const autosave = useConsultationAutosave({
    visitId: 'visit-1',
    snapshot: { ...base, plan },
    initialSnapshot: base,
    enabled,
  });
  return (
    <>
      <textarea aria-label="Plan" value={plan} onChange={(e) => setPlan(e.target.value)} />
      <output data-testid="status">{autosave.status}</output>
    </>
  );
}

const type = (text: string) =>
  fireEvent.change(screen.getByLabelText('Plan'), { target: { value: text } });
const advance = (ms: number) =>
  act(async () => {
    await vi.advanceTimersByTimeAsync(ms);
  });

beforeEach(() => {
  vi.useFakeTimers();
  saveDraftMock.mockReset();
  saveDraftMock.mockResolvedValue(undefined);
});
afterEach(() => {
  vi.useRealTimers();
  Object.defineProperty(document, 'visibilityState', { value: 'visible', configurable: true });
});

describe('useConsultationAutosave — HO-EMR-002 ADR-FE-050', () => {
  it('U-A7: saves once, 3 s after typing stops — and not before', async () => {
    render(<Harness />);
    type('Offer wet food');
    await advance(AUTOSAVE_DEBOUNCE_MS - 1);
    expect(saveDraftMock).not.toHaveBeenCalled();
    await advance(1);
    expect(saveDraftMock).toHaveBeenCalledTimes(1);
    expect(saveDraftMock.mock.calls[0]![1]).toMatchObject({ plan: 'Offer wet food' });
    expect(screen.getByTestId('status')).toHaveTextContent('saved');
    // Nothing changed since the last success — nothing is sent again.
    await advance(60_000);
    expect(saveDraftMock).toHaveBeenCalledTimes(1);
  });

  it('U-A7: typing continuously for 25 s saves at most 20 s apart', async () => {
    render(<Harness />);
    for (let second = 1; second <= 25; second += 1) {
      type(`word ${second}`);
      await advance(1_000);
    }
    expect(AUTOSAVE_MAX_WAIT_MS).toBe(20_000);
    // The 3 s debounce never settled while typing, so the 20 s ceiling produced the one save.
    expect(saveDraftMock).toHaveBeenCalledTimes(1);
    expect(saveDraftMock.mock.calls[0]![1].plan).toMatch(/^word (19|2\d)$/);
  });

  it('U-A7: hiding the tab flushes with keepalive', async () => {
    render(<Harness />);
    type('Last words before the tab closes');
    Object.defineProperty(document, 'visibilityState', { value: 'hidden', configurable: true });
    await act(async () => {
      document.dispatchEvent(new Event('visibilitychange'));
      await vi.advanceTimersByTimeAsync(0);
    });
    expect(saveDraftMock).toHaveBeenCalledTimes(1);
    expect(saveDraftMock.mock.calls[0]![2]).toEqual({ keepalive: true });
  });

  it('U-A7: leaving the route (unmount) flushes anything not yet saved with keepalive', async () => {
    const view = render(<Harness />);
    type('Typed, then navigated away');
    view.unmount();
    await advance(0);
    expect(saveDraftMock).toHaveBeenCalledTimes(1);
    expect(saveDraftMock.mock.calls[0]![2]).toEqual({ keepalive: true });
  });

  it('sends nothing at all when the role cannot edit (enabled = false)', async () => {
    render(<Harness enabled={false} />);
    type('Should never be sent');
    await advance(60_000);
    expect(saveDraftMock).not.toHaveBeenCalled();
  });

  it('U-A8: a failed save is never replayed; the next edit schedules a fresh one; text is untouched', async () => {
    saveDraftMock.mockRejectedValueOnce(
      new ApiUnauthorizedError({ error: 'session_idle_timeout' }),
    );
    render(<Harness />);
    type('Written while the lock was coming down');
    await advance(AUTOSAVE_DEBOUNCE_MS);
    expect(saveDraftMock).toHaveBeenCalledTimes(1);
    expect(screen.getByTestId('status')).toHaveTextContent('failed');
    expect(screen.getByLabelText('Plan')).toHaveValue('Written while the lock was coming down');

    await advance(120_000);
    expect(saveDraftMock).toHaveBeenCalledTimes(1); // no automatic retry

    type('Written while the lock was coming down, then more after unlocking');
    await advance(AUTOSAVE_DEBOUNCE_MS);
    expect(saveDraftMock).toHaveBeenCalledTimes(2);
    expect(screen.getByTestId('status')).toHaveTextContent('saved');
  });
});

describe('VisitHeaderBar auto-save status — UI-6', () => {
  const visit = {
    catName: 'Mittens',
    service: { name: 'General consultation' },
    appointment: { startsOn: null },
    openedBy: 'Dr Akter',
    openedOn: '2026-10-07T04:00:00Z',
    status: 'Open',
    branchTimeZoneId: 'Asia/Dhaka',
    allowedActions: ['editNote', 'finalise'],
  } as unknown as VisitDetail;

  it('announces ONLY the failure, politely, and keeps saying the text is still here', () => {
    const { rerender } = render(
      <VisitHeaderBar
        visit={visit}
        autosave={{ status: 'saving', savedAt: null }}
        onOpenHistory={() => undefined}
      />,
    );
    expect(screen.getByText('Saving draft…')).toBeInTheDocument();
    const live = screen.getByRole('status');
    expect(live).toHaveAttribute('aria-live', 'polite');
    expect(live).toBeEmptyDOMElement();

    rerender(
      <VisitHeaderBar
        visit={visit}
        autosave={{ status: 'saved', savedAt: new Date('2026-10-07T04:42:00Z') }}
        onOpenHistory={() => undefined}
      />,
    );
    expect(screen.getByText(/Draft saved 10:42 \(Asia\/Dhaka\)/)).toBeInTheDocument();
    expect(screen.getByRole('status')).toBeEmptyDOMElement();

    rerender(
      <VisitHeaderBar
        visit={visit}
        autosave={{ status: 'failed', savedAt: null }}
        onOpenHistory={() => undefined}
      />,
    );
    expect(screen.getByRole('status')).toHaveTextContent(
      'Couldn’t save your draft — your text is still here',
    );
  });
});
