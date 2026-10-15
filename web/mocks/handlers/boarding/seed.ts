import type { BoardingUnitType, CareProfile, StayQuote } from '@/lib/api/types';
import { addDaysIso, zonedLocalToInstant } from '@/lib/format/appointment';
import { nightsBetween } from '@/lib/format/boarding';
import { todayInZone } from '@/lib/format/time';
import {
  BRANCH_INFO,
  boardingFixtureIds as ids,
  RESERVED_SOON_SECONDS,
  serverNow,
} from './fixtures';
import type {
  BoardingDb,
  StoredCat,
  StoredIncident,
  StoredMedia,
  StoredObservation,
  StoredRateCard,
  StoredStay,
  StoredTask,
  StoredUnit,
} from './mock-types';
import { chargesFromQuote, money, num, quoteStay, type RateCardValues } from './rules';

/**
 * `mocks/handlers/boarding/seed.ts` — HO-BRD-002 §12 fixtures: two branches in different zones;
 * units of all four types with a maintenance block; rate cards with and without a long-stay
 * discount; a reserved stay whose deposit is due in 70 s; each `depositState`; a lapsed reservation;
 * an in-house family with tasks due, overdue and done, two `None` observations, diary entries and a
 * media reference; a medium and a high (escalated) incident; an isolation-flagged cat; a referral
 * whose approval exceeds the headroom; a 60-night stay with one cycle invoice; an early check-out
 * with a credit; a daycare day. Dates are relative to "today" in the main branch's zone.
 */

const MAIN = ids.mainBranchId;
const ZONE = BRANCH_INFO[MAIN]!.zone;

function unit(
  id: string,
  code: string,
  name: string,
  unitType: BoardingUnitType,
  capacityCats: number,
  block: string,
  sortOrder: number,
  branchId: string = MAIN,
): StoredUnit {
  return { id, branchId, code, name, unitType, capacityCats, block, sortOrder, isActive: true };
}

function card(
  unitType: BoardingUnitType,
  nightly: number,
  weekly: number,
  monthly: number,
  additionalCat: number,
  effectiveFrom: string,
  id: string,
): StoredRateCard {
  return {
    id,
    unitType,
    nightly,
    weekly,
    monthly,
    additionalCat,
    taxClassId: ids.taxClassId,
    effectiveFrom,
    setBy: 'Aisha Rahman',
  };
}

export function cardValues(c: StoredRateCard): RateCardValues {
  return {
    nightly: c.nightly,
    weekly: c.weekly,
    monthly: c.monthly,
    additionalCat: c.additionalCat,
  };
}

function profile(opts: {
  feedings: Array<[string, string, 'OwnFood' | 'HouseFood', string]>;
  medications?: CareProfile['medications'];
  limit?: number;
  authorised?: boolean;
}): CareProfile {
  return {
    feedings: opts.feedings.map(([localTime, portion, foodSource, foodDescription]) => ({
      localTime,
      portion,
      foodSource,
      foodDescription,
    })),
    medications: opts.medications ?? [],
    litterType: 'Clumping',
    litterNote: 'Unscented',
    vetVisitAuthorised: opts.authorised ?? true,
    vetSpendingLimit: money(opts.limit ?? 2000),
    emergencyContact: { name: 'Farhana Yasmin', phone: '01711000000', relationship: 'Owner' },
    notes: null,
  };
}

function cat(
  catId: string,
  catName: string,
  careProfile: CareProfile | null,
  extra: Partial<StoredCat> = {},
): StoredCat {
  return {
    catId,
    catName,
    isHighRisk: false,
    handlingNote: null,
    careProfile,
    gateStatus: 'Passed',
    gateReasons: [],
    gateOverriddenBy: null,
    gateOverrideReason: null,
    suspectedInfectious: false,
    awayFromUnit: false,
    awayReason: null,
    checkIn: null,
    checkOut: null,
    ...extra,
  };
}

