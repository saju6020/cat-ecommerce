import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { EffectiveDatedHistory } from '@/components/admin/EffectiveDatedHistory';

describe('EffectiveDatedHistory — ⭐ HO-ADM-002 §11.3, ADR-FE-019', () => {
  it('U-C1: groups rows into Current / Scheduled / Past, with no edit or delete control anywhere', () => {
    render(
      <EffectiveDatedHistory
        timeZoneId="Asia/Dhaka"
        nowIso="2027-06-01T00:00:00Z"
        rows={[
          { id: 'past', effectiveFrom: '2026-01-01T00:00:00Z', label: '৳1,000.00' },
          { id: 'current', effectiveFrom: '2027-01-01T00:00:00Z', label: '৳1,500.00' },
          { id: 'scheduled', effectiveFrom: '2028-01-01T00:00:00Z', label: '৳1,800.00' },
        ]}
      />,
    );

    expect(screen.getByText('Current')).toBeInTheDocument();
    expect(screen.getByText('Scheduled')).toBeInTheDocument();
    expect(screen.getByText('Past')).toBeInTheDocument();
    expect(screen.getByText('৳1,500.00')).toBeInTheDocument();
    expect(screen.getByText('৳1,800.00')).toBeInTheDocument();
    expect(screen.getByText('৳1,000.00')).toBeInTheDocument();

    // No edit or delete control anywhere — append-only (ADR-FE-019).
    expect(screen.queryByRole('button', { name: /edit/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /delete/i })).not.toBeInTheDocument();
  });

  it('shows "Not priced" text when there is no current row at all', () => {
    render(
      <EffectiveDatedHistory
        timeZoneId="Asia/Dhaka"
        nowIso="2027-06-01T00:00:00Z"
        rows={[{ id: 'future', effectiveFrom: '2028-01-01T00:00:00Z', label: '৳1,800.00' }]}
      />,
    );
    expect(screen.getByText(/not priced/i)).toBeInTheDocument();
  });
});
