import type {
  AvailabilityAlternative,
  AvailableUnitType,
  BoardingMoney,
  BoardingUnitType,
  QuoteDiscountLine,
  QuoteRateUnit,
  StayAction,
  StayChargeLine,
  StayQuote,
  BoardingStayStatus,
} from '@/lib/api/types';
import { addDaysIso } from '@/lib/format/appointment';
import { nightsBetween } from '@/lib/format/boarding';
import { zonedLocalToInstant } from '@/lib/format/appointment';
import { BRANCH_INFO, CURRENCY } from './fixtures';

/**
 * `mocks/handlers/boarding/rules.ts` — HO-BRD-002 §12. The MOCK SERVER's rules: pricing (§8.8
 * `BoardingQuoteCalculator`), availability (§8.4/§8.8 `BoardingAvailabilityFinder`) and the stay
 * transition table (§8.5 `StayTransitionRules`). They live here and ONLY here — the real UI never
 * computes any of them (ADR-FE-105, HO-BRD-002 §20.2); this file stands in for the API.
 */

export const LONG_STAY_THRESHOLD_NIGHTS = 14;
export const CHECK_IN_START = '14:00';
export const CHECK_OUT_END = '11:00';
export const TURNAROUND_HOURS = 3;
export const BOOKING_HORIZON_DAYS = 180;
export const MAX_STAY_NIGHTS = 120;
export const DEPOSIT_PERCENT = 30;
export const RESERVATION_HOLD_HOURS = 48;
export const CANCELLATION_WINDOW_HOURS = 72;
export const TASK_OVERDUE_GRACE_MINUTES = 30;

export function money(n: number): BoardingMoney {
  return { amount: n.toFixed(2), currencyCode: CURRENCY };
}

export function num(m: BoardingMoney | { amount: string }): number {
  return Number(m.amount);
}

export function round2(n: number): number {
  return Math.round((n + Number.EPSILON) * 100) / 100;
}

export const UNIT_TYPE_ORDER: BoardingUnitType[] = ['Standard', 'Deluxe', 'Family', 'Isolation'];

export const TYPE_CAPACITY: Record<BoardingUnitType, number> = {
  Standard: 1,
  Deluxe: 2,
  Family: 3,
  Isolation: 1,
};

export interface RateCardValues {
  nightly: number;
  weekly: number;
  monthly: number;
  additionalCat: number;
}

export function quoteStay(
  unitType: BoardingUnitType,
  nights: number,
  catCount: number,
  card: RateCardValues,
  discountPercent: number,
): StayQuote {
  const basis =
    nights < LONG_STAY_THRESHOLD_NIGHTS ? 'Nightly' : nights < 30 ? 'Weekly' : 'Monthly';
  const units: QuoteRateUnit[] = [];
  const discountLines: QuoteDiscountLine[] = [];
  let sequence = 1;
  let night = 1;
  let remaining = nights;
  const push = (kind: QuoteRateUnit['kind'], span: number, amount: number, label: string) => {
    const description = `${label} (nights ${night}–${night + span - 1}) — ${unitType} ${
      kind === 'Month' ? 'monthly' : kind === 'Week' ? 'weekly' : 'nightly'
    } rate`;
    units.push({
      sequence: sequence++,
      kind,
      fromNight: night,
      toNight: night + span - 1,
      description,
      amount: money(amount),
    });
    if (kind !== 'Night' && discountPercent > 0) {
      discountLines.push({
        description: `Long-stay discount ${discountPercent}% — ${label.toLowerCase()}`,
        amount: money(-round2((amount * discountPercent) / 100)),
      });
    }
    night += span;
    remaining -= span;
  };
  if (basis === 'Monthly') {
    for (let i = 1; remaining >= 30; i += 1) push('Month', 30, card.monthly, `Month ${i}`);
  }
  if (basis !== 'Nightly') {
    for (let i = 1; remaining >= 7; i += 1) push('Week', 7, card.weekly, `Week ${i}`);
  }
  if (remaining > 0)
    push(
      'Night',
      remaining,
      card.nightly * remaining,
      `${remaining} night${remaining === 1 ? '' : 's'}`,
    );
  if (catCount > 1) {
    const extra = card.additionalCat * (catCount - 1) * nights;
    units.push({
      sequence: sequence++,
      kind: 'AdditionalCat',
      fromNight: 1,
      toNight: nights,
      description: `${catCount - 1} additional cat${catCount - 1 === 1 ? '' : 's'} × ${nights} nights`,
      amount: money(extra),
    });
  }
  const gross = units.reduce((sum, u) => sum + num(u.amount), 0);
  const off = discountLines.reduce((sum, d) => sum + num(d.amount), 0);
  return { basis, units, discountLines, total: money(round2(gross + off)) };
}

export function allowedTransitionsFor(status: BoardingStayStatus): StayAction[] {
  switch (status) {
    case 'Enquiry':
      return ['Reserve', 'Cancel'];
    case 'Reserved':
      return ['Confirm', 'Cancel'];
    case 'Confirmed':
      return ['CheckIn', 'Cancel', 'RecordNoShow', 'Extend', 'Relocate'];
    case 'CheckedIn':
      return ['CheckOut', 'Extend', 'Relocate'];
    default:
      return [];
  }
}

