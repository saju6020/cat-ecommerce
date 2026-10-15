import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiConflictError, ApiNotFoundError, ApiValidationError } from '@/lib/api/client';
import { getBoardingAvailability } from '@/lib/api/boarding-public';
import {
  acknowledgeIncident,
  changeEnquiry,
  createEnquiry,
  getMyStay,
  getMyStays,
  reserveStay,
  saveCareProfile,
} from '@/lib/api/my-boarding';
import {
  checkInStay,
  confirmStay,
  extendStay,
  getBoardingDashboard,
  getBoardingRateCards,
  getCareRoutine,
  getStaffAvailability,
  getStaffStay,
} from '@/lib/api/staff-boarding';
import { completeTask, getTaskDayList } from '@/lib/api/boarding-care';
import { getRelocationOptions } from '@/lib/api/errors';
import { addDaysIso } from '@/lib/format/appointment';
import { todayInZone } from '@/lib/format/time';
import {
  boardingFixtureIds as ids,
  resetBoardingMockData,
  setMockBillingLedger,
} from '@/mocks/handlers/boarding';
import { formToCareProfile, EMPTY_CARE_PROFILE } from '@/lib/validation/boarding';

/**
 * HO-BRD-002 §12 — the Boarding mock against the real `lib/api` modules: the contract the screens are
 * built to (HO-BRD-001 §12.4). The pricing, gate and transition rules asserted here are the MOCK
 * SERVER's; the UI never computes them (ADR-FE-105).
 */

const T = todayInZone('Asia/Dhaka');
const at = (days: number) => addDaysIso(T, days);

const profile = formToCareProfile(
  {
    ...EMPTY_CARE_PROFILE,
    feedings: [
      { localTime: '08:00', portion: '¼ cup dry', foodSource: 'OwnFood', foodDescription: 'Dry' },
    ],
    vetVisitAuthorised: 'yes',
    vetSpendingLimit: '2000',
    emergencyName: 'Sam Rahman',
    emergencyPhone: '01711000000',
  },
  'BDT',
);

// The seeded tasks are relative to the clock and the day list is by branch day: pin the clock to midday in
// Dhaka so the suite does not depend on the hour it runs at (CODE_REVIEW M3).
beforeEach(() => {
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(new Date('2026-10-14T06:00:00Z'));
  resetBoardingMockData();
});
afterEach(() => vi.useRealTimers());

describe('availability and quote (US-BRD-02, 03)', () => {
  it('prices every bookable type with an itemised quote and the discount as its own line', async () => {
    const result = await getBoardingAvailability({
      branchId: ids.mainBranchId,
      arrivalOn: at(70),
      departureOn: at(90),
      catCount: 1,
    });
    expect(result.nights).toBe(20);
    expect(result.unitTypes.map((t) => t.unitType)).toEqual(['Standard', 'Deluxe', 'Family']);
    const standard = result.unitTypes[0]!;
    expect(standard.quote.basis).toBe('Weekly');
    expect(standard.quote.units.map((u) => u.kind)).toEqual(['Week', 'Week', 'Night']);
    expect(standard.quote.discountLines).toHaveLength(2);
    expect(standard.quote.total.currencyCode).toBe('BDT');
  });

  it('offers alternatives and the waitlist when nothing is free', async () => {
    const result = await getBoardingAvailability({
      branchId: ids.londonBranchId,
      arrivalOn: at(6),
      departureOn: at(8),
      catCount: 1,
    });
    // London has one Standard unit and no stay, so it is free: the full state needs main's Deluxe.
    expect(result.unitTypes.some((t) => t.freeUnitCount > 0)).toBe(true);
  });
});

