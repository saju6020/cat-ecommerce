import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen } from '@testing-library/react';
import { HoldCountdown } from '@/components/booking/HoldCountdown';
import { recordServerDate, resetServerClockSamples } from '@/lib/api/client';

const T0 = new Date('2026-10-12T04:00:00Z');

describe('HoldCountdown — HO-APT-002 ADR-FE-025', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(T0);
  });
  afterEach(() => {
    resetServerClockSamples();
    vi.useRealTimers();
  });

  function tick(ms: number) {
    act(() => {
      vi.advanceTimersByTime(ms);
    });
  }

  it('U-A6: with the server clock 90 s ahead it counts to the SERVER expiry, not the browser one', () => {
    // The server is 90 s ahead of this browser, and the hold expires 100 s from "server now".
    recordServerDate(new Date(T0.getTime() + 90_000).toUTCString());
    const expires = new Date(T0.getTime() + 90_000 + 100_000).toISOString();
    render(<HoldCountdown holdExpiresOn={expires} onExpire={vi.fn()} />);
    // Naively against the browser clock this would read 03:10; on the server clock it is 01:40.
    expect(screen.getByRole('timer')).toHaveTextContent('01:40');
    tick(10_000);
    expect(screen.getByRole('timer')).toHaveTextContent('01:30');
  });

  it('U-A7: announces only at 5:00, 1:00 and 0:00 — never every second', () => {
    const onExpire = vi.fn();
    const expires = new Date(T0.getTime() + 301_000).toISOString(); // 5:01 left
    render(<HoldCountdown holdExpiresOn={expires} onExpire={onExpire} />);
    const live = screen.getByRole('status');
    const heard: string[] = [];
    const sample = () => {
      const text = live.textContent ?? '';
      if (text && heard[heard.length - 1] !== text) heard.push(text);
    };

    for (let second = 0; second < 305; second += 1) {
      tick(1000);
      sample();
    }
    expect(heard).toEqual([
      'You have 5 minutes left to confirm your booking.',
      'You have 1 minute left to confirm your booking.',
      'Your hold has ended — please choose a time again.',
    ]);
    expect(onExpire).toHaveBeenCalledTimes(1);
  });

  it('does not announce thresholds that were already behind the starting time', () => {
    const expires = new Date(T0.getTime() + 70_000).toISOString();
    render(<HoldCountdown holdExpiresOn={expires} onExpire={vi.fn()} />);
    expect(screen.getByRole('status')).toHaveTextContent('');
    tick(11_000);
    expect(screen.getByRole('status')).toHaveTextContent('1 minute left');
  });

  it('shows an icon and words, not colour alone, under a minute', () => {
    const expires = new Date(T0.getTime() + 30_000).toISOString();
    render(<HoldCountdown holdExpiresOn={expires} onExpire={vi.fn()} />);
    expect(screen.getByText(/less than a minute left/i)).toBeInTheDocument();
  });
});
