import { loadPersisted, savePersisted, clearPersisted } from '../admin/fixtures';
import { dateOffset, instantOffset } from '../inventory/fixtures';
import { pharmacyFixtureIds as ids } from './fixtures';

/**
 * `mocks/handlers/pharmacy/store.ts` — HO-PHM-002 §12. The in-memory "database" behind the pharmacy
 * mocks (sessionStorage-persisted in a real browser, always fresh in Vitest — the inventory mock's
 * pattern). Quantities are plain numbers INSIDE the mock; every response goes through
 * `quantity()` so the wire carries strings, exactly as the real API does.
 */
const PERSIST_KEY = '__purrcare_pharmacy_mock_db__';

export const FREQUENCIES: Record<string, string> = {
  Once: 'once',
  OnceDaily: 'once daily',
  TwiceDaily: 'twice daily',
  ThreeTimesDaily: 'three times daily',
  FourTimesDaily: 'four times daily',
  EveryOtherDay: 'every other day',
  Weekly: 'once weekly',
  AsNeeded: 'as needed',
};

export interface MInteraction {
  id: string;
  medicineId: string | null;
  term: string | null;
  severity: 'Minor' | 'Moderate' | 'Major' | 'Contraindicated';
  note: string;
}

export interface MMedicine {
  id: string;
  code: string;
  name: string;
  ingredients: string[];
  classes: string[];
  form: string;
  strengthAmount: number;
  strengthUnit: string;
  perQuantity: number;
  perUnit: string;
  defaultRoute: string;
  allowedRoutes: string[];
  dosingBasis: 'PerKg' | 'Fixed';
  range: { min: number; max: number } | null;
  contraindications: string;
  interactions: MInteraction[];
  dispensingUnit: string;
  isControlled: boolean;
  schedule: string | null;
  isActive: boolean;
  linkedSkus: Array<{ skuId: string; productName: string; variantLabel: string }>;
  version: number;
}

export interface MBatch {
  batchId: string;
  medicineId: string;
  skuId: string;
  productName: string;
  variantLabel: string;
  lotNumber: string;
  barcode: string | null;
  expiresOn: string;
  available: number;
  /** Set for a quarantined / recalled batch. */
  notDispensableReason: string | null;
}

export interface MRepeatRequest {
  id: string;
  requestedOn: string;
  preferredBranchId: string;
  note: string | null;
  status: 'Requested' | 'Dispensed' | 'Declined' | 'Withdrawn';
  decidedOn: string | null;
  declineReason: string | null;
  dispensingId: string | null;
}

export interface MDispensingLine {
  lineId: string;
  skuId: string;
  batchId: string;
  lotNumber: string;
  expiresOn: string;
  quantity: number;
}

export interface MDispensing {
  id: string;
  prescriptionId: string;
  supplyNumber: number;
  isRepeat: boolean;
  status: 'Dispensed' | 'Reversed';
  dispensedOn: string;
  dispensedByName: string;
  witnessName: string | null;
  fefoOverrideReason: string | null;
  lines: MDispensingLine[];
  labelPrintCount: number;
  reversedOn: string | null;
  reversalReason: string | null;
  branchId: string;
}

export interface MWarning {
  kind:
    'DoseBelowRange' | 'DoseAboveRange' | 'DoseNotCheckable' | 'AllergyConflict' | 'Interaction';
  blocking: boolean;
  requiresAcknowledgement: boolean;
  severity: string | null;
  subject: string;
  detail: string;
}

export interface MPrescription {
  id: string;
  number: string;
  catId: string;
  medicineId: string;
  visitId: string;
  doseAmount: number;
  doseUnit: string;
  route: string;
  /** The enumeration NAME ("TwiceDaily"). */
  frequency: string;
  durationDays: number;
  quantity: number;
  instructions: string;
  startsOn: string;
  lastDispensedOn: string | null;
  isRepeatable: boolean;
  repeatsAuthorised: number;
  validUntil: string;
  status: 'Issued' | 'Dispensed' | 'Completed' | 'Expired' | 'Cancelled';
  prescriberName: string;
  issuedOn: string;
  isRemoteRenewal: boolean;
  hasAcknowledgedWarnings: boolean;
  weightKg: number | null;
  weightRecordedOn: string | null;
  doseMgPerKg: string | null;
  doseCheckOutcome: string;
  acknowledgedByName: string | null;
  acknowledgedOn: string | null;
  allergyReason: string | null;
  doseNote: string | null;
  warnings: MWarning[];
  repeatRequests: MRepeatRequest[];
  renewedFromId: string | null;
  renewedById: string | null;
  suppliesDispensed: number;
  branchId: string;
}

