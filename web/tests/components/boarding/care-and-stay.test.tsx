import { readdirSync, readFileSync, statSync } from 'node:fs';
import path from 'node:path';
import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useState, type ReactElement } from 'react';
import { OccupancyDayList, OccupancyGrid } from '@/components/boarding/board/OccupancyGrid';
import { DepositSummary } from '@/components/boarding/stay/DepositSummary';
import { StayCharges } from '@/components/boarding/stay/StayCharges';
import { ObservationSheet } from '@/components/boarding/tasks/ObservationSheet';
import { TaskCard } from '@/components/boarding/tasks/TaskCard';
import { UnitTaskGroup } from '@/components/boarding/tasks/UnitTaskGroup';
import { SignaturePad } from '@/components/signature/SignaturePad';
import { formatOverdue, formatStayDates } from '@/lib/format/boarding';
import type { BoardingOccupancy, CareTask, TaskUnitGroup } from '@/lib/api/types';
import { EMPTY_SIGNATURE, boundStrokes, type SignatureDraft } from '@/lib/validation/boarding';

/** HO-BRD-002 §15 U-B, U-C, U-D, U-E (signature), U-X4 — components with the server's answers as props. */

const money = (amount: string) => ({ amount, currencyCode: 'BDT' });

function wrap(ui: ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

const task = (over: Partial<CareTask> = {}): CareTask => ({
  id: 't1',
  stayId: 's1',
  catId: 'c1',
  catName: 'Mitu',
  kind: 'Feed',
  title: 'Feed ⅓ cup dry — own food',
  dueOn: '2026-10-14T03:00:00Z',
  dueLocalTime: '09:00',
  status: 'Pending',
  isOverdue: false,
  minutesLate: null,
  assignedToName: 'Tanvir Islam',
  assignedToMe: true,
  completedOn: null,
  completedByName: null,
  skipReason: null,
  ...over,
});

const handlers = { onUndo: vi.fn(), onSkip: vi.fn(), onObserve: vi.fn(), onIncident: vi.fn() };

describe('U-D1/D3/D7: the task card', () => {
  it('completes with ONE pointer event on Done — no dialog — and again by keyboard', async () => {
    const onDone = vi.fn();
    render(
      <ul>
        <TaskCard task={task()} zone="Asia/Dhaka" inFlight={false} onDone={onDone} {...handlers} />
      </ul>,
    );
    const done = screen.getByRole('button', { name: /^Done/ });
    // jsdom has no layout: this pins the class that makes the button 48 px; the real height is asserted in
    // `boarding-staff.spec.ts` (E-C2, boundingBox >= 48).
    expect(done.className).toMatch(/min-h-12/);
    await userEvent.click(done);
    expect(onDone).toHaveBeenCalledTimes(1);
    expect(screen.queryByRole('dialog')).toBeNull();
    done.focus();
    await userEvent.keyboard('{Enter}');
    expect(onDone).toHaveBeenCalledTimes(2);
  });

  it('shows the overdue chip as text from the server minutes', () => {
    render(
      <ul>
        <TaskCard
          task={task({ isOverdue: true, minutesLate: 25 })}
          zone="Asia/Dhaka"
          inFlight={false}
          onDone={vi.fn()}
          {...handlers}
        />
      </ul>,
    );
    expect(screen.getByText('25 min late')).toBeInTheDocument();
    expect(formatOverdue(65)).toBe('1 h 5 min late');
  });

  it('offers Undo only while the request is in flight, and shows a refusal on the card', () => {
    const { rerender } = render(
      <ul>
        <TaskCard
          task={task({
            status: 'Done',
            completedByName: 'Tanvir Islam',
            completedOn: '2026-10-14T03:05:00Z',
          })}
          zone="Asia/Dhaka"
          inFlight
          onDone={vi.fn()}
          {...handlers}
        />
      </ul>,
    );
    expect(screen.getByRole('button', { name: /undo/i })).toBeInTheDocument();
    rerender(
      <ul>
        <TaskCard
          task={task()}
          zone="Asia/Dhaka"
          inFlight={false}
          error="This task was already completed by Rafiq Uddin at 10:32."
          onDone={vi.fn()}
          {...handlers}
        />
      </ul>,
    );
    expect(screen.getByRole('alert')).toHaveTextContent(/rafiq uddin/i);
    expect(screen.queryByRole('button', { name: /undo/i })).toBeNull();
    expect(screen.getByRole('button', { name: /^Done/ })).toBeEnabled();
  });
});

describe('U-D5/D6: a unit group', () => {
  const group: TaskUnitGroup = {
    unitId: 'u1',
    unitCode: 'F-1',
    unitName: 'Family 1',
    block: 'Garden wing',
    isIsolation: false,
    isolationNote: null,
    cats: [
      { catId: 'a', catName: 'Noodle', catIsHighRisk: false, notEating: true },
      {
        catId: 'b',
        catName: 'Pepper',
        catIsHighRisk: true,
        handlingNote: 'Bites when restrained',
        notEating: false,
      },
    ],
    tasks: [task()],
  };

  it('lists the high-risk cat first with its handling note, and renders the server’s not-eating flag', () => {
    render(
      <UnitTaskGroup
        group={group}
        zone="Asia/Dhaka"
        inFlight={{}}
        errors={{}}
        onDone={vi.fn()}
        {...handlers}
      />,
    );
    const catList = screen.getByRole('list', { name: 'Cats in Family 1' });
    expect(catList.querySelector('li')).toHaveTextContent('Pepper');
    expect(screen.getByText('Bites when restrained')).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent(/not eating — flagged: noodle/i);
  });

  it('shows the isolation banner', () => {
    render(
      <UnitTaskGroup
        group={{ ...group, isIsolation: true, isolationNote: 'Isolation — gloves and gown.' }}
        zone="Asia/Dhaka"
        inFlight={{}}
        errors={{}}
        onDone={vi.fn()}
        {...handlers}
      />,
    );
    expect(screen.getByText(/gloves and gown/i)).toBeInTheDocument();
  });
});

describe('U-D4/D5: the observation sheet', () => {
  it('keeps its values when its parent re-renders with new data, and shows the server flag', async () => {
    function Host() {
      const [tick, setTick] = useState(0);
      return (
        <>
          <button onClick={() => setTick(tick + 1)}>refresh {tick}</button>
          <ObservationSheet
            open
            onOpenChange={vi.fn()}
            stayId="s1"
            catId="c1"
            catName="Noodle"
            notEating
          />
        </>
      );
    }
    wrap(<Host />);
    expect(screen.getByRole('status')).toHaveTextContent('Not eating — flagged');
    await userEvent.click(screen.getByRole('radio', { name: 'A quarter eaten' }));
    await userEvent.type(screen.getByLabelText('Notes (optional)'), 'Sniffed it');
    fireEvent.click(screen.getByText(/refresh/));
    expect(screen.getByLabelText('Notes (optional)')).toHaveValue('Sniffed it');
    expect(screen.getByRole('radio', { name: 'A quarter eaten' })).toHaveAttribute(
      'aria-checked',
      'true',
    );
  });
});

describe('U-E2: the signature pad (ADR-FE-103)', () => {
  function Host({ onValue }: { onValue: (v: SignatureDraft) => void }) {
    const [value, setValue] = useState<SignatureDraft>(EMPTY_SIGNATURE);
    return (
      <SignaturePad
        idPrefix="t"
        value={value}
        onChange={(v) => {
          setValue(v);
          onValue(v);
        }}
      />
    );
  }

  it('shows the typed-name path beside the canvas, and captures numeric strokes — no image', () => {
    const seen: SignatureDraft[] = [];
    render(<Host onValue={(v) => seen.push(v)} />);
    expect(screen.getByLabelText(/type your name instead/i)).toBeInTheDocument();
    const canvas = screen.getByTestId('t-canvas');
    Object.defineProperty(canvas, 'getBoundingClientRect', {
      value: () => ({ left: 0, top: 0, width: 480, height: 160, right: 480, bottom: 160 }),
    });
    fireEvent.pointerDown(canvas, { clientX: 10, clientY: 20, pointerId: 1 });
    fireEvent.pointerMove(canvas, { clientX: 60, clientY: 80, pointerId: 1 });
    fireEvent.pointerUp(canvas, { pointerId: 1 });
    const last = seen.at(-1)!;
    expect(last.method).toBe('Drawn');
    expect(last.strokes[0]).toEqual([
      [10, 20],
      [60, 80],
    ]);
    expect(last.strokes.flat(2).every((n) => Number.isInteger(n))).toBe(true);
    expect(document.querySelector('canvas')?.toDataURL).toBeDefined();
  });

  it('bounds strokes: numbers only, clamped, and null when nothing survives', () => {
    expect(boundStrokes([[[Number.NaN, 1]]])).toBeNull();
    expect(boundStrokes([[[20000, -5]]])).toEqual([[[10000, 0]]]);
  });

  it('the typed path completes with a name and the confirmation', async () => {
    const seen: SignatureDraft[] = [];
    render(<Host onValue={(v) => seen.push(v)} />);
    await userEvent.click(screen.getByLabelText(/type your name instead/i));
    await userEvent.type(screen.getByLabelText(/name of the person signing/i), 'Sam');
    await userEvent.click(screen.getByLabelText(/i confirm this is my signature/i));
    expect(seen.at(-1)).toMatchObject({ method: 'Typed', signerName: 'Sam', confirmed: true });
  });
});

describe('U-B1/B5: deposit states and charges', () => {
  it.each([
    ['Awaiting', 'Awaiting payment'],
    ['PendingApproval', 'Payment being checked'],
    ['Received', 'Deposit received'],
  ] as const)('%s reads as plain language', (state, text) => {
    wrap(
      <DepositSummary
        status="Reserved"
        zone="Asia/Dhaka"
        balanceDue={money('6300.00')}
        deposit={{
          required: money('2700.00'),
          accepted: money('0.00'),
          outstanding: money('2700.00'),
          state,
          payBy: '2026-10-16T10:00:00Z',
        }}
      />,
    );
    expect(screen.getAllByText(text).length).toBeGreaterThan(0);
  });

  it('charges render the server lines and invoice links with no payment control', () => {
    render(
      <StayCharges
        balanceDue={money('100.00')}
        depositReceived={money('2700.00')}
        charges={[
          { id: '1', kind: 'RateUnit', description: 'Week 1', amount: money('6000.00') },
          {
            id: '2',
            kind: 'EarlyCheckoutCredit',
            description: 'Early check-out credit',
            amount: money('-5200.00'),
          },
        ]}
        invoices={[
          {
            id: 'i1',
            number: 'INV-1',
            kind: 'Final',
            total: money('800.00'),
            issuedOn: '2026-10-01T00:00:00Z',
          },
        ]}
      />,
    );
    expect(screen.getByText('-৳5,200.00')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /your invoice inv-1/i })).toBeInTheDocument();
    expect(screen.queryByRole('button')).toBeNull();
  });
});

