import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { AgeDisplay } from '@/components/cat/AgeDisplay';

const ASOF = new Date(2026, 8, 25); // 25 Sep 2026 — fixed "now" for deterministic assertions

describe('AgeDisplay', () => {
  it('U1 — exact DOB: renders the exact age, not marked approximate', () => {
    render(<AgeDisplay dateOfBirth="2023-06-25" dateOfBirthPrecision="Exact" asOf={ASOF} />);
    expect(screen.getByText('3 years 3 months')).toBeInTheDocument();
    expect(screen.queryByText(/estimated/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/~/)).not.toBeInTheDocument();
  });

  it('U2 — estimated DOB: marked approximate (US-CAT-01 AC2)', () => {
    render(<AgeDisplay dateOfBirth="2023-06-25" dateOfBirthPrecision="Estimated" asOf={ASOF} />);
    const text = screen.getByText(/estimated/i);
    expect(text).toHaveTextContent('~3 years (estimated)');
  });

  it('U3 — no DOB: renders "Unknown" with no crash', () => {
    render(<AgeDisplay dateOfBirth={null} dateOfBirthPrecision="Estimated" asOf={ASOF} />);
    expect(screen.getByText('Unknown')).toBeInTheDocument();
  });
});