export type MEntryKind =
  | 'OpeningBalance'
  | 'Receipt'
  | 'Dispensed'
  | 'Administered'
  | 'Wastage'
  | 'Returned'
  | 'Adjustment'
  | 'Correction';

export interface MRegisterEntry {
  id: string;
  entryNumber: number;
  recordedOn: string;
  occurredOn: string;
  kind: MEntryKind;
  medicineId: string;
  batchId: string | null;
  lotNumber: string | null;
  batchExpiresOn: string | null;
  delta: number;
  doseText: string | null;
  wastage: number | null;
  catName: string | null;
  customerName: string | null;
  actorName: string;
  witnessName: string | null;
  sourceType: string;
  sourceId: string;
  sourceLabel: string;
  correctionReason: string | null;
  notes: string | null;
  correctsEntryNumber: number | null;
  isSealed: boolean;
  branchId: string;
}

export interface MDiet {
  id: string;
  catId: string;
  productId: string;
  productName: string;
  validFrom: string;
  validTo: string;
  revoked: boolean;
  authorisedBy: string;
  visitId: string;
  notes: string | null;
}

export interface MockDb {
  seq: number;
  medicines: MMedicine[];
  batches: MBatch[];
  prescriptions: MPrescription[];
  dispensings: MDispensing[];
  entries: MRegisterEntry[];
  diets: MDiet[];
  /** Test hook: report a broken chain at this position. */
  brokenAtPosition: number | null;
}

export const CLINIC = {
  name: 'PurrCare Veterinary Clinic — Uttara',
  phone: '+880 1700 000000',
  address: 'House 12, Road 7, Sector 4, Uttara, Dhaka',
};

export const DIET_PRODUCTS = [
  { productId: ids.dietProductRenalId, name: 'Renal Care dry food 2 kg', brand: 'Hill Cat' },
  {
    productId: ids.dietProductUrinaryId,
    name: 'Urinary Support dry food 1.5 kg',
    brand: 'Royal Cat',
  },
];

function medicine(
  partial: Partial<MMedicine> & Pick<MMedicine, 'id' | 'code' | 'name'>,
): MMedicine {
  return {
    ingredients: [partial.name.split(' ')[0]!.toLowerCase()],
    classes: [],
    form: 'Tablet',
    strengthAmount: 1,
    strengthUnit: 'Mg',
    perQuantity: 1,
    perUnit: 'Tablet',
    defaultRoute: 'Oral',
    allowedRoutes: ['Oral'],
    dosingBasis: 'PerKg',
    range: { min: 0.05, max: 0.1 },
    contraindications: '',
    interactions: [],
    dispensingUnit: 'Tablet',
    isControlled: false,
    schedule: null,
    isActive: true,
    linkedSkus: [],
    version: 1,
    ...partial,
  };
}

function sku(label: string, variant: string, key: string) {
  return {
    skuId: `e7000000-0000-4000-8000-${key.padStart(12, '0')}`,
    productName: label,
    variantLabel: variant,
  };
}

