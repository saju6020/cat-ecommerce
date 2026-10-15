import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  getSchedule,
  listScheduleRuns,
  runScheduleNow,
  transitionSchedule,
  updateSchedule,
} from '@/lib/api/report-schedules';
import type { ReportScheduleInput } from '@/lib/api/report-schedules';

// TEST gate (SECURITY 87 regression): a schedule id is encoded into the path on every id route, so a
// hostile or malformed id cannot reach another endpoint. The behaviour is observed on the wire
// (the URL handed to fetch), not read from the source.
const HOSTILE = 'a/../b?x=1#frag';
const ENCODED = encodeURIComponent(HOSTILE);

afterEach(() => vi.restoreAllMocks());

function captureUrls() {
  const urls: string[] = [];
  vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
    urls.push(typeof input === 'string' ? input : input instanceof URL ? input.href : input.url);
    return new Response('{}', { status: 200, headers: { 'Content-Type': 'application/json' } });
  });
  return urls;
}

describe('report-schedules API: the schedule id is path-encoded on every id route', () => {
  it.each([
    ['getSchedule', () => getSchedule(HOSTILE)],
    ['updateSchedule', () => updateSchedule(HOSTILE, {} as ReportScheduleInput)],
    ['pause', () => transitionSchedule(HOSTILE, 'pause')],
    ['resume', () => transitionSchedule(HOSTILE, 'resume')],
    ['cancel', () => transitionSchedule(HOSTILE, 'cancel')],
    ['runScheduleNow', () => runScheduleNow(HOSTILE)],
    ['listScheduleRuns', () => listScheduleRuns(HOSTILE, 1)],
  ])('%s', async (_name, call) => {
    const urls = captureUrls();
    await call().catch(() => undefined);
    expect(urls).toHaveLength(1);
    const path = new URL(urls[0]!).pathname;
    expect(path).toContain(`/report-schedules/${ENCODED}`);
    expect(path).not.toContain('/../');
    expect(urls[0]).not.toContain('#frag');
  });
});
