import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { VaccinationStatusBadge } from '@/components/cat/VaccinationStatusBadge';
import type { VaccinationStatus } from '@/lib/api/cats';

describe('VaccinationStatusBadge', () => {
  it.each<[VaccinationStatus, string]>([
    ['Current', 'Current'],
    ['DueSoon', 'Due soon'],
    ['Overdue', 'Overdue'],
  ])('U10 — %s renders the correct label and an icon, not colour alone', (status, label) => {
    const { container } = render(<VaccinationStatusBadge status={status} />);
    expect(screen.getByText(label)).toBeInTheDocument();
    // Every status pairs an icon (lucide renders an <svg>) with the text — UI-6.
    expect(container.querySelector('svg')).toBeInTheDocument();
  });
});
