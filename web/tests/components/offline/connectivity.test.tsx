import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { apiFetch, OfflineError, setConnectivityReporter } from '@/lib/api/client';
import {
  forceCheck,
  getSnapshot,
  onRecovered,
  pingCountForTests,
  recordOutcome,
  resetConnectivityForTests,
  startMonitor,
} from '@/lib/connectivity/monitor';
import { useConnectivity } from '@/lib/hooks/useConnectivity';
import { WriteButton } from '@/components/common/WriteButton';
import { OFFLINE_WRITE_MESSAGE, describeApiError } from '@/lib/api/errors';
import { resetXctMock, xctMock } from '@/mocks/handlers/operations';

beforeEach(() => {
  resetConnectivityForTests();
  resetXctMock();
});
afterEach(() => {
  resetConnectivityForTests();
  setConnectivityReporter(null);
  vi.useRealTimers();
});

describe('U-A1..U-A3 the monitor is the server\'s answer, not the browser\'s flag (ADR-FE-142)', () => {
  it('U-A1: one failure stays online, two consecutive go offline, a success comes back', () => {
    recordOutcome('unreachable');
    expect(getSnapshot().status).toBe('online');
    recordOutcome('unreachable');
    expect(getSnapshot().status).toBe('offline');
    const recovered = vi.fn();
    onRecovered(recovered);
    recordOutcome('reachable');
    expect(getSnapshot().status).toBe('online');
    expect(recovered).toHaveBeenCalledTimes(1);
  });

  it('U-A2: a 5xx is degraded, not offline', () => {
    recordOutcome('server-error');
    expect(getSnapshot().status).toBe('degraded');
    recordOutcome('server-error');
    expect(getSnapshot().status).toBe('degraded');
    recordOutcome('reachable');
    expect(getSnapshot().status).toBe('online');
  });

  it('U-A3: one failure while the browser reports no network goes offline at once', () => {
    const spy = vi.spyOn(window.navigator, 'onLine', 'get').mockReturnValue(false);
    recordOutcome('unreachable');
    expect(getSnapshot().status).toBe('offline');
    spy.mockRestore();
  });

  it('pings the server on start and goes offline after two failed pings', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    xctMock.pingMode = 'fail';
    const stop = startMonitor();
    await vi.advanceTimersByTimeAsync(100);
    expect(getSnapshot().status).toBe('online'); // one failure so far
    await vi.advanceTimersByTimeAsync(3_100); // the re-check comes forward after a failure
    expect(getSnapshot().status).toBe('offline');
    xctMock.pingMode = 'ok';
    await vi.advanceTimersByTimeAsync(3_100); // and keeps coming quickly while offline
    expect(getSnapshot().status).toBe('online');
    stop();
  });

  it('a ping answered 503 is degraded', async () => {
    xctMock.pingMode = 'serverError';
    const stop = startMonitor();
    await vi.waitFor(() => expect(getSnapshot().status).toBe('degraded'));
    stop();
  });
});

describe('U-A8 client.ts refuses a write before any request while offline (ADR-FE-143)', () => {
  it('throws OfflineError before fetch, queues nothing, and the message is mapped', async () => {
    const seen = vi.fn();
    server.use(
      http.post('*/api/some/write', () => {
        seen();
        return HttpResponse.json({});
      }),
    );
    xctMock.pingMode = 'fail'; // the monitor's own pings keep failing
    const stop = startMonitor(); // registers the reporter
    recordOutcome('unreachable');
    recordOutcome('unreachable');
    await expect(apiFetch('/api/some/write', { method: 'POST', body: { a: 1 } })).rejects.toBeInstanceOf(
      OfflineError,
    );
    expect(seen).not.toHaveBeenCalled();
    expect(describeApiError(new OfflineError())).toBe(OFFLINE_WRITE_MESSAGE);
    expect(OFFLINE_WRITE_MESSAGE).not.toMatch(/OfflineError|errorCode/);
    stop();
  });

  it('a read still goes through while offline (the data on screen is the cache)', async () => {
    xctMock.pingMode = 'fail';
    const stop = startMonitor();
    recordOutcome('unreachable');
    recordOutcome('unreachable');
    server.use(http.get('*/api/some/read', () => HttpResponse.json({ ok: true })));
    await expect(apiFetch('/api/some/read')).resolves.toEqual({ ok: true });
    stop();
  });
});

