import { describe, expect, it } from 'vitest';
import {
  compatiblePeriods,
  formatCell,
  formatReportDate,
  groupDigits,
  asVarianceDirection,
  toChartNumber,
} from '@/lib/format/report';

const dhaka = 'Asia/Dhaka';

describe('formatCell — HO-RPT-002 §8.4 (U-C8)', () => {
  it('renders null as a dash, never zero', () => {
    expect(formatCell(null, { type: 'Money' }, dhaka)).toBe('—');
    expect(formatCell(undefined, { type: 'Integer' }, dhaka)).toBe('—');
  });

  it('renders money from the string pair', () => {
    expect(
      formatCell({ amount: '1500.00', currencyCode: 'BDT' }, { type: 'Money' }, dhaka),
    ).toContain('1,500.00');
  });

  it('suffixes percent and groups digits without rounding', () => {
    expect(formatCell('12.5', { type: 'Percent' }, dhaka)).toBe('12.5%');
    expect(formatCell('1234567.125', { type: 'Decimal' }, dhaka)).toBe('1,234,567.125');
    expect(formatCell(1234567, { type: 'Integer' }, dhaka)).toBe('1,234,567');
    expect(groupDigits('-1000')).toBe('-1,000');
  });

  it('formats a date by string, so no zone can shift the day', () => {
    expect(formatCell('2026-10-01', { type: 'Date' }, 'Pacific/Kiritimati')).toBe('01 Oct 2026');
    expect(formatReportDate('2026-01-31')).toBe('31 Jan 2026');
  });

  it('renders an instant in the zone the payload names', () => {
    const instant = '2026-10-14T23:30:00+00:00';
    expect(formatCell(instant, { type: 'Instant' }, 'Asia/Dhaka')).toBe('15 Oct 2026 05:30');
    expect(formatCell(instant, { type: 'Instant' }, 'Europe/London')).toBe('15 Oct 2026 00:30');
  });

  it('renders booleans as words and text as text', () => {
    expect(formatCell(true, { type: 'Boolean' }, dhaka)).toBe('Yes');
    expect(formatCell(false, { type: 'Boolean' }, dhaka)).toBe('No');
    expect(formatCell('<b>x</b>', { type: 'Text' }, dhaka)).toBe('<b>x</b>');
    expect(formatCell('anything', { type: 'FutureType' }, dhaka)).toBe('anything');
  });
});

describe('plotting', () => {
  it('gives the chart a number only for plotting', () => {
    expect(toChartNumber({ amount: '12.50', currencyCode: 'BDT' })).toBe(12.5);
    expect(toChartNumber(null)).toBeNull();
  });
});

describe('compatiblePeriods — U-E1 (mirrors ReportSchedulePeriods.IsCompatible)', () => {
  it('offers only forward periods to a forward-looking report', () => {
    expect(compatiblePeriods({ kind: 'Period', defaultPeriod: 'Next30Days' })).toEqual([
      'Next30Days',
      'Next90Days',
    ]);
  });

  it('offers Today or its own default to an as-at report', () => {
    expect(compatiblePeriods({ kind: 'AsAt', defaultPeriod: 'Today' })).toEqual(['Today']);
  });

  it('offers any backward window, and Today only when it is the default', () => {
    const periods = compatiblePeriods({ kind: 'Period', defaultPeriod: 'MonthToDate' });
    expect(periods).toContain('PreviousMonth');
    expect(periods).not.toContain('Today');
    expect(periods).not.toContain('Next30Days');
    expect(compatiblePeriods({ kind: 'Period', defaultPeriod: 'Today' })).toContain('Today');
  });
});

describe('varianceDirection — the server word, never the sign (HO-RPT-001-FU1)', () => {
  it.each([['Over'], ['Short'], ['Matched']])('%s is a known direction', (word) => {
    expect(asVarianceDirection(word)).toBe(word);
  });

  it.each([[null], [undefined], [''], ['Mismatch'], ['over'], [350]])(
    '%s is not a direction (so nothing-to-compare is never "Matched")',
    (value) => {
      expect(asVarianceDirection(value)).toBeNull();
    },
  );
});
