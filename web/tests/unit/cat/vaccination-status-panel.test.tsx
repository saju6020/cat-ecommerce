import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { VaccinationStatusPanel } from '@/components/cat/VaccinationStatusPanel';
import type { CatVaccinationStatus } from '@/lib/api/cats';

describe('VaccinationStatusPanel', () => {
  it('U11 — DueSoon shows the due date and a booking link (US-CAT-07 AC2)', () => {
    const status: CatVaccinationStatus = {
      catId: 'cat-1',
      overallStatus: 'DueSoon',
      earliestNextDueOn: '2026-10-10',
      bookingEligible: true,
      vaccines: [
        {
          vaccineId: 'v1',
          vaccineCode: 'RABIES',
          vaccineName: 'Rabies',
          status: 'DueSoon',
          nextDueOn: '2026-10-10',
          reason: null,
        },
      ],
    };
    render(
      <VaccinationStatusPanel status={status} bookingHref="/staff/appointments/new?catId=cat-1" />,
    );
    expect(screen.getByText(/next due/i)).toHaveTextContent('10 Oct 2026');
    expect(screen.getByRole('link', { name: 'Book a vaccination' })).toHaveAttribute(
      'href',
      '/staff/appointments/new?catId=cat-1',
    );
  });

  it('U12 — Overdue warns that boarding will be blocked (US-CAT-07 AC3)', () => {
    const status: CatVaccinationStatus = {
      catId: 'cat-1',
      overallStatus: 'Overdue',
      earliestNextDueOn: null,
      bookingEligible: false,
      vaccines: [
        {
          vaccineId: 'v1',
          vaccineCode: 'RABIES',
          vaccineName: 'Rabies',
          status: 'Overdue',
          nextDueOn: '2026-01-01',
          reason: null,
        },
      ],
    };
    render(<VaccinationStatusPanel status={status} bookingHref="" />);
    expect(screen.getByRole('alert')).toHaveTextContent(/boarding will be blocked/i);
  });

  it('U13 — a core vaccine never administered is listed as overdue, not omitted', () => {
    const status: CatVaccinationStatus = {
      catId: 'cat-1',
      overallStatus: 'Overdue',
      earliestNextDueOn: null,
      bookingEligible: false,
      vaccines: [
        {
          vaccineId: 'v1',
          vaccineCode: 'RABIES',
          vaccineName: 'Rabies',
          status: 'Overdue',
          nextDueOn: null,
          reason: 'NeverAdministered',
        },
      ],
    };
    render(<VaccinationStatusPanel status={status} bookingHref="" />);
    expect(screen.getByText('Rabies')).toBeInTheDocument();
    expect(screen.getByText('Never administered')).toBeInTheDocument();
    expect(screen.getByText('Overdue')).toBeInTheDocument();
  });
});
