import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  buildBranchCalendarMonth,
  leadingBlankCount,
  monthBounds,
  parseMonthParam,
  shiftMonth,
} from '@/lib/format/branch-calendar';
import type {
  BranchCalendarInterval,
  BranchClosure,
  OpeningInterval,
} from '@/lib/api/admin-branches';

const DHAKA_WEEK: OpeningInterval[] = (
  ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'] as const
).flatMap((dayOfWeek) => [
  { dayOfWeek, opens: '09:00', closes: '13:00' },
  { dayOfWeek, opens: '14:00', closes: '18:00' },
]);

function closure(overrides: Partial<BranchClosure>): BranchClosure {
  return {
    id: 'c1',
    branchId: 'b1',
    startsOn: '2027-01-01',
    endsOn: '2027-01-01',
    kind: 'Closure',
    reason: 'Reason',
    partialFrom: null,
    partialTo: null,
    isCancelled: false,
    ...overrides,
  };
}

function build(
  overrides: Partial<Parameters<typeof buildBranchCalendarMonth>[0]> & { month: string },
) {
  return buildBranchCalendarMonth({
    timeZoneId: 'Asia/Dhaka',
    weeklyHours: DHAKA_WEEK,
    intervals: [],
    closures: [],
    today: '2020-01-01',
    ...overrides,
  });
}

const day = (days: ReturnType<typeof build>, date: string) => days.find((d) => d.date === date)!;