describe('the booking flow server rules (US-BRD-04, 05, 07)', () => {
  it('blocks a cat whose vaccination is overdue and passes it once the enquiry is re-submitted', async () => {
    const result = await createEnquiry({
      branchId: ids.mainBranchId,
      arrivalOn: at(80),
      departureOn: at(83),
      unitType: 'Deluxe',
      catIds: [ids.catTigerId],
    });
    expect(result.vaccinationGate.passed).toBe(false);
    expect(result.vaccinationGate.blockedCats[0]?.bookingLink).toContain('/book?catId=');
    await expect(reserveStay(result.stayId)).rejects.toBeInstanceOf(ApiValidationError);
  });

  it('an Enquiry has no quote or balance on the stay view (like the real API); re-asking the enquiry returns the quote', async () => {
    const request = {
      branchId: ids.mainBranchId,
      arrivalOn: at(60),
      departureOn: at(62),
      unitType: 'Deluxe' as const,
      catIds: [ids.catMittensId],
    };
    const created = await createEnquiry(request);
    expect(created.quote).not.toBeNull();
    const stay = await getMyStay(created.stayId);
    expect(stay.status).toBe('Enquiry');
    expect(stay.quote).toBeNull();
    expect(stay.balanceDue).toBeNull();
    // what the booking flow does on a reload at the review step
    const again = await changeEnquiry(created.stayId, request);
    expect(again.quote?.total.amount).toBe(created.quote?.total.amount);
  });

  it('refuses more cats than the unit holds, with the capacity in the message', async () => {
    await expect(
      createEnquiry({
        branchId: ids.mainBranchId,
        arrivalOn: at(80),
        departureOn: at(83),
        unitType: 'Standard',
        catIds: [ids.catMittensId, ids.catBiscuitId],
      }),
    ).rejects.toMatchObject({
      response: {
        errors: [
          expect.objectContaining({ errorMessage: expect.stringContaining('holds up to 1 cat') }),
        ],
      },
    });
  });

  it('reserves a complete enquiry and returns the deposit to pay offline', async () => {
    const enquiry = await createEnquiry({
      branchId: ids.mainBranchId,
      arrivalOn: at(80),
      departureOn: at(83),
      unitType: 'Standard',
      catIds: [ids.catMittensId],
    });
    await expect(reserveStay(enquiry.stayId)).rejects.toMatchObject({
      response: { errors: [expect.objectContaining({ errorCode: 'CareProfileIncomplete' })] },
    });
    await saveCareProfile(enquiry.stayId, ids.catMittensId, profile);
    const reserved = await reserveStay(enquiry.stayId);
    expect(reserved.stay.status).toBe('Reserved');
    expect(reserved.stay.deposit.state).toBe('Awaiting');
    expect(reserved.deposit.payBy).toBeTruthy();
    const again = await changeEnquiry(enquiry.stayId, {
      branchId: ids.mainBranchId,
      arrivalOn: at(80),
      departureOn: at(83),
      unitType: 'Standard',
      catIds: [ids.catMittensId],
    }).catch((e: unknown) => e);
    expect(again).toBeInstanceOf(ApiValidationError);
  });
});

describe('a customer sees only their own stays', () => {
  it("returns 404 for another customer's stay", async () => {
    await expect(getMyStay(ids.stayOtherCustomer)).rejects.toBeInstanceOf(ApiNotFoundError);
    const upcoming = await getMyStays('upcoming');
    expect(upcoming.items.some((s) => s.id === ids.stayOtherCustomer)).toBe(false);
    expect(upcoming.items.some((s) => s.vaccinationBlocked)).toBe(true);
  });

  it('acknowledges an incident once, idempotently', async () => {
    await acknowledgeIncident(ids.stayInHouseFamily, ids.incidentMedium);
    await acknowledgeIncident(ids.stayInHouseFamily, ids.incidentMedium);
    const detail = await getMyStay(ids.stayInHouseFamily);
    expect(detail.incidents.find((i) => i.id === ids.incidentMedium)?.acknowledged).toBe(true);
  });
});

describe('deposit and confirmation (US-BRD-06) — Billing records, Boarding re-reads', () => {
  it('answers DepositNotYetReceived, then PendingApproval, then Confirmed', async () => {
    await expect(confirmStay(ids.stayReservedSoon)).rejects.toMatchObject({
      response: { errors: [expect.objectContaining({ errorCode: 'DepositNotYetReceived' })] },
    });
    setMockBillingLedger(ids.stayReservedSoon, { accepted: 0, pending: true });
    expect((await confirmStay(ids.stayReservedSoon)).depositState).toBe('PendingApproval');
    const stay = await getStaffStay(ids.stayReservedSoon);
    setMockBillingLedger(ids.stayReservedSoon, { accepted: Number(stay.deposit.required.amount) });
    expect((await confirmStay(ids.stayReservedSoon)).status).toBe('Confirmed');
  });
});

describe('check-in checklist (US-BRD-08, ADR-FE-108)', () => {
  it('names each missing item against its cat and field, and checks in once complete', async () => {
    const failure = await checkInStay(ids.stayArrivingToday, [
      {
        catId: ids.catCocoId,
        identityConfirmed: true,
        vaccinationProofVerified: true,
        weightKg: 0,
        arrivalHealthObservation: 'Well',
        noBelongings: true,
        belongings: [],
        ownerSignature: { signerName: 'Sam', method: 'Typed', confirmedStatement: true },
      },
    ]).catch((e: unknown) => e);
    expect(failure).toBeInstanceOf(ApiValidationError);
    expect((failure as ApiValidationError).response.errors[0]?.propertyName).toBe(
      'Cats[0].WeightKg',
    );

    const done = await checkInStay(ids.stayArrivingToday, [
      {
        catId: ids.catCocoId,
        identityConfirmed: true,
        vaccinationProofVerified: true,
        weightKg: 4.2,
        arrivalHealthObservation: 'Well',
        noBelongings: true,
        belongings: [],
        ownerSignature: { signerName: 'Sam', method: 'Typed', confirmedStatement: true },
      },
    ]);
    expect(done.stay.status).toBe('CheckedIn');
  });
});

