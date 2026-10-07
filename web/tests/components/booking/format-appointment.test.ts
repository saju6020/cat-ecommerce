import { describe, expect, it } from 'vitest';
import {
  addDaysIso,
  dateRange,
  dayPeriodOf,
  formatAppointmentWhen,
  formatCountdown,
  formatEstimatedWait,
  formatSlot,
  startOfWeekIso,
  zonedLocalToInstant,
} from '@/lib/format/appointment';

describe('lib/format/appointment — HO-APT-002 §8.4', () => {
  it('U-A3: a Europe/London slot is shown in London time whatever zone the viewer is in', () => {
    // 09:30Z in October is BST (UTC+1) in London; Dhaka would read 15:30.
    const startsOn = '2026-10-12T09:30:00Z';
    const endsOn = '2026-10-12T10:00:00Z';
    expect(formatSlot(startsOn, endsOn, 'Europe/London')).toBe('10:30–11:00');
    expect(formatSlot(startsOn, endsOn, 'Asia/Dhaka')).toBe('15:30–16:00');
    expect(formatAppointmentWhen(startsOn, 'Europe/London')).toBe('Mon 12 Oct, 10:30');
  });

  it('turns a branch-local date and time into the real instant, DST included', () => {
    expect(zonedLocalToInstant('2026-10-12', '10:30', 'Asia/Dhaka')).toBe(
      '2026-10-12T04:30:00.000Z',
    );
    expect(zonedLocalToInstant('2026-10-12', '10:30', 'Europe/London')).toBe(
      '2026-10-12T09:30:00.000Z',
    );
    expect(zonedLocalToInstant('2026-12-14', '10:30', 'Europe/London')).toBe(
      '2026-12-14T10:30:00.000Z',
    );
  });

  it('W16: is correct across DST transitions, not just on ordinary days', () => {
    // London: spring forward 2026-03-29 01:00 UTC, fall back 2026-10-25 01:00 UTC.
    expect(zonedLocalToInstant('2026-03-29', '00:30', 'Europe/London')).toBe(
      '2026-03-29T00:30:00.000Z',
    );
    expect(zonedLocalToInstant('2026-03-29', '02:30', 'Europe/London')).toBe(
      '2026-03-29T01:30:00.000Z',
    );
    expect(zonedLocalToInstant('2026-03-29', '12:00', 'Europe/London')).toBe(
      '2026-03-29T11:00:00.000Z',
    );
    expect(zonedLocalToInstant('2026-10-25', '00:30', 'Europe/London')).toBe(
      '2026-10-24T23:30:00.000Z',
    );
    expect(zonedLocalToInstant('2026-10-25', '03:00', 'Europe/London')).toBe(
      '2026-10-25T03:00:00.000Z',
    );
    // New York: spring forward 2026-03-08 07:00 UTC, fall back 2026-11-01 06:00 UTC.
    expect(zonedLocalToInstant('2026-11-01', '03:00', 'America/New_York')).toBe(
      '2026-11-01T08:00:00.000Z',
    );
    expect(zonedLocalToInstant('2026-03-08', '03:30', 'America/New_York')).toBe(
      '2026-03-08T07:30:00.000Z',
    );
    expect(zonedLocalToInstant('2026-03-08', '01:30', 'America/New_York')).toBe(
      '2026-03-08T06:30:00.000Z',
    );
  });

  it('does calendar arithmetic on yyyy-MM-dd strings without a browser zone', () => {
    expect(addDaysIso('2026-10-31', 1)).toBe('2026-11-01');
    expect(addDaysIso('2026-03-01', -1)).toBe('2026-02-28');
    expect(dateRange('2026-10-30', 3)).toEqual(['2026-10-30', '2026-10-31', '2026-11-01']);
    expect(startOfWeekIso('2026-10-04')).toBe('2026-09-28'); // a Sunday -> the Monday before
    expect(startOfWeekIso('2026-10-05')).toBe('2026-10-05');
  });

  it('groups slots into morning / afternoon / evening by branch hour', () => {
    expect(dayPeriodOf('2026-10-12T04:30:00Z', 'Asia/Dhaka')).toBe('morning'); // 10:30
    expect(dayPeriodOf('2026-10-12T08:00:00Z', 'Asia/Dhaka')).toBe('afternoon'); // 14:00
    expect(dayPeriodOf('2026-10-12T11:00:00Z', 'Asia/Dhaka')).toBe('evening'); // 17:00
  });

  it('formats countdowns and queue estimates', () => {
    expect(formatCountdown(65_000)).toBe('01:05');
    expect(formatCountdown(-5)).toBe('00:00');
    expect(formatEstimatedWait(35)).toBe('About 35 min');
    expect(formatEstimatedWait(0)).toBe('Next');
    expect(formatEstimatedWait(null)).toBe('Wait not estimated');
  });
});