describe('U-A7 typed input survives going offline; submit is refused with a visible reason', () => {
  function Form() {
    const { status } = useConnectivity();
    const [value, setValue] = useState('');
    const [sent, setSent] = useState(0);
    return (
      <form
        onSubmit={(e) => {
          e.preventDefault();
          setSent((n) => n + 1);
        }}
      >
        <label>
          Weight
          <input value={value} onChange={(e) => setValue(e.target.value)} />
        </label>
        <WriteButton type="submit">Save</WriteButton>
        <p>status:{status} sent:{sent}</p>
      </form>
    );
  }

  it('keeps the values, disables Save with a reason, and enables it again on recovery', async () => {
    const user = userEvent.setup();
    render(<Form />);
    await user.type(screen.getByLabelText('Weight'), '4.2');
    act(() => {
      recordOutcome('unreachable');
      recordOutcome('unreachable');
    });
    const save = screen.getByRole('button', { name: 'Save' });
    expect(screen.getByLabelText('Weight')).toHaveValue('4.2');
    expect(save).toBeDisabled();
    expect(screen.getByText("Offline — can't save")).toBeInTheDocument();
    expect(save).toHaveAccessibleDescription("Offline — can't save");
    act(() => recordOutcome('reachable'));
    expect(screen.getByRole('button', { name: 'Save' })).toBeEnabled();
    expect(screen.getByLabelText('Weight')).toHaveValue('4.2');
    await user.click(screen.getByRole('button', { name: 'Save' }));
    expect(screen.getByText(/sent:1/)).toBeInTheDocument();
  });
});

describe('ARCHITECTURE HIGH H1: one ping loop, whatever happens', () => {
  it('counts pings across a ten-minute outage and two minutes after recovery, and none after stop()', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    xctMock.pingMode = 'fail';
    const stop = startMonitor();
    // A failure reported by an ordinary request while a ping is in flight must not add a second loop.
    for (let i = 0; i < 5; i += 1) recordOutcome('unreachable');
    await vi.advanceTimersByTimeAsync(10 * 60_000);
    const duringOutage = pingCountForTests();
    // 3 s apart for the first minute (about 20), 15 s until five minutes (about 16), then 60 s (about 5).
    expect(duringOutage).toBeGreaterThanOrEqual(35);
    expect(duringOutage).toBeLessThanOrEqual(48);

    xctMock.pingMode = 'ok';
    await vi.advanceTimersByTimeAsync(60_000); // the next 60 s check sees it
    const recovered = pingCountForTests();
    await vi.advanceTimersByTimeAsync(2 * 60_000);
    const afterRecovery = pingCountForTests() - recovered;
    expect(getSnapshot().status).toBe('online');
    expect(afterRecovery).toBeGreaterThanOrEqual(7); // 15 s apart: 8 in two minutes
    expect(afterRecovery).toBeLessThanOrEqual(9);

    stop();
    const atStop = pingCountForTests();
    await vi.advanceTimersByTimeAsync(5 * 60_000);
    expect(pingCountForTests()).toBe(atStop);
  });

  it('a client failure during a healthy stretch does not double the rate', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const stop = startMonitor();
    await vi.advanceTimersByTimeAsync(100);
    const start = pingCountForTests();
    recordOutcome('unreachable'); // one blip, then healthy again
    await vi.advanceTimersByTimeAsync(60_000);
    const used = pingCountForTests() - start;
    expect(used).toBeLessThanOrEqual(6); // 3 s re-check, then 15 s: about 4, never 8
    stop();
  });
});

describe('ARCHITECTURE LOW: in-flight sharing and stop() during a ping', () => {
  function slowPing(delayMs: number) {
    const hits = vi.fn();
    server.use(
      http.get('*/api/system/ping', async () => {
        hits();
        await new Promise((resolve) => setTimeout(resolve, delayMs));
        return new HttpResponse(null, { status: 204 });
      }),
    );
    return hits;
  }

  it('two concurrent checks cause ONE ping', async () => {
    const hits = slowPing(150);
    const [a, b] = await Promise.all([forceCheck(), forceCheck()]);
    expect(a).toBe('reachable');
    expect(b).toBe('reachable');
    expect(hits).toHaveBeenCalledTimes(1);
    expect(pingCountForTests()).toBe(1);
  });

  it('stop() while a ping is in flight leaves no further ping behind', async () => {
    // Fake timers BEFORE the monitor starts: a timer scheduled after stop() (by a missing `!running`
    // guard) is then a fake timer, which advanceTimersByTimeAsync below really fires.
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const hits = slowPing(200);
    const stop = startMonitor(); // its first ping is now in flight
    await vi.waitFor(() => expect(hits).toHaveBeenCalledTimes(1));
    stop();
    await vi.advanceTimersByTimeAsync(400); // the ping completes after stop()
    const atStop = pingCountForTests();
    await vi.advanceTimersByTimeAsync(5 * 60_000);
    expect(pingCountForTests()).toBe(atStop);
    expect(hits).toHaveBeenCalledTimes(1);
  });
});
