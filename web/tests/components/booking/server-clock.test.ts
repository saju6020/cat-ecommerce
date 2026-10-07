import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  getServerClockOffsetMs,
  recordServerDate,
  resetServerClockSamples,
} from '@/lib/api/client';

describe('server clock offset — HO-APT-002 §8.4, ADR-FE-025', () => {
  afterEach(() => {
    resetServerClockSamples();
    vi.useRealTimers();
  });

  it('U-A6: the offset is the median of the last five Date headers', () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-10-12T04:00:00Z'));
    expect(getServerClockOffsetMs()).toBe(0);
    // A server 90 s ahead, with one slow outlier sample.
    for (const ahead of [90, 91, 90, 400, 89]) {
      recordServerDate(new Date(Date.now() + ahead * 1000).toUTCString());
    }
    expect(getServerClockOffsetMs()).toBe(90_000);
  });

  it('ignores a missing or unparsable header', () => {
    recordServerDate(null);
    recordServerDate('not a date');
    expect(getServerClockOffsetMs()).toBe(0);
  });
});