function seedMedicines(): MMedicine[] {
  return [
    medicine({
      id: ids.medMeloxicamId,
      code: 'MLX-OS-15',
      name: 'Meloxicam oral suspension',
      ingredients: ['meloxicam'],
      classes: ['nsaid'],
      form: 'OralLiquid',
      strengthAmount: 1.5,
      strengthUnit: 'Mg',
      perQuantity: 1,
      perUnit: 'Millilitre',
      dispensingUnit: 'Millilitre',
      range: { min: 0.05, max: 0.1 },
      contraindications:
        'Not for cats with dehydration, kidney or liver disease or active gut ulcers.',
      interactions: [
        {
          id: 'e8000000-0000-4000-8000-000000000001',
          medicineId: ids.medPrednisoloneId,
          term: null,
          severity: 'Major',
          note: 'NSAIDs with corticosteroids raise the risk of stomach and gut ulceration.',
        },
        {
          id: 'e8000000-0000-4000-8000-000000000002',
          medicineId: ids.medGabapentinId,
          term: null,
          severity: 'Minor',
          note: 'May increase drowsiness; monitor.',
        },
      ],
      linkedSkus: [sku('Meloxicam 1.5 mg/ml oral suspension', '15 ml bottle', '1')],
    }),
    medicine({
      id: ids.medFusidicId,
      code: 'FUS-GEL-1',
      name: 'Fusidic acid gel',
      ingredients: ['fusidic acid'],
      classes: ['antibiotic'],
      form: 'Topical',
      strengthAmount: 1,
      strengthUnit: 'Percent',
      perQuantity: 1,
      perUnit: 'Gram',
      defaultRoute: 'Topical',
      allowedRoutes: ['Topical', 'Ophthalmic'],
      dosingBasis: 'Fixed',
      range: null,
      dispensingUnit: 'Tube',
      linkedSkus: [sku('Fusidic acid 1% gel', '5 g tube', '2')],
    }),
    medicine({
      id: ids.medAmoxicillinId,
      code: 'AMX-TAB-50',
      name: 'Amoxicillin tablets',
      ingredients: ['amoxicillin'],
      classes: ['penicillin', 'antibiotic'],
      form: 'Tablet',
      strengthAmount: 50,
      strengthUnit: 'Mg',
      perQuantity: 1,
      perUnit: 'Tablet',
      dispensingUnit: 'Tablet',
      range: { min: 10, max: 20 },
      linkedSkus: [sku('Amoxicillin 50 mg tablets', 'Strip of 10', '3')],
    }),
    medicine({
      id: ids.medPrednisoloneId,
      code: 'PRD-TAB-5',
      name: 'Prednisolone tablets',
      ingredients: ['prednisolone'],
      classes: ['corticosteroid'],
      form: 'Tablet',
      strengthAmount: 5,
      strengthUnit: 'Mg',
      perQuantity: 1,
      perUnit: 'Tablet',
      dispensingUnit: 'Tablet',
      range: { min: 0.5, max: 2 },
      interactions: [
        {
          id: 'e8000000-0000-4000-8000-000000000003',
          medicineId: ids.medMeloxicamId,
          term: null,
          severity: 'Major',
          note: 'Corticosteroids with NSAIDs raise the risk of gut ulceration.',
        },
      ],
      linkedSkus: [sku('Prednisolone 5 mg tablets', 'Strip of 10', '4')],
    }),
    medicine({
      id: ids.medBuprenorphineId,
      code: 'BUP-INJ-03',
      name: 'Buprenorphine injection',
      ingredients: ['buprenorphine'],
      classes: ['opioid'],
      form: 'Injection',
      strengthAmount: 0.3,
      strengthUnit: 'Mg',
      perQuantity: 1,
      perUnit: 'Millilitre',
      defaultRoute: 'Subcutaneous',
      allowedRoutes: ['Subcutaneous', 'Intramuscular', 'Intravenous'],
      dispensingUnit: 'Millilitre',
      range: { min: 0.01, max: 0.03 },
      isControlled: true,
      schedule: 'Schedule 3',
      linkedSkus: [sku('Buprenorphine 0.3 mg/ml injection', '10 ml vial', '5')],
    }),
    medicine({
      id: ids.medGabapentinId,
      code: 'GBP-CAP-100',
      name: 'Gabapentin capsules',
      ingredients: ['gabapentin'],
      form: 'Capsule',
      strengthAmount: 100,
      strengthUnit: 'Mg',
      perQuantity: 1,
      perUnit: 'Capsule',
      dispensingUnit: 'Capsule',
      range: { min: 5, max: 10 },
      linkedSkus: [],
    }),
    medicine({
      id: ids.medMaropitantId,
      code: 'MRP-TAB-16',
      name: 'Maropitant tablets',
      ingredients: ['maropitant'],
      form: 'Tablet',
      strengthAmount: 16,
      strengthUnit: 'Mg',
      perQuantity: 1,
      perUnit: 'Tablet',
      dispensingUnit: 'Tablet',
      range: { min: 1, max: 2 },
      linkedSkus: [sku('Maropitant 16 mg tablets', 'Strip of 4', '7')],
    }),
  ];
}