describe('U-C3/C4: the board in words, and its list equivalent', () => {
  const data: BoardingOccupancy = {
    from: '2026-10-12',
    to: '2026-10-14',
    branchTimeZoneId: 'Asia/Dhaka',
    days: ['2026-10-12', '2026-10-13', '2026-10-14'],
    units: [
      {
        unitId: 'u1',
        code: 'F-1',
        name: 'Family 1',
        unitType: 'Family',
        block: 'Garden',
        stays: [
          {
            stayId: 's1',
            catNames: ['Noodle'],
            status: 'CheckedIn',
            fromOn: '2026-10-12',
            toOn: '2026-10-13',
            awayInSurgery: false,
          },
        ],
        maintenance: [],
        turnaroundDays: ['2026-10-14'],
      },
      {
        unitId: 'u2',
        code: 'S-3',
        name: 'Standard 3',
        unitType: 'Standard',
        block: 'Main',
        stays: [],
        maintenance: [
          { blockId: 'm1', fromOn: '2026-10-12', toOn: '2026-10-14', reason: 'Deep clean' },
        ],
        turnaroundDays: [],
      },
    ],
  };

  it('labels stays, maintenance and turnaround with words and links the stay', () => {
    render(<OccupancyGrid data={data} />);
    expect(screen.getByRole('link', { name: /noodle · in the care centre/i })).toHaveAttribute(
      'href',
      '/staff/boarding/stays/s1',
    );
    expect(screen.getByText(/maintenance — deep clean/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/kept free for cleaning/i)).toBeInTheDocument();
  });

  it('the day list names who is in each unit', () => {
    render(<OccupancyDayList data={data} />);
    expect(screen.getAllByText(/noodle \(in the care centre\)/i).length).toBe(2);
    expect(screen.getAllByText(/maintenance — deep clean/i).length).toBe(3);
  });
});

