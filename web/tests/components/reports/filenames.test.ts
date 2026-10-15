import { describe, expect, it } from 'vitest';
import { safeFilename } from '@/lib/download/reportExport';
import { exportBaseName } from '@/components/reports/ReportViewer';

describe('safeFilename — a server filename becomes a harmless basename', () => {
  it('keeps an ordinary name', () => {
    expect(safeFilename('purrcare-daily-2026-10-14-to-2026-10-14.csv')).toBe(
      'purrcare-daily-2026-10-14-to-2026-10-14.csv',
    );
  });

  it('decodes a percent-encoded name', () => {
    expect(safeFilename('report%20one.pdf')).toBe('reportone.pdf');
  });

  it('does not throw on a malformed escape', () => {
    expect(safeFilename('%E0%A4%A.csv')).toBeNull();
  });

  it('keeps only the basename and drops path tricks', () => {
    expect(safeFilename('..%2F..%2Fetc%2Fpasswd')).toBe('passwd');
    expect(safeFilename('C:\\temp\\evil.csv')).toBe('evil.csv');
    expect(safeFilename('..')).toBeNull();
    expect(safeFilename(undefined)).toBeNull();
  });
});

describe('exportBaseName — the fallback filename uses safe characters only', () => {
  it('builds purrcare-{key}-{from}-to-{to}', () => {
    expect(exportBaseName('clinic-daily-schedule', { from: '2026-10-14', to: '2026-10-15' })).toBe(
      'purrcare-clinic-daily-schedule-2026-10-14-to-2026-10-15',
    );
  });

  it('strips anything else from raw URL values', () => {
    expect(exportBaseName('k', { from: '../../x"', to: '2026-10-15\r\nX: y' })).toBe(
      'purrcare-k-x-to-2026-10-15Xy',
    );
  });
});