function batch(
  key: string,
  medicineId: string,
  lot: string,
  expiresInDays: number,
  available: number,
  skuKey: string,
  productName: string,
  variantLabel: string,
  extra: Partial<MBatch> = {},
): MBatch {
  return {
    batchId: `e9000000-0000-4000-8000-${key.padStart(12, '0')}`,
    medicineId,
    skuId: sku(productName, variantLabel, skuKey).skuId,
    productName,
    variantLabel,
    lotNumber: lot,
    barcode: `8900${key.padStart(8, '0')}`,
    expiresOn: dateOffset(expiresInDays),
    available,
    notDispensableReason: null,
    ...extra,
  };
}

function seedBatches(): MBatch[] {
  const mlx = ['Meloxicam 1.5 mg/ml oral suspension', '15 ml bottle'] as const;
  return [
    // Two usable meloxicam lots (the earlier-expiring one is the FEFO suggestion), plus an expired and a quarantined one.
    batch('1', ids.medMeloxicamId, 'L4410', 60, 6, '1', ...mlx),
    batch('2', ids.medMeloxicamId, 'L4551', 180, 40, '1', ...mlx),
    batch('3', ids.medMeloxicamId, 'L3302', -20, 15, '1', ...mlx),
    batch('4', ids.medMeloxicamId, 'L4600', 200, 30, '1', ...mlx, {
      notDispensableReason: 'Quarantined by Inventory pending a storage check.',
    }),
    batch(
      '5',
      ids.medAmoxicillinId,
      'AMX-221',
      240,
      60,
      '3',
      'Amoxicillin 50 mg tablets',
      'Strip of 10',
    ),
    batch(
      '6',
      ids.medPrednisoloneId,
      'PRD-118',
      300,
      50,
      '4',
      'Prednisolone 5 mg tablets',
      'Strip of 10',
    ),
    batch(
      '7',
      ids.medBuprenorphineId,
      'BUP-77',
      400,
      27.6,
      '5',
      'Buprenorphine 0.3 mg/ml injection',
      '10 ml vial',
    ),
    batch(
      '10',
      ids.medBuprenorphineId,
      'BUP-81',
      300,
      7.5,
      '5',
      'Buprenorphine 0.3 mg/ml injection',
      '10 ml vial',
    ),
    batch('8', ids.medFusidicId, 'FUS-09', 365, 25, '2', 'Fusidic acid 1% gel', '5 g tube'),
    // Only expired stock exists for maropitant.
    batch(
      '9',
      ids.medMaropitantId,
      'MRP-04',
      -35,
      12,
      '7',
      'Maropitant 16 mg tablets',
      'Strip of 4',
    ),
  ];
}

function rx(
  partial: Partial<MPrescription> & Pick<MPrescription, 'id' | 'number' | 'catId' | 'medicineId'>,
): MPrescription {
  return {
    visitId: 'b1000000-0000-0000-0000-000000000001',
    doseAmount: 1,
    doseUnit: 'Tablet',
    route: 'Oral',
    frequency: 'OnceDaily',
    durationDays: 7,
    quantity: 7,
    instructions: 'Give by mouth once daily, with or after food.',
    startsOn: dateOffset(-3),
    lastDispensedOn: null,
    isRepeatable: false,
    repeatsAuthorised: 0,
    validUntil: dateOffset(60),
    status: 'Issued',
    prescriberName: 'Dr Akter',
    issuedOn: instantOffset(-3, 9),
    isRemoteRenewal: false,
    hasAcknowledgedWarnings: false,
    weightKg: 4.2,
    weightRecordedOn: dateOffset(-30),
    doseMgPerKg: null,
    doseCheckOutcome: 'NotCheckable',
    acknowledgedByName: null,
    acknowledgedOn: null,
    allergyReason: null,
    doseNote: null,
    warnings: [],
    repeatRequests: [],
    renewedFromId: null,
    renewedById: null,
    suppliesDispensed: 0,
    branchId: ids.mainBranchId,
    ...partial,
  };
}