describe('formats', () => {
  it('formats a stay’s dates and nights', () => {
    expect(formatStayDates('2026-10-12', '2026-10-19')).toBe('12–19 Oct 2026 · 7 nights');
    expect(formatStayDates('2026-10-30', '2026-11-02')).toBe('30 Oct – 2 Nov 2026 · 3 nights');
    expect(formatStayDates('2026-10-12', '2026-10-13')).toBe('12–13 Oct 2026 · 1 night');
  });
});

describe('U-X4: no payment control anywhere in the boarding source (ADR-FE-106)', () => {
  function walk(dir: string, out: string[] = []): string[] {
    for (const name of readdirSync(dir)) {
      const full = path.join(dir, name);
      if (statSync(full).isDirectory()) walk(full, out);
      else if (/\.(tsx?|ts)$/.test(name)) out.push(full);
    }
    return out;
  }
  const roots = [
    'components/boarding',
    'components/signature',
    'app/(portal)/boarding',
    'app/(portal)/portal/boarding',
    'app/(staff)/staff/boarding',
    'app/(staff)/staff/admin/boarding',
  ].map((r) => path.resolve(process.cwd(), r));
  const files = roots.flatMap((r) => walk(r));
  const source = files.map((f) => readFileSync(f, 'utf8')).join('\n');

  it('has no card field, cvv, pay-now, gateway or token control, and no upload or FormData', () => {
    expect(files.length).toBeGreaterThan(40);
    expect(source).not.toMatch(
      /cvv|cvc|card[-_ ]?number|autocomplete="cc-|pay now|pay online|payment link|payment gateway|new FormData|type="file"|<input[^>]*accept=/i,
    );
    expect(source).not.toMatch(/\btoken\b/i);
  });
});