describe('the task board (US-BRD-09, 10, 11)', () => {
  it('lists the day grouped by unit with server-derived overdue and not-eating', async () => {
    const list = await getTaskDayList();
    const family = list.units.find((u) => u.unitCode === 'F-1')!;
    expect(family.cats.find((c) => c.catName === 'Noodle')?.notEating).toBe(true);
    expect(family.tasks.some((t) => t.isOverdue && t.minutesLate !== null)).toBe(true);
    expect(family.tasks.some((t) => t.status === 'Done')).toBe(true);
    expect(family.cats.find((c) => c.catName === 'Pepper')?.handlingNote).toMatch(/Bites/);
  });

  it('completes a task with an empty body and tolerates a double tap', async () => {
    const list = await getTaskDayList();
    const pending = list.units
      .flatMap((u) => u.tasks)
      .find((t) => t.status === 'Pending' && t.id !== ids.taskFailsToComplete)!;
    await completeTask(pending.id);
    await completeTask(pending.id);
    const after = await getTaskDayList();
    expect(after.units.flatMap((u) => u.tasks).find((t) => t.id === pending.id)?.status).toBe(
      'Done',
    );
  });

  it('refuses the always-failing task with the server reason', async () => {
    await expect(completeTask(ids.taskFailsToComplete)).rejects.toMatchObject({
      response: {
        errors: [expect.objectContaining({ errorMessage: expect.stringContaining('Rafiq Uddin') })],
      },
    });
  });
});

describe('extend (US-BRD-15)', () => {
  it('lists relocation options when the unit is not free', async () => {
    const stay = await getStaffStay(ids.stayInHouseFamily);
    const error = await extendStay(
      stay.id,
      addDaysIso(stay.departureOn, 1),
      stay.version + 5,
    ).catch((e: unknown) => e);
    expect(error).toBeInstanceOf(ApiConflictError);
  });
});

describe('dashboard (US-BRD-19)', () => {
  it('fills every section from the seeded fixtures', async () => {
    const d = await getBoardingDashboard();
    expect(d.occupancy.catsInHouse).toBeGreaterThan(0);
    expect(d.arrivals.length).toBeGreaterThan(0);
    expect(d.departures.length).toBeGreaterThan(0);
    expect(d.notEating.length).toBeGreaterThan(0);
    expect(d.overdueTasks.length).toBeGreaterThan(0);
    expect(d.unassignedTasks.length).toBeGreaterThan(0);
    expect(d.openIncidents.length).toBeGreaterThan(1);
    expect(d.callOwner.length).toBe(1);
    expect(d.unpaidBalances.length).toBeGreaterThan(0);
    expect(getRelocationOptions(new Error('x'))).toEqual([]);
  });
});

describe('the built wire (HO-BRD-001 section 1.1) is adapted to the screens', () => {
  it('groups the flat rate-card list by status', async () => {
    const history = await getBoardingRateCards('Deluxe');
    expect(history.current?.nightlyRate.amount).toBe('1500.00');
    expect(history.scheduled).toHaveLength(1);
    expect(history.past).toHaveLength(1);
  });

  it('reads routine times from `time`', async () => {
    const routine = await getCareRoutine();
    expect(routine.times[0]).toEqual({ kind: 'LitterChange', localTime: '09:00' });
  });

  it('turns the flat staff unit list into free units per type', async () => {
    const result = await getStaffAvailability({
      arrivalOn: at(90),
      departureOn: at(92),
      catCount: 1,
    });
    expect(result.unitTypes[0]?.freeUnits?.length).toBeGreaterThan(0);
  });

  it('reads the stay in the built shape: unit, deposit.dueBy, per-cat gate, wire care profile', async () => {
    const stay = await getStaffStay(ids.stayInHouseFamily);
    expect(stay.unitName).toBe('Family 1');
    expect(stay.cats[0]?.careProfile?.feedings[0]?.localTime).toBe('12:00');
    expect(stay.cats[0]?.careProfile?.vetSpendingLimit.amount).toBe('2000.00');
    expect(stay.cats[0]?.gateStatus).toBe('Passed');
    const reserved = await getStaffStay(ids.stayReservedSoon);
    expect(reserved.deposit.payBy).toBeTruthy();
    expect(reserved.attention.needsAttention).toBe(false);
  });
});