function seedPrescriptions(): MPrescription[] {
  return [
    // Mittens: dispensed 2 days ago, 2 repeats left — "5 days left", can request a repeat.
    rx({
      id: ids.rxMittensActiveId,
      number: 'RX-2026-000101',
      catId: ids.catMittensId,
      medicineId: ids.medMeloxicamId,
      doseAmount: 0.3,
      doseUnit: 'Ml',
      durationDays: 7,
      quantity: 10,
      instructions: 'Give 0.3 ml by mouth once daily, with or after food. Shake well before use.',
      status: 'Dispensed',
      startsOn: dateOffset(-2),
      lastDispensedOn: dateOffset(-2),
      isRepeatable: true,
      repeatsAuthorised: 2,
      validUntil: dateOffset(75),
      suppliesDispensed: 1,
      weightKg: 3.6,
      doseMgPerKg: '0.1250',
      doseCheckOutcome: 'AboveRange',
      hasAcknowledgedWarnings: true,
      acknowledgedByName: 'Dr Akter',
      acknowledgedOn: instantOffset(-2, 10),
      doseNote: 'Higher loading dose on day 1 only.',
      warnings: [
        {
          kind: 'DoseAboveRange',
          blocking: false,
          requiresAcknowledgement: true,
          severity: null,
          subject: 'Dose above range',
          detail: '0.12 mg/kg against 0.05–0.10 mg/kg.',
        },
      ],
    }),
    // Mittens: repeatable, but its repeat window ended.
    rx({
      id: ids.rxMittensEndedId,
      number: 'RX-2026-000090',
      catId: ids.catMittensId,
      medicineId: ids.medFusidicId,
      doseAmount: 1,
      doseUnit: 'Application',
      route: 'Topical',
      frequency: 'TwiceDaily',
      durationDays: 10,
      quantity: 1,
      instructions: 'Apply a thin layer to the sore area twice daily.',
      status: 'Dispensed',
      startsOn: dateOffset(-120),
      lastDispensedOn: dateOffset(-100),
      isRepeatable: true,
      repeatsAuthorised: 2,
      validUntil: dateOffset(-14),
      suppliesDispensed: 1,
      issuedOn: instantOffset(-120, 9),
    }),
    // Mittens: dispensed, a repeat already requested and waiting at the pharmacy.
    rx({
      id: ids.rxMittensRequestedId,
      number: 'RX-2026-000095',
      catId: ids.catMittensId,
      medicineId: ids.medAmoxicillinId,
      doseAmount: 1,
      doseUnit: 'Tablet',
      frequency: 'TwiceDaily',
      durationDays: 10,
      quantity: 20,
      status: 'Dispensed',
      startsOn: dateOffset(-12),
      lastDispensedOn: dateOffset(-12),
      isRepeatable: true,
      repeatsAuthorised: 1,
      validUntil: dateOffset(40),
      suppliesDispensed: 1,
      issuedOn: instantOffset(-12, 9),
      weightKg: 3.6,
      doseMgPerKg: '13.8889',
      doseCheckOutcome: 'WithinRange',
      repeatRequests: [
        {
          id: ids.repeatRequestMittensId,
          requestedOn: instantOffset(-1, 11),
          preferredBranchId: ids.mainBranchId,
          note: 'Please have it ready after 5 pm.',
          status: 'Requested',
          decidedOn: null,
          declineReason: null,
          dispensingId: null,
        },
      ],
    }),
    // Whiskers: an active prednisolone course (Major interaction with meloxicam).
    rx({
      id: ids.rxWhiskersPrednisoloneId,
      number: 'RX-2026-000117',
      catId: ids.catWhiskersId,
      medicineId: ids.medPrednisoloneId,
      doseAmount: 1,
      doseUnit: 'Tablet',
      durationDays: 21,
      quantity: 21,
      status: 'Dispensed',
      startsOn: dateOffset(-5),
      lastDispensedOn: dateOffset(-5),
      validUntil: dateOffset(30),
      suppliesDispensed: 1,
      issuedOn: instantOffset(-5, 9),
      weightKg: 4.2,
      doseMgPerKg: '1.1905',
      doseCheckOutcome: 'WithinRange',
    }),
    // Whiskers: an active gabapentin course (a Minor interaction with meloxicam).
    rx({
      id: ids.rxWhiskersGabapentinId,
      number: 'RX-2026-000118',
      catId: ids.catWhiskersId,
      medicineId: ids.medGabapentinId,
      doseAmount: 1,
      doseUnit: 'Capsule',
      frequency: 'TwiceDaily',
      durationDays: 14,
      quantity: 28,
      status: 'Dispensed',
      startsOn: dateOffset(-4),
      lastDispensedOn: dateOffset(-4),
      validUntil: dateOffset(30),
      suppliesDispensed: 1,
      issuedOn: instantOffset(-4, 9),
      weightKg: 4.2,
      doseMgPerKg: '23.8095',
      doseCheckOutcome: 'AboveRange',
    }),
    // Tiger: a controlled injectable dispensed TODAY (a vet or administrator may still reverse it).
    rx({
      id: ids.rxTigerControlledDispensedId,
      number: 'RX-2026-000088',
      catId: ids.catTigerId,
      medicineId: ids.medBuprenorphineId,
      doseAmount: 0.2,
      doseUnit: 'Ml',
      route: 'Subcutaneous',
      frequency: 'TwiceDaily',
      durationDays: 5,
      quantity: 2,
      instructions: 'Give 0.2 ml under the skin twice daily.',
      status: 'Dispensed',
      startsOn: dateOffset(0),
      lastDispensedOn: dateOffset(0),
      suppliesDispensed: 1,
      issuedOn: instantOffset(0, 2),
      weightKg: 5.8,
      doseMgPerKg: '0.0103',
      doseCheckOutcome: 'WithinRange',
    }),
    // Awaiting first supply: amoxicillin for Whiskers.
    rx({
      id: ids.rxWhiskersAmoxicillinQueueId,
      number: 'RX-2026-000121',
      catId: ids.catWhiskersId,
      medicineId: ids.medAmoxicillinId,
      doseAmount: 1,
      doseUnit: 'Tablet',
      frequency: 'TwiceDaily',
      durationDays: 5,
      quantity: 10,
      issuedOn: instantOffset(0, 8),
      startsOn: dateOffset(0),
      weightKg: 4.2,
      doseMgPerKg: '11.9048',
      doseCheckOutcome: 'WithinRange',
    }),
    // Awaiting first supply: a controlled injectable for Tiger.
    rx({
      id: ids.rxTigerControlledQueueId,
      number: 'RX-2026-000122',
      catId: ids.catTigerId,
      medicineId: ids.medBuprenorphineId,
      doseAmount: 0.2,
      doseUnit: 'Ml',
      route: 'Subcutaneous',
      frequency: 'TwiceDaily',
      durationDays: 3,
      quantity: 2,
      instructions: 'Give 0.2 ml under the skin twice daily for three days.',
      issuedOn: instantOffset(0, 9),
      startsOn: dateOffset(0),
      weightKg: 5.8,
      doseMgPerKg: '0.0103',
      doseCheckOutcome: 'WithinRange',
    }),
    // Awaiting first supply, but only expired stock exists.
    rx({
      id: ids.rxExpiredOnlyQueueId,
      number: 'RX-2026-000123',
      catId: ids.catWhiskersId,
      medicineId: ids.medMaropitantId,
      doseAmount: 1,
      doseUnit: 'Tablet',
      durationDays: 4,
      quantity: 4,
      issuedOn: instantOffset(0, 10),
      startsOn: dateOffset(0),
    }),
    // The repeat request waiting at the pharmacy belongs to the amoxicillin course above; a second
    // dispensed prescription backs the "Repeat requests" row for the dispensary.
    rx({
      id: ids.rxMittensRepeatQueueId,
      number: 'RX-2026-000099',
      catId: ids.catMittensId,
      medicineId: ids.medMeloxicamId,
      doseAmount: 0.3,
      doseUnit: 'Ml',
      durationDays: 7,
      quantity: 10,
      status: 'Completed',
      startsOn: dateOffset(-40),
      lastDispensedOn: dateOffset(-33),
      validUntil: dateOffset(-10),
      suppliesDispensed: 1,
      issuedOn: instantOffset(-40, 9),
    }),
  ];
}