function checkedIn(
  weightKg: string,
  whenIso: string,
  belongings: string[] = [],
): StoredCat['checkIn'] {
  return {
    weightKg,
    observation: 'Bright, alert, eating well.',
    belongings: belongings.map((description, i) => ({
      id: `bel-${whenIso.slice(0, 10)}-${i + 1}`,
      kind: i === 0 ? ('Carrier' as const) : ('Toy' as const),
      description,
      reference: null,
    })),
    signature: { signerName: 'Farhana Yasmin', method: 'Typed', signedOn: whenIso },
  };
}

interface StayInit {
  id: string;
  branchId?: string;
  customerId?: string;
  customerName?: string;
  status: StoredStay['status'];
  arrivalOn: string;
  departureOn: string;
  type: BoardingUnitType;
  unitId: string | null;
  cats: StoredCat[];
  depositState: StoredStay['depositState'];
  payBy?: string | null;
  channel?: StoredStay['channel'];
  card: StoredRateCard;
  discountPercent?: number;
}

function stay(init: StayInit, now: number): StoredStay {
  const branchId = init.branchId ?? MAIN;
  const nights = nightsBetween(init.arrivalOn, init.departureOn);
  const quote: StayQuote = quoteStay(
    init.type,
    nights,
    init.cats.length,
    cardValues(init.card),
    init.discountPercent ?? BRANCH_INFO[branchId]?.discountPercent ?? 0,
  );
  const zone = BRANCH_INFO[branchId]?.zone ?? ZONE;
  const from = zonedLocalToInstant(init.arrivalOn, '14:00', zone);
  return {
    id: init.id,
    branchId,
    customerId: init.customerId ?? ids.signedInCustomerId,
    customerName: init.customerName ?? ids.signedInCustomerName,
    customerPhone: '01711000000',
    channel: init.channel ?? 'Online',
    status: init.status,
    arrivalOn: init.arrivalOn,
    departureOn: init.departureOn,
    requestedUnitType: init.type,
    unitId: init.unitId,
    cats: init.cats,
    quote,
    depositRequired: Math.round(num(quote.total) * 0.3 * 100) / 100,
    depositState: init.depositState,
    payBy: init.payBy ?? null,
    version: 1,
    statusHistory: [
      {
        from: 'Enquiry',
        to: init.status,
        changedOn: new Date(now - 86_400_000).toISOString(),
        changedBy:
          init.channel === 'Reception' ? 'Maryam Sultana' : (init.customerName ?? 'Sam Rahman'),
      },
    ],
    allocations: init.unitId
      ? [
          {
            id: `alloc-${init.id}`,
            unitName: '',
            unitType: init.type,
            from,
            to: zonedLocalToInstant(init.departureOn, '14:00', zone),
            isActive: init.status !== 'Cancelled',
            reason: null,
          },
        ]
      : [],
    isolationEntries: [],
    referrals: [],
    approvals: [],
    charges: init.status === 'Enquiry' ? [] : chargesFromQuote(quote, init.id.slice(-4)),
    invoices: [],
    attention: { needsAttention: false, reason: null, note: null },
    depositForfeited: null,
    checkedInOn: null,
    checkedOutOn: null,
    createdOn: new Date(now - 2 * 86_400_000).toISOString(),
  };
}

