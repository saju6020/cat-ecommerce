import { apiFetch } from './client';
import { queryString } from './formulary';
import type { PagedResult } from './types';
import type { QuantityValue } from '@/lib/format/units';

/**
 * `lib/api/controlled-drugs.ts` — HO-PHM-002 §8.2. Built to `StaffControlledDrugsController` and
 * `PharmacyRegisterViewModels.cs`. The register is append-only: there is deliberately no update or
 * delete call here (ADR-FE-065, C-2). A correction is a NEW entry (`recordCorrection`), and only a
 * vet may add one.
 */

export type RegisterEntryKind =
  | 'OpeningBalance'
  | 'Receipt'
  | 'Dispensed'
  | 'Administered'
  | 'Wastage'
  | 'Returned'
  | 'Adjustment'
  | 'Correction';

export const REGISTER_ENTRY_KINDS: RegisterEntryKind[] = [
  'OpeningBalance',
  'Receipt',
  'Dispensed',
  'Administered',
  'Wastage',
  'Returned',
  'Adjustment',
  'Correction',
];

export interface RegisterMedicine {
  id: string;
  name: string;
  strengthText: string;
  form: string;
  controlledSchedule: string | null;
}

export interface RegisterSource {
  /** `Dispensing`, `DispensingReversal`, `Treatment`, `Surgery`, `GoodsReceipt`, `StockAdjustment`, `Correction`. */
  type: string;
  id: string;
  label: string;
}

export interface RegisterEntry {
  id: string;
  entryNumber: number;
  recordedOn: string;
  occurredOn: string;
  kind: RegisterEntryKind;
  medicine: RegisterMedicine;
  batchId: string | null;
  lotNumber: string | null;
  batchExpiresOn: string | null;
  /** Signed. */
  quantityDelta: QuantityValue;
  /** The register's balance for this medicine and batch AS OF this entry. */
  runningBalance: QuantityValue;
  doseText: string | null;
  wastage: QuantityValue | null;
  catName: string | null;
  customerName: string | null;
  actorName: string;
  witnessName: string | null;
  source: RegisterSource;
  correctionReason: string | null;
  notes: string | null;
  correctsEntryNumber: number | null;
  correctedByEntryNumbers: number[];
  isSealed: boolean;
}

export interface RegisterSeal {
  chainPosition: number;
  entryHash: string;
  sealedOn: string;
}

export interface RegisterEntryDetail {
  entry: RegisterEntry;
  /** Every entry that corrects this one, oldest first. */
  corrections: RegisterEntry[];
  seal: RegisterSeal | null;
}

export interface RegisterBalance {
  medicineId: string;
  medicineName: string;
  batchId: string | null;
  lotNumber: string | null;
  registerBalance: QuantityValue;
  stockBalance: QuantityValue | null;
  isMismatch: boolean;
}

export interface RegisterVerification {
  isIntact: boolean;
  entriesChecked: number;
  firstBrokenPosition: number | null;
  staleUnsealed: Array<{ entryNumber: number; recordedOn: string }>;
  checkedOn: string;
}

export interface RegisterFilters {
  medicineId?: string;
  batchId?: string;
  kind?: string;
  from?: string;
  to?: string;
  pageNumber?: number;
  pageSize?: number;
}

export interface CorrectionInput {
  correctedDoseText?: string | null;
  correctedCatId?: string | null;
  quantityDelta: number;
  notes?: string | null;
  reason: string;
  witnessIdentifier: string;
  witnessPassword: string;
}

const base = '/api/staff/controlled-drugs';

export function getRegister(filters: RegisterFilters): Promise<PagedResult<RegisterEntry>> {
  return apiFetch(`${base}/register${queryString({ ...filters })}`);
}

export function getRegisterEntry(entryId: string): Promise<RegisterEntryDetail> {
  return apiFetch(`${base}/register/${entryId}`);
}

export function recordCorrection(
  entryId: string,
  body: CorrectionInput,
): Promise<{ entryId: string; entryNumber: number }> {
  return apiFetch(`${base}/register/${entryId}/corrections`, { method: 'POST', body });
}

export function getRegisterBalances(): Promise<RegisterBalance[]> {
  return apiFetch(`${base}/balances`);
}

export function getRegisterVerification(): Promise<RegisterVerification> {
  return apiFetch(`${base}/verification`);
}

/**
 * The export endpoint takes only `medicineId`, `from` and `to` (not batch or kind). The path is used
 * by the branch-aware download (an anchor could not send `X-Branch-Id`), never fetched as JSON.
 */
export function registerExportPath(filters: RegisterFilters): string {
  return `${base}/register/export${queryString({
    medicineId: filters.medicineId,
    from: filters.from,
    to: filters.to,
  })}`;
}