function seedDispensings(): MDispensing[] {
  const line = (
    id: string,
    batchId: string,
    lot: string,
    skuId: string,
    qty: number,
    exp: number,
  ) => ({
    lineId: id,
    skuId,
    batchId,
    lotNumber: lot,
    expiresOn: dateOffset(exp),
    quantity: qty,
  });
  return [
    {
      id: ids.dispensingMittensId,
      prescriptionId: ids.rxMittensActiveId,
      supplyNumber: 1,
      isRepeat: false,
      status: 'Dispensed',
      dispensedOn: instantOffset(-2, 11),
      dispensedByName: 'Reception Desk',
      witnessName: null,
      fefoOverrideReason: null,
      lines: [
        line(
          'ea000000-0000-4000-8000-000000000001',
          'e9000000-0000-4000-8000-000000000002',
          'L4551',
          sku('', '', '1').skuId,
          10,
          180,
        ),
      ],
      labelPrintCount: 1,
      reversedOn: null,
      reversalReason: null,
      branchId: ids.mainBranchId,
    },
    {
      id: ids.dispensingControlledId,
      prescriptionId: ids.rxTigerControlledDispensedId,
      supplyNumber: 1,
      isRepeat: false,
      status: 'Dispensed',
      dispensedOn: new Date().toISOString(),
      dispensedByName: 'Nadia Nurse',
      witnessName: 'Wasim Nurse',
      fefoOverrideReason: null,
      lines: [
        line(
          'ea000000-0000-4000-8000-000000000002',
          'e9000000-0000-4000-8000-000000000007',
          'BUP-77',
          sku('', '', '5').skuId,
          2,
          400,
        ),
      ],
      labelPrintCount: 0,
      reversedOn: null,
      reversalReason: null,
      branchId: ids.mainBranchId,
    },
  ];
}