export function seedBoardingDb(): BoardingDb {
  const now = serverNow();
  const T = todayInZone(ZONE);
  const at = (date: string, time: string) => zonedLocalToInstant(date, time, ZONE);
  const ago = (minutes: number) => new Date(now - minutes * 60_000).toISOString();
  const ahead = (minutes: number) => new Date(now + minutes * 60_000).toISOString();

  // ---- units ----
  const units: StoredUnit[] = [
    unit(ids.unitS1, 'S-1', 'Standard 1', 'Standard', 1, 'Main block', 1),
    unit(ids.unitS2, 'S-2', 'Standard 2', 'Standard', 1, 'Main block', 2),
    unit(ids.unitS3, 'S-3', 'Standard 3', 'Standard', 1, 'Main block', 3),
    unit(ids.unitD1, 'D-1', 'Deluxe 1', 'Deluxe', 2, 'Main block', 4),
    unit(ids.unitD2, 'D-2', 'Deluxe 2', 'Deluxe', 2, 'Main block', 5),
    unit(ids.unitF1, 'F-1', 'Family 1', 'Family', 3, 'Garden wing', 6),
    unit(ids.unitIso1, 'ISO-1', 'Isolation 1', 'Isolation', 1, 'Isolation block', 7),
    unit(ids.unitIso2, 'ISO-2', 'Isolation 2', 'Isolation', 1, 'Isolation block', 8),
    unit(
      ids.unitLondonS1,
      'S-1',
      'Standard 1',
      'Standard',
      1,
      'Ground floor',
      1,
      ids.londonBranchId,
    ),
  ];

  const longAgo = new Date(now - 90 * 86_400_000).toISOString();
  const rateCards: StoredRateCard[] = [
    card('Standard', 1000, 6000, 22000, 500, longAgo, 'rc-standard-1'),
    card('Deluxe', 1400, 8400, 30000, 650, longAgo, 'rc-deluxe-1'),
    card(
      'Deluxe',
      1500,
      9000,
      33000,
      700,
      new Date(now - 30 * 86_400_000).toISOString(),
      'rc-deluxe-2',
    ),
    card('Deluxe', 1650, 9900, 36300, 770, at(addDaysIso(T, 30), '00:00'), 'rc-deluxe-3'),
    card('Family', 2000, 12000, 44000, 600, longAgo, 'rc-family-1'),
    card('Isolation', 1800, 10800, 39000, 0, longAgo, 'rc-iso-1'),
  ];
  const currentCard = (type: BoardingUnitType) =>
    [...rateCards]
      .filter((c) => c.unitType === type && c.effectiveFrom <= new Date(now).toISOString())
      .sort((a, b) => b.effectiveFrom.localeCompare(a.effectiveFrom))[0]!;

  const stays: Record<string, StoredStay> = {};
  const add = (s: StoredStay) => {
    stays[s.id] = s;
    return s;
  };

  // ---- S1: the in-house family (Noodle + Pepper, F-1) ----
  const s1 = add(
    stay(
      {
        id: ids.stayInHouseFamily,
        status: 'CheckedIn',
        arrivalOn: addDaysIso(T, -2),
        departureOn: addDaysIso(T, 5),
        type: 'Family',
        unitId: ids.unitF1,
        depositState: 'Received',
        card: currentCard('Family'),
        cats: [
          cat(
            ids.catNoodleId,
            'Noodle',
            profile({ feedings: [['12:00', '½ pouch wet', 'OwnFood', 'No fish']] }),
            { checkIn: checkedIn('4.1', at(addDaysIso(T, -2), '15:10'), ['Blue carrier']) },
          ),
          cat(
            ids.catPepperId,
            'Pepper',
            profile({
              feedings: [['12:30', '⅓ cup dry', 'OwnFood', 'Grain-free dry']],
              medications: [
                {
                  name: 'Metacam',
                  dose: '0.5 ml',
                  route: 'Oral',
                  times: ['09:00', '21:00'],
                  instructions: 'With food',
                },
              ],
              limit: 2000,
            }),
            {
              isHighRisk: true,
              handlingNote: 'Bites when restrained — two handlers.',
              checkIn: checkedIn('5.3', at(addDaysIso(T, -2), '15:10')),
            },
          ),
        ],
      },
      now,
    ),
  );
  s1.checkedInOn = at(addDaysIso(T, -2), '15:10');
  s1.statusHistory.push({
    from: 'Confirmed',
    to: 'CheckedIn',
    changedOn: s1.checkedInOn,
    changedBy: 'Maryam Sultana',
  });
  s1.referrals.push({
    id: 'ref-pepper-1',
    catId: ids.catPepperId,
    catName: 'Pepper',
    reason: 'Limping on the left foreleg',
    urgency: 'Urgent',
    referredOn: ago(300),
    referredBy: 'Tanvir Islam',
    visitId: null,
  });
  s1.approvals.push(
    {
      id: ids.approvalDecided,
      catId: ids.catPepperId,
      catName: 'Pepper',
      amount: money(1200),
      description: 'X-ray of the left foreleg',
      status: 'Approved',
      requestedOn: ago(280),
      decidedOn: ago(270),
      decidedVia: 'Portal',
    },
    {
      id: ids.approvalPending,
      catId: ids.catPepperId,
      catName: 'Pepper',
      amount: money(3500),
      description: 'Treat the fracture under sedation',
      status: 'Pending',
      requestedOn: ago(90),
      decidedOn: null,
      decidedVia: null,
    },
  );

  // ---- S2: the isolation-flagged cat ----
  const s2 = add(
    stay(
      {
        id: ids.stayIsolation,
        customerId: ids.otherCustomerId,
        customerName: 'Imran Chowdhury',
        status: 'CheckedIn',
        arrivalOn: addDaysIso(T, -1),
        departureOn: addDaysIso(T, 3),
        type: 'Isolation',
        unitId: ids.unitIso1,
        depositState: 'Received',
        channel: 'Reception',
        card: currentCard('Isolation'),
        cats: [
          cat(
            ids.catKittyId,
            'Kitty',
            profile({ feedings: [['11:00', '¼ cup dry', 'HouseFood', 'House dry']] }),
            {
              suspectedInfectious: true,
              checkIn: checkedIn('3.8', at(addDaysIso(T, -1), '16:00')),
            },
          ),
        ],
      },
      now,
    ),
  );
  s2.checkedInOn = at(addDaysIso(T, -1), '16:00');
  s2.isolationEntries.push({
    id: 'iso-1',
    unitName: 'Isolation 1',
    staffName: 'Tanvir Islam',
    enteredOn: ago(180),
    leftOn: ago(165),
    purpose: 'Medication and litter',
  });

  // ---- S3: the 60-night stay (Mishti, D-1) with one cycle invoice ----
  const s3 = add(
    stay(
      {
        id: ids.stayLong,
        status: 'CheckedIn',
        arrivalOn: addDaysIso(T, -35),
        departureOn: addDaysIso(T, 25),
        type: 'Deluxe',
        unitId: ids.unitD1,
        depositState: 'Received',
        card: currentCard('Deluxe'),
        cats: [
          cat(
            ids.catMishtiId,
            'Mishti',
            profile({ feedings: [['08:30', '½ cup dry', 'OwnFood', 'Salmon dry']] }),
            { checkIn: checkedIn('4.6', at(addDaysIso(T, -35), '15:00'), ['Plush bed']) },
          ),
        ],
      },
      now,
    ),
  );
  s3.checkedInOn = at(addDaysIso(T, -35), '15:00');
  const cycleLines = s3.charges.filter(
    (c) => (c.fromNight ?? 0) <= 30 && c.kind !== 'AdditionalCat',
  );
  const cycleLineIds = new Set(cycleLines.map((c) => c.id));
  s3.charges = s3.charges.map((c) =>
    cycleLineIds.has(c.id) || (c.kind === 'LongStayDiscount' && c.id.endsWith('d1'))
      ? { ...c, invoicedOn: ago(60 * 24 * 5), invoiceId: 'inv-cycle-1' }
      : c,
  );
  s3.invoices.push({
    id: 'inv-cycle-1',
    number: 'INV-BRD-0041',
    kind: 'Cycle',
    total: money(29700),
    issuedOn: ago(60 * 24 * 5),
  });

  // ---- S4: reserved, deposit due in ~70 s ----
  add(
    stay(
      {
        id: ids.stayReservedSoon,
        status: 'Reserved',
        arrivalOn: addDaysIso(T, 20),
        departureOn: addDaysIso(T, 25),
        type: 'Standard',
        unitId: ids.unitS1,
        depositState: 'Awaiting',
        payBy: new Date(now + RESERVED_SOON_SECONDS * 1000).toISOString(),
        card: currentCard('Standard'),
        cats: [
          cat(
            'c1000000-0000-0000-0000-00000000000a',
            'Bean',
            profile({ feedings: [['08:00', '¼ cup dry', 'OwnFood', 'Dry']] }),
          ),
        ],
      },
      now,
    ),
  );

  // ---- S5: confirmed, arriving today (Coco) ----
  const s5 = add(
    stay(
      {
        id: ids.stayArrivingToday,
        status: 'Confirmed',
        arrivalOn: T,
        departureOn: addDaysIso(T, 3),
        type: 'Standard',
        unitId: ids.unitS2,
        depositState: 'Received',
        channel: 'Reception',
        card: currentCard('Standard'),
        cats: [
          cat(
            ids.catCocoId,
            'Coco',
            profile({ feedings: [['08:00', '⅓ cup dry', 'OwnFood', 'Chicken dry']] }),
          ),
        ],
      },
      now,
    ),
  );
  s5.statusHistory.push({
    from: 'Reserved',
    to: 'Confirmed',
    changedOn: ago(60 * 20),
    changedBy: 'Maryam Sultana',
  });

  // ---- S6: second approval pending in Billing ----
  add(
    stay(
      {
        id: ids.stayPendingApproval,
        status: 'Reserved',
        arrivalOn: addDaysIso(T, 30),
        departureOn: addDaysIso(T, 33),
        type: 'Deluxe',
        unitId: ids.unitD2,
        depositState: 'PendingApproval',
        payBy: ahead(60 * 10),
        card: currentCard('Deluxe'),
        cats: [
          cat(
            ids.catTofuId,
            'Tofu',
            profile({ feedings: [['09:00', '¼ cup dry', 'OwnFood', 'Dry']] }),
          ),
        ],
      },
      now,
    ),
  );

  // ---- S7: lapsed ----
  const s7 = add(
    stay(
      {
        id: ids.stayLapsed,
        status: 'Cancelled',
        arrivalOn: addDaysIso(T, 10),
        departureOn: addDaysIso(T, 12),
        type: 'Standard',
        unitId: null,
        depositState: 'Awaiting',
        card: currentCard('Standard'),
        cats: [cat('c1000000-0000-0000-0000-00000000000b', 'Lemon', null)],
      },
      now,
    ),
  );
  s7.statusHistory.push({
    from: 'Reserved',
    to: 'Cancelled',
    changedOn: ago(60 * 3),
    changedBy: 'System (reservation lapsed)',
  });
  s7.attention = { needsAttention: false, reason: null, note: 'ReservationLapsed' };

  // ---- S8: another customer's stay ----
  add(
    stay(
      {
        id: ids.stayOtherCustomer,
        customerId: ids.otherCustomerId,
        customerName: 'Imran Chowdhury',
        status: 'Reserved',
        arrivalOn: addDaysIso(T, 8),
        departureOn: addDaysIso(T, 10),
        type: 'Standard',
        unitId: ids.unitS3,
        depositState: 'Awaiting',
        payBy: ahead(60 * 40),
        card: currentCard('Standard'),
        cats: [cat(ids.catGhostId, 'Ghost', null)],
      },
      now,
    ),
  );

  // ---- S9: checked out early with a credit (Lulu) ----
  const s9 = add(
    stay(
      {
        id: ids.stayCheckedOut,
        status: 'CheckedOut',
        arrivalOn: addDaysIso(T, -16),
        departureOn: addDaysIso(T, -2),
        type: 'Standard',
        unitId: ids.unitS3,
        depositState: 'Received',
        card: currentCard('Standard'),
        cats: [
          cat(
            ids.catLuluId,
            'Lulu',
            profile({ feedings: [['09:00', '¼ cup dry', 'OwnFood', 'Dry']] }),
            {
              checkIn: checkedIn('4.2', at(addDaysIso(T, -16), '15:00'), [
                'Green carrier',
                'Feather toy',
              ]),
              checkOut: {
                weightKg: '4.4',
                returns: {
                  [`bel-${addDaysIso(T, -16)}-1`]: { returned: true, note: null },
                  [`bel-${addDaysIso(T, -16)}-2`]: {
                    returned: false,
                    note: 'Left at home by the owner',
                  },
                },
                signature: {
                  signerName: 'Sam Rahman',
                  method: 'Drawn',
                  signedOn: at(addDaysIso(T, -10), '10:00'),
                },
              },
            },
          ),
        ],
      },
      now,
    ),
  );
  s9.checkedInOn = at(addDaysIso(T, -16), '15:00');
  s9.checkedOutOn = at(addDaysIso(T, -10), '10:00');
  const used = quoteStay('Standard', 6, 1, cardValues(currentCard('Standard')), 0);
  s9.quote = used;
  s9.charges = [
    ...chargesFromQuote(used, 'lulu'),
    {
      id: 'lulu-credit',
      kind: 'EarlyCheckoutCredit',
      description: 'Early check-out — 8 nights not used, long-stay discount no longer applies',
      amount: money(-5200),
    },
  ];
  s9.invoices.push({
    id: 'inv-final-1',
    number: 'INV-BRD-0048',
    kind: 'Final',
    total: money(num(used.total)),
    issuedOn: ago(60 * 24 * 9),
  });

  // ---- S10: departing today (Rolo) ----
  const s10 = add(
    stay(
      {
        id: ids.stayDeparting,
        status: 'CheckedIn',
        arrivalOn: addDaysIso(T, -4),
        departureOn: T,
        type: 'Standard',
        unitId: ids.unitS2,
        depositState: 'Received',
        channel: 'Reception',
        card: currentCard('Standard'),
        cats: [
          cat(
            ids.catRoloId,
            'Rolo',
            profile({ feedings: [['08:00', '⅓ cup dry', 'OwnFood', 'Dry']] }),
            { checkIn: checkedIn('5.0', at(addDaysIso(T, -4), '15:00'), ['Red carrier']) },
          ),
        ],
      },
      now,
    ),
  );
  s10.checkedInOn = at(addDaysIso(T, -4), '15:00');

  // ---- S11: a vaccination-blocked enquiry (Tiger, the overdue cat) ----
  add(
    stay(
      {
        id: ids.stayEnquiryBlocked,
        status: 'Enquiry',
        arrivalOn: addDaysIso(T, 40),
        departureOn: addDaysIso(T, 43),
        type: 'Deluxe',
        unitId: null,
        depositState: 'NotRequired',
        card: currentCard('Deluxe'),
        cats: [
          cat(ids.catTigerId, 'Tiger', null, {
            isHighRisk: true,
            handlingNote: 'Scratches when startled.',
            gateStatus: 'Blocked',
            gateReasons: [
              { reason: 'DueBeforeDeparture', vaccineName: 'Rabies', dueOn: addDaysIso(T, -5) },
            ],
          }),
        ],
      },
      now,
    ),
  );

  // allocation unit names
  for (const s of Object.values(stays)) {
    const u = units.find((x) => x.id === s.unitId);
    s.allocations = s.allocations.map((a) => ({ ...a, unitName: u?.name ?? '' }));
  }

  // ---- tasks: due, overdue and done, across the in-house stays ----
  const tasks: StoredTask[] = [];
  let taskSeq = 0;
  const task = (
    stayId: string,
    catId: string,
    catName: string,
    unitId: string,
    kind: StoredTask['kind'],
    title: string,
    dueOn: string,
    extra: Partial<StoredTask> = {},
  ) =>
    tasks.push({
      id: `task-${++taskSeq}`,
      stayId,
      catId,
      catName,
      unitId,
      kind,
      title,
      dueOn,
      status: 'Pending',
      assignedToName: 'Tanvir Islam',
      assignedToMe: true,
      completedOn: null,
      completedByName: null,
      skipReason: null,
      ...extra,
    });
  const doneBy = { status: 'Done' as const, completedByName: 'Tanvir Islam' };
  task(
    ids.stayInHouseFamily,
    ids.catNoodleId,
    'Noodle',
    ids.unitF1,
    'Feed',
    'Feed ½ pouch wet — own food (no fish)',
    ago(200),
    { ...doneBy, completedOn: ago(195) },
  );
  task(
    ids.stayInHouseFamily,
    ids.catPepperId,
    'Pepper',
    ids.unitF1,
    'Feed',
    'Feed ⅓ cup dry — own food',
    ahead(60),
  );
  task(
    ids.stayInHouseFamily,
    ids.catPepperId,
    'Pepper',
    ids.unitF1,
    'Medication',
    'Give Metacam 0.5 ml oral',
    ago(10),
  );
  task(
    ids.stayInHouseFamily,
    ids.catNoodleId,
    'Noodle',
    ids.unitF1,
    'LitterChange',
    'Clean litter — unscented clumping',
    ago(45),
  );
  task(
    ids.stayInHouseFamily,
    ids.catNoodleId,
    'Noodle',
    ids.unitF1,
    'Enrichment',
    'Play session — 10 minutes',
    ahead(240),
  );
  tasks.push({
    id: ids.taskFailsToComplete,
    stayId: ids.stayInHouseFamily,
    catId: ids.catNoodleId,
    catName: 'Noodle',
    unitId: ids.unitF1,
    kind: 'ObservationCheck',
    title: 'Mid-morning check',
    dueOn: ago(5),
    status: 'Pending',
    assignedToName: 'Tanvir Islam',
    assignedToMe: true,
    completedOn: null,
    completedByName: null,
    skipReason: null,
  });
  task(
    ids.stayIsolation,
    ids.catKittyId,
    'Kitty',
    ids.unitIso1,
    'Feed',
    'Feed ¼ cup dry — house food',
    ahead(18),
    {
      assignedToName: 'Rafiq Uddin',
      assignedToMe: false,
    },
  );
  task(
    ids.stayLong,
    ids.catMishtiId,
    'Mishti',
    ids.unitD1,
    'Feed',
    'Feed ½ cup dry — own food',
    ahead(120),
  );
  task(
    ids.stayDeparting,
    ids.catRoloId,
    'Rolo',
    ids.unitS2,
    'Feed',
    'Feed ⅓ cup dry — own food',
    ago(35),
    {
      assignedToName: null,
      assignedToMe: false,
    },
  );

  // ---- observations: two consecutive None for Noodle, normal for the rest ----
  const observations: StoredObservation[] = [];
  const obs = (
    stayId: string,
    catId: string,
    catName: string,
    when: string,
    portionEaten: StoredObservation['portionEaten'],
    notes: string | null = null,
  ) =>
    observations.push({
      id: `obs-${observations.length + 1}`,
      stayId,
      catId,
      catName,
      recordedOn: when,
      portionEaten,
      waterIntake: 'Normal',
      litterOutput: 'Normal',
      mood: portionEaten === 'None' ? 'Withdrawn' : 'Relaxed',
      notes,
      recordedByName: 'Tanvir Islam',
    });
  obs(ids.stayInHouseFamily, ids.catNoodleId, 'Noodle', at(addDaysIso(T, -2), '19:00'), 'All');
  obs(ids.stayInHouseFamily, ids.catPepperId, 'Pepper', at(addDaysIso(T, -2), '19:05'), 'Half');
  obs(ids.stayInHouseFamily, ids.catNoodleId, 'Noodle', at(addDaysIso(T, -1), '10:00'), 'Quarter');
  obs(
    ids.stayInHouseFamily,
    ids.catPepperId,
    'Pepper',
    at(addDaysIso(T, -1), '10:05'),
    'All',
    'Ate well, purring.',
  );
  obs(ids.stayInHouseFamily, ids.catNoodleId, 'Noodle', ago(600), 'None');
  obs(
    ids.stayInHouseFamily,
    ids.catNoodleId,
    'Noodle',
    ago(240),
    'None',
    'Sniffed the bowl and walked away.',
  );

  // ---- incidents: a medium (open, unacknowledged) and a high (escalated, unacknowledged) ----
  const incidents: StoredIncident[] = [
    {
      id: ids.incidentMedium,
      stayId: ids.stayInHouseFamily,
      catId: ids.catPepperId,
      kind: 'Injury',
      severity: 'Medium',
      description: 'Pepper has a small scratch on her left ear after a disagreement with Noodle.',
      immediateAction: 'Cleaned with saline and separated them for an hour.',
      occurredOn: ago(130),
      reportedOn: ago(125),
      reportedByName: 'Tanvir Islam',
      ownerNotified: true,
      ownerAcknowledgedOn: null,
      acknowledgedVia: null,
      escalatedOn: null,
      phoneCallOn: null,
      phoneCallOutcome: null,
      resolvedOn: null,
      resolution: null,
    },
    {
      id: ids.incidentHighEscalated,
      stayId: ids.stayInHouseFamily,
      catId: ids.catNoodleId,
      kind: 'RefusalToEat',
      severity: 'High',
      description: 'Noodle has refused food for two checks in a row and is hiding.',
      immediateAction: 'Offered a different food; asked the vet to look.',
      occurredOn: ago(200),
      reportedOn: ago(195),
      reportedByName: 'Rafiq Uddin',
      ownerNotified: true,
      ownerAcknowledgedOn: null,
      acknowledgedVia: null,
      escalatedOn: ago(160),
      phoneCallOn: null,
      phoneCallOutcome: null,
      resolvedOn: null,
      resolution: null,
    },
  ];

  const media: StoredMedia[] = [
    {
      id: 'media-1',
      stayId: ids.stayInHouseFamily,
      catId: ids.catNoodleId,
      incidentId: null,
      caption: 'Noodle on the window shelf',
      capturedOnLocalDate: addDaysIso(T, -1),
      reference: 'dev-ref-noodle-0001',
      status: 'Visible',
      uploadedByName: 'Tanvir Islam',
    },
  ];

  const db: BoardingDb = {
    units,
    maintenance: [
      {
        id: 'mb-1',
        unitId: ids.unitS3,
        branchId: MAIN,
        startsOn: addDaysIso(T, 50),
        endsOn: addDaysIso(T, 53),
        reason: 'Deep clean and repaint',
        isCancelled: false,
      },
    ],
    rateCards,
    daycareRateCards: [
      {
        id: 'dc-1',
        hourly: 150,
        day: 800,
        taxClassId: ids.taxClassId,
        effectiveFrom: longAgo,
        setBy: 'Aisha Rahman',
      },
    ],
    routine: {
      isDefault: true,
      times: [
        { kind: 'LitterChange', localTime: '09:00' },
        { kind: 'LitterChange', localTime: '17:00' },
        { kind: 'Enrichment', localTime: '15:00' },
        { kind: 'ObservationCheck', localTime: '10:00' },
        { kind: 'ObservationCheck', localTime: '20:00' },
      ],
    },
    stays,
    tasks,
    observations,
    incidents,
    media,
    waitlist: [
      {
        id: ids.waitlistEntryId,
        customerId: ids.signedInCustomerId,
        branchId: MAIN,
        arrivalOn: addDaysIso(T, 60),
        departureOn: addDaysIso(T, 64),
        catCount: 2,
        unitType: null,
        status: 'Waiting',
        notifiedOn: null,
      },
    ],
    daycare: [
      {
        id: ids.daycareVisitId,
        branchId: MAIN,
        customerId: ids.signedInCustomerId,
        customerName: ids.signedInCustomerName,
        catId: ids.catMittensId,
        catName: 'Mittens',
        catIsHighRisk: false,
        handlingNote: null,
        localDate: T,
        basis: 'Day',
        expectedHours: null,
        status: 'Booked',
        chargeAmount: null,
        checkedInOn: null,
        checkedOutOn: null,
      },
    ],
    seq: 100,
  };
  return db;
}
