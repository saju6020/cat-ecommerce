import { accountsFixtureIds } from '../areas/accounts';
import { catProfilesFixtureIds } from '../areas/cat-profiles';
import { administrationFixtureIds } from '../admin/fixtures';

export {
  apiUrl,
  forbidden,
  json,
  noContent,
  notFound,
  serverNow,
  violation,
  type MockViolation,
} from '../appointments/fixtures';

/**
 * `mocks/handlers/boarding/fixtures.ts` — HO-BRD-002 §12. Ids shared by the Boarding mock modules.
 * Generated from HO-BRD-001 §12.4 (and §8 where §12.4 is silent — see `lib/api/types.ts`). Like the
 * other area mocks, no session is resolved: every customer endpoint answers for
 * `boardingFixtureIds.signedInCustomerId`; a `403` a test needs is produced with `server.use(...)`.
 *
 * The cats a customer can BOOK come from the Cat Profiles mock (Mittens current, Biscuit due soon,
 * Tiger overdue + high risk, Whiskers, Clover) so the vaccination gate reads the real mock. The cats
 * already IN the care centre (Noodle, Pepper, Kitty, …) are this mock's own roster.
 */
export const boardingFixtureIds = {
  mainBranchId: administrationFixtureIds.mainBranchId,
  londonBranchId: administrationFixtureIds.londonBranchId,
  signedInCustomerId: accountsFixtureIds.customerId,
  signedInCustomerName: 'Sam Rahman',
  otherCustomerId: 'a1000000-0000-0000-0000-000000000002',
  taxClassId: administrationFixtureIds.taxClassStandardId,

  // bookable cats (Cat Profiles mock)
  catMittensId: catProfilesFixtureIds.catCurrentPlain,
  catBiscuitId: catProfilesFixtureIds.catNoAllergiesEstimatedDob,
  catTigerId: catProfilesFixtureIds.catHighRiskOverdue,
  catWhiskersId: catProfilesFixtureIds.catWithAllergiesExactDob,

  // cats already in the care centre (this mock's roster)
  catNoodleId: 'c1000000-0000-0000-0000-000000000001',
  catPepperId: 'c1000000-0000-0000-0000-000000000002',
  catKittyId: 'c1000000-0000-0000-0000-000000000003',
  catMishtiId: 'c1000000-0000-0000-0000-000000000004',
  catCocoId: 'c1000000-0000-0000-0000-000000000005',
  catTofuId: 'c1000000-0000-0000-0000-000000000006',
  catLuluId: 'c1000000-0000-0000-0000-000000000007',
  catGhostId: 'c1000000-0000-0000-0000-000000000008',
  catRoloId: 'c1000000-0000-0000-0000-000000000009',

  // units, main branch
  unitS1: 'd1000000-0000-0000-0000-000000000001',
  unitS2: 'd1000000-0000-0000-0000-000000000002',
  unitS3: 'd1000000-0000-0000-0000-000000000003',
  unitD1: 'd1000000-0000-0000-0000-000000000004',
  unitD2: 'd1000000-0000-0000-0000-000000000005',
  unitF1: 'd1000000-0000-0000-0000-000000000006',
  unitIso1: 'd1000000-0000-0000-0000-000000000007',
  unitIso2: 'd1000000-0000-0000-0000-000000000008',
  // units, London
  unitLondonS1: 'd2000000-0000-0000-0000-000000000001',

  // stays
  stayInHouseFamily: 'e1000000-0000-0000-0000-000000000001', // Noodle + Pepper, F-1, checked in
  stayIsolation: 'e1000000-0000-0000-0000-000000000002', // Kitty, ISO-1, flagged infectious
  stayLong: 'e1000000-0000-0000-0000-000000000003', // Mishti, 60 nights, one cycle invoice
  stayReservedSoon: 'e1000000-0000-0000-0000-000000000004', // deposit due in ~70 s
  stayArrivingToday: 'e1000000-0000-0000-0000-000000000005', // Coco, Confirmed, arrives today
  stayPendingApproval: 'e1000000-0000-0000-0000-000000000006', // Tofu, second approval in Billing
  stayLapsed: 'e1000000-0000-0000-0000-000000000007', // cancelled — ReservationLapsed
  stayOtherCustomer: 'e1000000-0000-0000-0000-000000000008', // 404 for the signed-in customer
  stayCheckedOut: 'e1000000-0000-0000-0000-000000000009', // Lulu, early check-out done
  stayDeparting: 'e1000000-0000-0000-0000-00000000000a', // Rolo, departs today
  stayEnquiryBlocked: 'e1000000-0000-0000-0000-00000000000b', // vaccination-blocked draft

  incidentMedium: 'f1000000-0000-0000-0000-000000000001',
  incidentHighEscalated: 'f1000000-0000-0000-0000-000000000002',
  approvalPending: 'f2000000-0000-0000-0000-000000000001',
  approvalDecided: 'f2000000-0000-0000-0000-000000000002',

  /** A task whose completion always fails — "already completed by Rafiq Uddin at 10:32" (U-D2). */
  taskFailsToComplete: 'f3000000-0000-0000-0000-0000000000ff',
  waitlistEntryId: 'f4000000-0000-0000-0000-000000000001',
  daycareVisitId: 'f5000000-0000-0000-0000-000000000001',
} as const;

export const CURRENCY = 'BDT';

/** Seconds until the seeded "reserved" stay's deposit is due (§12: "due in 70 s"). */
export const RESERVED_SOON_SECONDS = 70;

/** sessionStorage key a test writes to simulate Billing recording a deposit (ADR-FE-106: Boarding
 *  never records payments — Billing does; the mock's confirm reads this "ledger"). Shape:
 *  `{ [stayId]: { accepted: number; pending?: boolean } }`. */
export const BILLING_LEDGER_STORAGE_KEY = '__purrcare_brd_mock_ledger__';

export const BRANCH_INFO: Record<
  string,
  { name: string; zone: string; discountPercent: number; daycareCapacity: number }
> = {
  [administrationFixtureIds.mainBranchId]: {
    name: 'Main clinic',
    zone: 'Asia/Dhaka',
    discountPercent: 10,
    daycareCapacity: 3,
  },
  [administrationFixtureIds.londonBranchId]: {
    name: 'London',
    zone: 'Europe/London',
    discountPercent: 0,
    daycareCapacity: 0,
  },
};