export function occupiedWindow(arrivalOn: string, departureOn: string, zone: string) {
  return {
    occupiedFrom: zonedLocalToInstant(arrivalOn, CHECK_IN_START, zone),
    occupiedTo: new Date(
      new Date(zonedLocalToInstant(departureOn, CHECK_OUT_END, zone)).getTime() +
        TURNAROUND_HOURS * 3_600_000,
    ).toISOString(),
  };
}

export interface BusyUnit {
  id: string;
  code: string;
  name: string;
  unitType: BoardingUnitType;
  capacityCats: number;
  block: string | null;
  sortOrder: number;
  isActive: boolean;
}

export interface BusyInterval {
  unitId: string;
  /** Local dates; the interval is [from, to) — departure day is free for an arrival. */
  from: string;
  to: string;
}

function overlaps(
  a: { from: string; to: string },
  arrivalOn: string,
  departureOn: string,
): boolean {
  return a.from < departureOn && arrivalOn < a.to;
}

export function freeUnits(
  units: BusyUnit[],
  busy: BusyInterval[],
  arrivalOn: string,
  departureOn: string,
  catCount: number,
  unitType: BoardingUnitType,
): BusyUnit[] {
  return units
    .filter((u) => u.isActive && u.unitType === unitType && u.capacityCats >= catCount)
    .filter((u) => !busy.some((b) => b.unitId === u.id && overlaps(b, arrivalOn, departureOn)))
    .sort(
      (a, b) =>
        (a.block ?? '').localeCompare(b.block ?? '') ||
        a.sortOrder - b.sortOrder ||
        a.code.localeCompare(b.code),
    );
}

export interface AvailabilityInputs {
  branchId: string;
  units: BusyUnit[];
  busy: BusyInterval[];
  cardFor: (unitType: BoardingUnitType) => RateCardValues | null;
  arrivalOn: string;
  departureOn: string;
  catCount: number;
  mode: 'Online' | 'Staff';
  requiresIsolation: boolean;
}

export function findAvailability(input: AvailabilityInputs): {
  unitTypes: AvailableUnitType[];
  total: number;
} {
  const nights = nightsBetween(input.arrivalOn, input.departureOn);
  const discount = BRANCH_INFO[input.branchId]?.discountPercent ?? 0;
  // Isolation units are offered only when the stay requires isolation, and then only those (ADR-227).
  const types: BoardingUnitType[] =
    input.requiresIsolation && input.mode === 'Staff'
      ? ['Isolation']
      : UNIT_TYPE_ORDER.filter((t) => t !== 'Isolation');
  const out: AvailableUnitType[] = [];
  let total = 0;
  for (const unitType of types) {
    const card = input.cardFor(unitType);
    if (!card) continue;
    const free = freeUnits(
      input.units,
      input.busy,
      input.arrivalOn,
      input.departureOn,
      input.catCount,
      unitType,
    );
    total += free.length;
    out.push({
      unitType,
      capacityCats: TYPE_CAPACITY[unitType],
      freeUnitCount: free.length,
      quote: quoteStay(unitType, nights, input.catCount, card, discount),
      freeUnits:
        input.mode === 'Staff'
          ? free.map((u) => ({ unitId: u.id, code: u.code, name: u.name, block: u.block }))
          : undefined,
    });
  }
  return { unitTypes: out, total };
}

/** Up to three nearest alternatives of the same length (§8.8). */
export function findAlternatives(
  input: AvailabilityInputs,
  searchDays = 14,
): AvailabilityAlternative[] {
  const nights = nightsBetween(input.arrivalOn, input.departureOn);
  const found: AvailabilityAlternative[] = [];
  for (let shift = 1; shift <= searchDays && found.length < 3; shift += 1) {
    for (const sign of [1, -1]) {
      if (found.length >= 3) break;
      const arrivalOn = addDaysIso(input.arrivalOn, sign * shift);
      const departureOn = addDaysIso(arrivalOn, nights);
      const result = findAvailability({ ...input, arrivalOn, departureOn });
      const open = result.unitTypes.filter((t) => t.freeUnitCount > 0);
      if (open.length === 0) continue;
      const cheapest = [...open].sort((a, b) => num(a.quote.total) - num(b.quote.total))[0]!;
      found.push({
        arrivalOn,
        departureOn,
        unitType: cheapest.unitType,
        total: cheapest.quote.total,
      });
    }
  }
  return found.sort((a, b) => a.arrivalOn.localeCompare(b.arrivalOn));
}

/** The charge lines a reserved quote becomes (§8.15 `StayChargeDeriver`): one per rate unit and one
 *  per discount line — never merged, so the portal can itemise them (US-BRD-03 AC2). */
export function chargesFromQuote(quote: StayQuote, idPrefix: string): StayChargeLine[] {
  const lines: StayChargeLine[] = quote.units.map((unit) => ({
    id: `${idPrefix}-u${unit.sequence}`,
    kind: unit.kind === 'AdditionalCat' ? 'AdditionalCat' : 'RateUnit',
    description: unit.description,
    amount: unit.amount,
    fromNight: unit.fromNight,
    toNight: unit.toNight,
  }));
  quote.discountLines.forEach((discount, index) => {
    lines.push({
      id: `${idPrefix}-d${index + 1}`,
      kind: 'LongStayDiscount',
      description: discount.description,
      amount: discount.amount,
    });
  });
  return lines;
}
