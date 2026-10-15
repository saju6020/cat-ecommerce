import { describe, expect, it } from 'vitest';
import { formatAxisTick } from '@/lib/format/report';
import { chartAxisTitle } from '@/components/reports/ReportChart';

describe('chart Y axis (owner 2026-10-14: show the taka sign)', () => {
  it('shows a compact label with the currency sign', () => {
    expect(formatAxisTick(0, 'BDT')).toBe('৳0');
    expect(formatAxisTick(8000, 'BDT')).toBe('৳8K');
    expect(formatAxisTick(12500, 'BDT')).toBe('৳12.5K');
    expect(formatAxisTick(1_250_000, 'BDT')).toBe('৳1.3M');
  });

  it('is a plain compact number when the column has no currency', () => {
    expect(formatAxisTick(12500)).toBe('12.5K');
    expect(formatAxisTick(40)).toBe('40');
  });

  it('survives an unknown currency code', () => {
    expect(formatAxisTick(5, 'NOPE-CODE')).toBe('5');
  });

  it('names the currency in the axis title', () => {
    expect(chartAxisTitle({ label: 'Gross', unit: null }, 'BDT')).toBe('BDT');
    expect(chartAxisTitle({ label: 'Wait', unit: 'min' }, undefined)).toBe('min');
    expect(chartAxisTitle({ label: 'Invoices', unit: null }, undefined)).toBe('Invoices');
  });
});