function entry(
  partial: Partial<MRegisterEntry> & Pick<MRegisterEntry, 'id' | 'entryNumber' | 'kind' | 'delta'>,
): MRegisterEntry {
  return {
    recordedOn: instantOffset(-10, 9),
    occurredOn: instantOffset(-10, 9),
    medicineId: ids.medBuprenorphineId,
    batchId: 'e9000000-0000-4000-8000-000000000007',
    lotNumber: 'BUP-77',
    batchExpiresOn: dateOffset(400),
    doseText: null,
    wastage: null,
    catName: null,
    customerName: null,
    actorName: 'Dr Akter',
    witnessName: null,
    sourceType: 'GoodsReceipt',
    sourceId: 'ec000000-0000-4000-8000-000000000001',
    sourceLabel: 'Goods receipt GR-2026-000014',
    correctionReason: null,
    notes: null,
    correctsEntryNumber: null,
    isSealed: true,
    branchId: ids.mainBranchId,
    ...partial,
  };
}

function seedEntries(): MRegisterEntry[] {
  return [
    entry({
      id: 'eb000000-0000-4000-8000-000000000001',
      entryNumber: 1198,
      kind: 'OpeningBalance',
      delta: 20,
      sourceType: 'StockAdjustment',
      sourceLabel: 'Opening balance',
      recordedOn: instantOffset(-30, 9),
    }),
    entry({
      id: 'eb000000-0000-4000-8000-000000000002',
      entryNumber: 1199,
      kind: 'Receipt',
      delta: 10,
      recordedOn: instantOffset(-20, 9),
    }),
    entry({
      id: 'eb000000-0000-4000-8000-000000000003',
      entryNumber: 1200,
      kind: 'Dispensed',
      delta: -2,
      doseText: '0.2 ml twice daily',
      catName: 'Tiger',
      customerName: 'Rahim Uddin',
      witnessName: 'Nadia Nurse',
      sourceType: 'Dispensing',
      sourceId: ids.dispensingControlledId,
      sourceLabel: 'Dispensing RX-2026-000088 #1',
      recordedOn: instantOffset(-9, 10),
    }),
    entry({
      id: 'eb000000-0000-4000-8000-000000000004',
      entryNumber: 1201,
      kind: 'Administered',
      delta: -0.3,
      doseText: '0.3 ml once',
      catName: 'Whiskers',
      customerName: 'Sam Rahman',
      witnessName: 'Nadia Nurse',
      sourceType: 'Treatment',
      sourceId: 'ec000000-0000-4000-8000-000000000002',
      sourceLabel: 'Treatment during visit VIS-2026-004790',
      recordedOn: instantOffset(-6, 14),
    }),
    entry({
      id: 'eb000000-0000-4000-8000-000000000005',
      entryNumber: 1202,
      kind: 'Wastage',
      delta: -0.1,
      wastage: 0.1,
      witnessName: 'Nadia Nurse',
      sourceType: 'Treatment',
      sourceId: 'ec000000-0000-4000-8000-000000000002',
      sourceLabel: 'Treatment during visit VIS-2026-004790',
      recordedOn: instantOffset(-6, 14),
    }),
    // A correction of #1200: the dose text and one unit less in stock than recorded.
    entry({
      id: 'eb000000-0000-4000-8000-000000000006',
      entryNumber: 1203,
      kind: 'Correction',
      delta: -1,
      doseText: '0.2 ml twice daily for 5 days',
      catName: 'Tiger',
      customerName: 'Rahim Uddin',
      witnessName: 'Nadia Nurse',
      sourceType: 'Correction',
      sourceId: 'eb000000-0000-4000-8000-000000000003',
      sourceLabel: 'Correction of #1200',
      correctionReason: 'Duration was recorded as 3 days; the prescription says 5.',
      correctsEntryNumber: 1200,
      recordedOn: instantOffset(-2, 9),
    }),
    entry({
      id: 'eb000000-0000-4000-8000-000000000007',
      entryNumber: 1204,
      kind: 'Returned',
      delta: 1,
      catName: 'Tiger',
      sourceType: 'DispensingReversal',
      sourceId: ids.dispensingControlledId,
      sourceLabel: 'Reversal of dispensing RX-2026-000088 #1',
      witnessName: 'Nadia Nurse',
      recordedOn: instantOffset(-1, 16),
      isSealed: false,
    }),
    // A second lot whose register balance (8) differs from the stock on hand (7.5): a mismatch.
    entry({
      id: 'eb000000-0000-4000-8000-000000000008',
      entryNumber: 1205,
      kind: 'Receipt',
      delta: 8,
      batchId: 'e9000000-0000-4000-8000-000000000010',
      lotNumber: 'BUP-81',
      batchExpiresOn: dateOffset(300),
      sourceLabel: 'Goods receipt GR-2026-000021',
      recordedOn: instantOffset(-1, 12),
    }),
  ];
}

