import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import { RosterWeekView, type RosterDayCell } from '@/components/roster/RosterWeekView';

const WEEK = [
  '2027-01-04',
  '2027-01-05',
  '2027-01-06',
  '2027-01-07',
  '2027-01-08',
  '2027-01-09',
  '2027-01-10',
];
const ROWS = [{ staffAccountId: 's1', staffFullName: 'Nadia Nurse' }];

describe('RosterWeekView — ⭐ HO-ADM-002 §11.4, ADR-FE-017, UI-1/UI-6', () => {
  it('U-D1: renders a real <table> with row/column headers AND a day-grouped list — both always in the DOM, CSS-toggled, never JS-conditional on viewport', () => {
    const cellFor = (): RosterDayCell => ({ intervals: [], isOnLeave: false, exceptionLabels: [] });
    render(
      <RosterWeekView
        weekDates={WEEK}
        rows={ROWS}
        cellFor={cellFor}
        onPrevWeek={vi.fn()}
        onNextWeek={vi.fn()}
        onThisWeek={vi.fn()}
        isLoading={false}
        isError={false}
        onRetry={vi.fn()}
      />,
    );

    const table = screen.getByRole('table');
    expect(within(table).getByRole('columnheader', { name: 'Staff' })).toBeInTheDocument();
    expect(within(table).getByRole('rowheader', { name: 'Nadia Nurse' })).toBeInTheDocument();

    // The day-grouped list (below-768px layout) is also in the DOM, not conditionally rendered.
    expect(
      screen.getByRole('region', { name: /roster \(scrolls horizontally\)/i }).closest('div'),
    ).toHaveClass('hidden');
    const sections = document.querySelectorAll('section');
    expect(sections.length).toBe(WEEK.length);
  });

  it('U-D2: leave, an extra shift and an outside-hours shift are each shown as text, never colour alone', () => {
    const cellFor = (_staffAccountId: string, date: string): RosterDayCell => {
      if (date === WEEK[0]) return { intervals: [], isOnLeave: true, exceptionLabels: [] };
      if (date === WEEK[1])
        return { intervals: [], isOnLeave: false, exceptionLabels: ['Extra shift'] };
      if (date === WEEK[2]) {
        return {
          intervals: [{ label: '09:00–17:00', outsideOpeningHours: true }],
          isOnLeave: false,
          exceptionLabels: [],
        };
      }
      return { intervals: [], isOnLeave: false, exceptionLabels: [] };
    };
    render(
      <RosterWeekView
        weekDates={WEEK}
        rows={ROWS}
        cellFor={cellFor}
        onPrevWeek={vi.fn()}
        onNextWeek={vi.fn()}
        onThisWeek={vi.fn()}
        isLoading={false}
        isError={false}
        onRetry={vi.fn()}
      />,
    );

    expect(screen.getAllByText('On leave').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Extra shift').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Outside opening hours').length).toBeGreaterThan(0);
  });

  it('shows an empty state when no staff are rostered', () => {
    render(
      <RosterWeekView
        weekDates={WEEK}
        rows={[]}
        cellFor={() => ({ intervals: [], isOnLeave: false, exceptionLabels: [] })}
        onPrevWeek={vi.fn()}
        onNextWeek={vi.fn()}
        onThisWeek={vi.fn()}
        isLoading={false}
        isError={false}
        onRetry={vi.fn()}
      />,
    );
    expect(screen.getByText(/no staff rostered/i)).toBeInTheDocument();
  });
});