describe('buildBranchCalendarMonth — HO-ADM-002-FU1 §11.1', () => {
  it('U-CAL1: Dhaka Monday with two server intervals is open with branch-zone labels', () => {
    const intervals: BranchCalendarInterval[] = [
      { start: '2027-02-08T03:00:00Z', end: '2027-02-08T07:00:00Z' },
      { start: '2027-02-08T08:00:00Z', end: '2027-02-08T12:00:00Z' },
    ];
    const d = day(build({ month: '2027-02', intervals }), '2027-02-08');
    expect(d.status).toBe('open');
    expect(d.dayOfWeek).toBe('Monday');
    expect(d.intervals.map((i) => i.label)).toEqual(['09:00–13:00', '14:00–18:00']);
    expect(d.text).toBe('Open 09:00–13:00, 14:00–18:00');
  });

  it('U-CAL2: a public holiday with no intervals reads Closed — Public holiday', () => {
    const d = day(
      build({
        month: '2027-01',
        closures: [closure({ kind: 'PublicHoliday', reason: "New Year's Day" })],
      }),
      '2027-01-01',
    );
    expect(d.status).toBe('holiday');
    expect(d.text).toBe("Closed — Public holiday: New Year's Day");
  });

  it('U-CAL3: a three-day closure covers its dates and not the next day', () => {
    const days = build({
      month: '2027-03',
      closures: [
        closure({ startsOn: '2027-03-09', endsOn: '2027-03-11', reason: 'Refurbishment' }),
      ],
    });
    for (const date of ['2027-03-09', '2027-03-10', '2027-03-11']) {
      expect(day(days, date).status).toBe('closure');
      expect(day(days, date).text).toBe('Closed — Refurbishment');
    }
    expect(day(days, '2027-03-12').text).toBe('Closed');
  });

  it('U-CAL4: a Sunday with no Sunday row is a regular weekly closure', () => {
    const d = day(build({ month: '2027-01' }), '2027-01-03');
    expect(d.dayOfWeek).toBe('Sunday');
    expect(d.status).toBe('weeklyClosed');
    expect(d.text).toBe('Closed (regular weekly closure)');
  });

  it('U-CAL5: a part-day closure over a server morning interval is reduced hours', () => {
    const d = day(
      build({
        month: '2027-02',
        intervals: [{ start: '2027-02-15T03:00:00Z', end: '2027-02-15T07:00:00Z' }],
        closures: [
          closure({
            startsOn: '2027-02-15',
            endsOn: '2027-02-15',
            reason: 'Staff training (half day)',
            partialFrom: '13:00',
            partialTo: '18:00',
          }),
        ],
      }),
      '2027-02-15',
    );
    expect(d.status).toBe('reduced');
    expect(d.text).toBe(
      'Reduced hours: 09:00–13:00. Closed 13:00–18:00: Staff training (half day)',
    );
  });

  it('U-CAL5b: a part-day closure covering all hours with no intervals reads closed with the window', () => {
    const d = day(
      build({
        month: '2027-02',
        closures: [
          closure({
            startsOn: '2027-02-16',
            endsOn: '2027-02-16',
            reason: 'Power cut',
            partialFrom: '09:00',
            partialTo: '18:00',
          }),
        ],
      }),
      '2027-02-16',
    );
    expect(d.status).toBe('closure');
    expect(d.text).toBe(
      'Closed — Power cut (part-day closure 09:00–18:00 covers all opening hours)',
    );
  });

  it('U-CAL6: the server wins over weekly hours and closures', () => {
    // (a) weekly hours say Tuesday is open, the server returned nothing, no closure.
    const a = day(build({ month: '2027-02' }), '2027-02-09');
    expect(a.dayOfWeek).toBe('Tuesday');
    expect(a.status).toBe('closed');
    expect(a.text).toBe('Closed');
    expect(JSON.stringify(a)).not.toContain('09:00');

    // (b) a full-day closure on a date where the server DOES return intervals.
    const b = day(
      build({
        month: '2027-02',
        intervals: [{ start: '2027-02-10T03:00:00Z', end: '2027-02-10T07:00:00Z' }],
        closures: [closure({ startsOn: '2027-02-10', endsOn: '2027-02-10', reason: 'Hidden' })],
      }),
      '2027-02-10',
    );
    expect(b.status).toBe('open');
    expect(b.text).toBe('Open 09:00–13:00');
    expect(b.closure).toBeNull();

    // (c) a cancelled holiday on a Sunday: ignored, falls to the weekly closure.
    const c = day(
      build({
        month: '2027-01',
        closures: [
          closure({
            kind: 'PublicHoliday',
            startsOn: '2027-01-03',
            endsOn: '2027-01-03',
            isCancelled: true,
          }),
        ],
      }),
      '2027-01-03',
    );
    expect(c.status).toBe('weeklyClosed');
  });

  it('with closures undefined, closed days fall to weekly closed / closed', () => {
    const days = build({ month: '2027-01', closures: undefined });
    expect(day(days, '2027-01-01').text).toBe('Closed');
    expect(day(days, '2027-01-03').status).toBe('weeklyClosed');
  });

  it('marks today and past days', () => {
    const days = build({ month: '2027-01', today: '2027-01-10' });
    expect(day(days, '2027-01-10').isToday).toBe(true);
    expect(day(days, '2027-01-09').isPast).toBe(true);
    expect(day(days, '2027-01-11').isPast).toBe(false);
  });

  it('U-CAL7: London BST/GMT days both read 08:00–17:00; a Dhaka 23:30Z start lands on the next local day', () => {
    const london = build({
      month: '2027-10',
      timeZoneId: 'Europe/London',
      weeklyHours: [{ dayOfWeek: 'Friday', opens: '08:00', closes: '17:00' }],
      intervals: [{ start: '2027-10-29T07:00:00+00:00', end: '2027-10-29T16:00:00Z' }],
    });
    expect(day(london, '2027-10-29').text).toBe('Open 08:00–17:00');
    const nov = build({
      month: '2027-11',
      timeZoneId: 'Europe/London',
      weeklyHours: [{ dayOfWeek: 'Monday', opens: '08:00', closes: '17:00' }],
      intervals: [{ start: '2027-11-01T08:00:00Z', end: '2027-11-01T17:00:00Z' }],
    });
    expect(day(nov, '2027-11-01').text).toBe('Open 08:00–17:00');

    const dhaka = build({
      month: '2027-03',
      intervals: [{ start: '2027-03-01T23:30:00Z', end: '2027-03-02T03:30:00Z' }],
    });
    expect(day(dhaka, '2027-03-02').intervals).toHaveLength(1);
    expect(day(dhaka, '2027-03-01').intervals).toHaveLength(0);
    expect(day(dhaka, '2027-03-02').text).toBe('Open 05:30–09:30');
  });
});

describe('month helpers — U-CAL8', () => {
  afterEach(() => vi.useRealTimers());

  it('monthBounds, shiftMonth, leadingBlankCount', () => {
    expect(monthBounds('2027-02')).toEqual({ from: '2027-02-01', to: '2027-02-28' });
    expect(monthBounds('2028-02')).toEqual({ from: '2028-02-01', to: '2028-02-29' });
    expect(shiftMonth('2027-12', 1)).toBe('2028-01');
    expect(shiftMonth('2027-01', -1)).toBe('2026-12');
    expect(leadingBlankCount('2027-01')).toBe(4);
    expect(leadingBlankCount('2028-05')).toBe(0);
  });

  it('parseMonthParam keeps a valid month and replaces an invalid one with the current month in the zone', () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2027-03-31T19:00:00Z'));
    expect(parseMonthParam('2027-13', 'Asia/Dhaka')).toBe('2027-04');
    expect(parseMonthParam(null, 'Europe/London')).toBe('2027-03');
    expect(parseMonthParam('<script>', 'Asia/Dhaka')).toBe('2027-04');
    expect(parseMonthParam('2027-02', 'Asia/Dhaka')).toBe('2027-02');
  });
});