function seedDiets(): MDiet[] {
  return [
    {
      id: ids.dietAuthorisationActiveId,
      catId: ids.catMittensId,
      productId: ids.dietProductRenalId,
      productName: DIET_PRODUCTS[0]!.name,
      validFrom: dateOffset(-20),
      validTo: dateOffset(70),
      revoked: false,
      authorisedBy: 'Dr Akter',
      visitId: 'b1000000-0000-0000-0000-000000000005',
      notes: 'Early-stage CKD; renal diet only.',
    },
    {
      id: 'e5000000-0000-4000-8000-000000000002',
      catId: ids.catMittensId,
      productId: ids.dietProductUrinaryId,
      productName: DIET_PRODUCTS[1]!.name,
      validFrom: dateOffset(-200),
      validTo: dateOffset(-20),
      revoked: false,
      authorisedBy: 'Dr Akter',
      visitId: 'b1000000-0000-0000-0000-000000000005',
      notes: null,
    },
  ];
}

function seed(): MockDb {
  return {
    seq: 200,
    medicines: seedMedicines(),
    batches: seedBatches(),
    prescriptions: seedPrescriptions(),
    dispensings: seedDispensings(),
    entries: seedEntries(),
    diets: seedDiets(),
    brokenAtPosition: null,
  };
}

const persisted = loadPersisted<MockDb>(PERSIST_KEY);
export const db: MockDb = persisted ?? seed();

export function persist(): void {
  savePersisted(PERSIST_KEY, db);
}

export function resetPharmacyMockData(): void {
  clearPersisted(PERSIST_KEY);
  Object.assign(db, seed());
}

export function nextSeq(): number {
  db.seq += 1;
  return db.seq;
}

export function newId(prefix: string): string {
  return `${prefix.padEnd(8, '0')}-0000-4000-8000-${String(nextSeq()).padStart(12, '0')}`;
}

export function findMedicine(id: string): MMedicine | undefined {
  return db.medicines.find((m) => m.id === id);
}

export function findPrescription(id: string): MPrescription | undefined {
  return db.prescriptions.find((p) => p.id === id);
}

export function today(): string {
  return dateOffset(0);
}

/** The branch-local calendar day (Asia/Dhaka) of an instant, `yyyy-MM-dd`. */
export function localDay(instant: string): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Dhaka' }).format(new Date(instant));
}
