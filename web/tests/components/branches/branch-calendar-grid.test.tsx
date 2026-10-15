import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { BranchMonthCalendar } from '@/components/branches/BranchMonthCalendar';
import { BranchCalendarDayList } from '@/components/branches/BranchCalendarDayList';
import { BranchCalendarMonthNav } from '@/components/branches/BranchCalendarMonthNav';
import { buildBranchCalendarMonth } from '@/lib/format/branch-calendar';
import type { BranchClosure, OpeningInterval } from '@/lib/api/admin-branches';

const WEEKLY: OpeningInterval[] = (
  ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'] as const
).map((dayOfWeek) => ({ dayOfWeek, opens: '09:00', closes: '13:00' }));

function closure(overrides: Partial<BranchClosure>): BranchClosure {
  return {
    id: 'c',
    branchId: 'b',
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

/** January 2027 (1 Jan is a Friday): open weekdays from the server, with a holiday, a closure, a
 *  reduced day and a plain closed day, and "today" on the 13th. */
function januaryDays(reason = 'Refurbishment') {
  const intervals = [];
  for (let d = 1; d <= 31; d += 1) {
    const date = `2027-01-${String(d).padStart(2, '0')}`;
    const weekday = new Date(`${date}T12:00:00Z`).getUTCDay();
    if (weekday === 0 || weekday === 6 || d === 1 || d === 5) continue;
    intervals.push({ start: `${date}T03:00:00Z`, end: `${date}T07:00:00Z` });
  }
  return buildBranchCalendarMonth({
    month: '2027-01',
    timeZoneId: 'Asia/Dhaka',
    weeklyHours: WEEKLY,
    intervals,
    closures: [
      closure({ id: '1', kind: 'PublicHoliday', reason: "New Year's Day" }),
      closure({ id: '2', startsOn: '2027-01-05', endsOn: '2027-01-05', reason }),
      closure({
        id: '3',
        startsOn: '2027-01-06',
        endsOn: '2027-01-06',
        reason: 'Training',
        partialFrom: '11:00',
        partialTo: '13:00',
      }),
    ],
    today: '2027-01-13',
  });
}

function renderGrid(overrides: Partial<React.ComponentProps<typeof BranchMonthCalendar>> = {}) {
  const onMonthChange = vi.fn();
  const view = render(
    <>
      <h3 id="month-heading">January 2027</h3>
      <BranchMonthCalendar
        days={januaryDays()}
        month="2027-01"
        monthHeadingId="month-heading"
        onMonthChange={onMonthChange}
        {...overrides}
      />
    </>,
  );
  return { onMonthChange, ...view };
}

const dayCells = () =>
  screen.getAllByRole('gridcell').filter((c) => c.getAttribute('aria-disabled') !== 'true');

describe('BranchMonthCalendar — U-CAL9', () => {
  it('is a labelled read-only grid with Monday-first column headers and 4 leading blanks', () => {
    renderGrid();
    const grid = screen.getByRole('grid', { name: 'January 2027' });
    expect(grid).toHaveAttribute('aria-readonly', 'true');
    const headers = within(grid).getAllByRole('columnheader');
    expect(headers.map((h) => h.textContent)).toEqual([
      'Mon',
      'Tue',
      'Wed',
      'Thu',
      'Fri',
      'Sat',
      'Sun',
    ]);
    expect(headers.map((h) => h.getAttribute('abbr'))[0]).toBe('Monday');
    const all = screen.getAllByRole('gridcell');
    expect(all.filter((c) => c.getAttribute('aria-disabled') === 'true').length).toBe(4);
    expect(dayCells()).toHaveLength(31);
  });

  it('marks today and names each cell with its full date and status sentence', () => {
    renderGrid();
    const today = dayCells()[12]!;
    expect(today).toHaveAttribute('aria-current', 'date');
    expect(within(today).getAllByText('Today').length).toBeGreaterThan(0);
    expect(dayCells().filter((c) => c.hasAttribute('aria-current'))).toHaveLength(1);

    expect(
      screen.getByRole('gridcell', {
        name: /Friday 1 January 2027\. Closed — Public holiday: New Year's Day/,
      }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole('gridcell', { name: /Tuesday 5 January 2027\. Closed — Refurbishment/ }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole('gridcell', {
        name: /Wednesday 6 January 2027\. Reduced hours: 09:00–13:00\. Closed 11:00–13:00: Training/,
      }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole('gridcell', {
        name: /Sunday 3 January 2027\. Closed \(regular weekly closure\)/,
      }),
    ).toBeInTheDocument();
  });

  it('shows every status as words, not colour alone', () => {
    renderGrid();
    const grid = screen.getByRole('grid');
    expect(within(grid).getAllByText('Open').length).toBeGreaterThan(0);
    expect(within(grid).getAllByText('Closed — Public holiday').length).toBe(1);
    expect(within(grid).getAllByText('Closed — closure').length).toBe(1);
    expect(within(grid).getAllByText('Closed (regular weekly closure)').length).toBeGreaterThan(0);
  });
});

describe('BranchMonthCalendar keyboard — U-CAL10', () => {
  it('has exactly one Tab stop, starting on today', () => {
    renderGrid();
    const stops = dayCells().filter((c) => c.tabIndex === 0);
    expect(stops).toHaveLength(1);
    expect(stops[0]).toHaveAttribute('aria-current', 'date');
  });

  it('starts on the 1st when today is not in the month', () => {
    renderGrid({ days: januaryDays().map((d) => ({ ...d, isToday: false })) });
    expect(dayCells()[0]).toHaveAttribute('tabindex', '0');
  });

  it('arrows move by 1 and 7 days and stop at the month bounds without wrapping', async () => {
    const user = userEvent.setup();
    renderGrid({ days: januaryDays().map((d) => ({ ...d, isToday: false })) });
    const cells = dayCells();
    cells[0]!.focus();
    await user.keyboard('{ArrowRight}');
    expect(cells[1]).toHaveFocus();
    await user.keyboard('{ArrowDown}');
    expect(cells[8]).toHaveFocus();
    await user.keyboard('{ArrowUp}{ArrowLeft}');
    expect(cells[0]).toHaveFocus();
    await user.keyboard('{ArrowLeft}{ArrowUp}');
    expect(cells[0]).toHaveFocus();
    expect(cells.filter((c) => c.tabIndex === 0)).toEqual([cells[0]]);
    cells[30]!.focus();
    await user.keyboard('{ArrowRight}{ArrowDown}');
    expect(cells[30]).toHaveFocus();
  });

  it('Home and End go to the first and last in-month day of the week row', async () => {
    const user = userEvent.setup();
    renderGrid({ days: januaryDays().map((d) => ({ ...d, isToday: false })) });
    const cells = dayCells();
    // Row 1 holds only Fri 1 - Sun 3.
    cells[1]!.focus();
    await user.keyboard('{Home}');
    expect(cells[0]).toHaveFocus();
    await user.keyboard('{End}');
    expect(cells[2]).toHaveFocus();
    // Week of Mon 11 - Sun 17.
    cells[12]!.focus();
    await user.keyboard('{Home}');
    expect(cells[10]).toHaveFocus();
    await user.keyboard('{End}');
    expect(cells[16]).toHaveFocus();
    // Last row holds only Mon 25 - Sun 31.
    cells[30]!.focus();
    await user.keyboard('{Home}');
    expect(cells[24]).toHaveFocus();
  });

  it('PageUp/PageDown call onMonthChange, and focus lands on the clamped same day after re-render', async () => {
    const user = userEvent.setup();
    const onMonthChange = vi.fn();
    const props = { monthHeadingId: 'month-heading', onMonthChange };
    const january = januaryDays().map((d) => ({ ...d, isToday: false }));
    const { rerender } = render(
      <>
        <h3 id="month-heading">x</h3>
        <BranchMonthCalendar days={january} month="2027-01" {...props} />
      </>,
    );
    const cells = dayCells();
    cells[30]!.focus();
    await user.keyboard('{PageDown}');
    expect(onMonthChange).toHaveBeenCalledWith(1, 31);
    await user.keyboard('{PageUp}');
    expect(onMonthChange).toHaveBeenLastCalledWith(-1, 31);

    const february = Array.from({ length: 28 }, (_, i) => ({
      ...january[0]!,
      date: `2027-02-${String(i + 1).padStart(2, '0')}`,
      isToday: false,
    }));
    rerender(
      <>
        <h3 id="month-heading">x</h3>
        <BranchMonthCalendar days={february} month="2027-02" {...props} />
      </>,
    );
    const next = dayCells();
    expect(next).toHaveLength(28);
    expect(next[27]).toHaveFocus();
    expect(next.filter((c) => c.tabIndex === 0)).toEqual([next[27]]);
  });

  it('a remounted grid focuses restoreFocusDay, clamped, and reports it', () => {
    const onFocusRestored = vi.fn();
    renderGrid({ restoreFocusDay: 99, onFocusRestored });
    expect(dayCells()[30]).toHaveFocus();
    expect(onFocusRestored).toHaveBeenCalledOnce();
  });
});

describe('BranchCalendarDayList — U-CAL13', () => {
  it('is an ordered list of 31 days with the same status text as the grid, no grid role', () => {
    const days = januaryDays();
    render(
      <>
        <h3 id="month-heading">January 2027</h3>
        <BranchCalendarDayList days={days} monthHeadingId="month-heading" />
      </>,
    );
    const list = screen.getByRole('list', { name: 'January 2027' });
    expect(list.tagName).toBe('OL');
    const items = within(list).getAllByRole('listitem');
    expect(items).toHaveLength(31);
    days.forEach((day, i) => expect(items[i]).toHaveTextContent(day.text));
    expect(screen.queryByRole('grid')).not.toBeInTheDocument();
    expect(items[0]).toHaveTextContent('Fri 1 Jan');
    expect(items[12]).toHaveTextContent('Today');
  });
});

describe('closure reason is text — U-CAL15', () => {
  const reason = '<img src=x onerror=alert(1)>';

  it('renders literally in the grid and the list with no element created', () => {
    const days = januaryDays(reason);
    const { container } = render(
      <>
        <h3 id="month-heading">January 2027</h3>
        <BranchMonthCalendar
          days={days}
          month="2027-01"
          monthHeadingId="month-heading"
          onMonthChange={vi.fn()}
        />
        <BranchCalendarDayList days={days} monthHeadingId="month-heading" />
      </>,
    );
    expect(container.querySelector('img')).toBeNull();
    expect(within(screen.getByRole('grid')).getAllByText(reason).length).toBeGreaterThan(0);
    expect(within(screen.getByRole('list')).getByText(`Closed — ${reason}`)).toBeInTheDocument();
  });
});

describe('BranchCalendarMonthNav', () => {
  it('links to the previous, next and current month and states the zone', () => {
    render(
      <BranchCalendarMonthNav
        month="2028-12"
        currentMonth="2027-02"
        timeZoneId="Europe/London"
        basePath="/staff/admin/branches/b/calendar"
        headingId="h"
        hasPastDays
      />,
    );
    expect(screen.getByRole('heading', { name: 'December 2028' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Previous month' })).toHaveAttribute(
      'href',
      '/staff/admin/branches/b/calendar?month=2028-11',
    );
    expect(screen.getByRole('link', { name: 'Next month' })).toHaveAttribute(
      'href',
      '/staff/admin/branches/b/calendar?month=2029-01',
    );
    expect(screen.getByRole('link', { name: 'This month' })).toHaveAttribute(
      'href',
      '/staff/admin/branches/b/calendar?month=2027-02',
    );
    expect(screen.getByText(/Times in Europe\/London\./)).toHaveTextContent(
      'Past days are shown with the current opening hours',
    );
    const legend = screen.getByLabelText('Legend');
    for (const word of [
      'Open',
      'Reduced hours',
      'Closed — Public holiday',
      'Closed — closure',
      'Closed (regular weekly closure)',
    ]) {
      expect(within(legend).getByText(word)).toBeInTheDocument();
    }
  });

  it('hides This month on the current month and the past-days note on a future month', () => {
    render(
      <BranchCalendarMonthNav
        month="2027-02"
        currentMonth="2027-02"
        timeZoneId="Asia/Dhaka"
        basePath="/x"
        headingId="h"
        hasPastDays={false}
      />,
    );
    expect(screen.queryByRole('link', { name: 'This month' })).not.toBeInTheDocument();
    expect(screen.queryByText(/Past days/)).not.toBeInTheDocument();
  });
});
