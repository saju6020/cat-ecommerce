import { apiFetch } from './client';
import { queryString } from './formulary';
import type { PrescriptionMedicine, PrescriptionSummary } from './prescriptions';
import type { QuantityValue } from '@/lib/format/units';

/**
 * `lib/api/pharmacy.ts` — HO-PHM-002 §8.2. Built to `StaffPharmacyController`,
 * `StaffDispensingsController` and `PharmacyDispensingViewModels.cs`. The dispense plan is the
 * server's FEFO answer: the client lists, preselects the server's suggestion and sends back the
 * batches chosen — it never orders, filters or compares expiry itself (ADR-FE-062).
 */

export interface PharmacyQueueItem {
  prescription: PrescriptionSummary;
  catName: string;
  catIsHighRisk: boolean;
  customerName: string;
}

export interface PharmacyQueueRepeat {
  requestId: string;
  prescriptionId: string;
  prescriptionNumber: string;
  catName: string;
  customerName: string;
  medicine: PrescriptionMedicine;
  requestedOn: string;
  note: string | null;
}

export interface PharmacyQueue {
  generatedOn: string;
  awaitingFirstSupply: PharmacyQueueItem[];
  repeatRequests: PharmacyQueueRepeat[];
}

export interface PlanBatch {
  skuId: string;
  productName: string;
  variantLabel: string;
  batchId: string;
  lotNumber: string;
  barcode: string | null;
  expiresOn: string | null;
  available: QuantityValue;
  isExpired: boolean;
  isDispensable: boolean;
  notDispensableReason: string | null;
}

export interface SuggestedLine {
  skuId: string;
  batchId: string;
  quantity: QuantityValue;
}

export interface DispensePlan {
  prescription: PrescriptionSummary;
  /** Header facts only — `{ id, name, isHighRisk, handlingNote }`. */
  cat: { id: string; name: string; isHighRisk: boolean; handlingNote: string | null };
  quantity: QuantityValue;
  isControlled: boolean;
  /** Role names the witness may hold ("Vet", "Nurse"). */
  witnessRoles: string[];
  canDispense: boolean;
  blockingReason: string | null;
  batches: PlanBatch[];
  suggestedLines: SuggestedLine[];
  onlyExpiredStockAvailable: boolean;
  shortfall: QuantityValue | null;
}

export interface MedicineLabel {
  catName: string;
  ownerName: string;
  medicineLine: string;
  doseLine: string;
  instructions: string;
  dispensedOn: string;
  expiresOn: string | null;
  quantityLine: string;
  prescriptionNumber: string;
  prescriberName: string;
  clinicName: string;
  clinicPhone: string;
  clinicAddress: string | null;
  controlledNotice: string | null;
  standardWarnings: string;
}

export interface DispensingLineView {
  lineId: string;
  skuId: string;
  batchId: string;
  lotNumber: string;
  expiresOn: string | null;
  quantity: QuantityValue;
  isPriced: boolean;
}

export interface Dispensing {
  id: string;
  prescriptionId: string;
  catId: string;
  prescriptionNumber: string;
  supplyNumber: number;
  isRepeat: boolean;
  /** `Dispensed` or `Reversed`. */
  status: string;
  dispensedOn: string;
  dispensedOnLocal: string;
  dispensedByName: string;
  witnessName: string | null;
  fefoOverrideReason: string | null;
  lines: DispensingLineView[];
  label: MedicineLabel;
  labelPrintCount: number;
  reversedOn: string | null;
  reversalReason: string | null;
  allowedActions: Array<'Reprint' | 'Reverse'>;
}

export interface DispenseLineInput {
  skuId: string;
  batchId: string;
  quantity: number;
}

export interface WitnessCredentials {
  witnessIdentifier: string;
  witnessPassword: string;
}

export interface DispenseRequest extends Partial<WitnessCredentials> {
  lines: DispenseLineInput[];
  fefoOverrideReason?: string | null;
  repeatRequestId?: string | null;
}

export interface DispenseResult {
  dispensingId: string;
  supplyNumber: number;
  label: MedicineLabel;
  /** Ids of lines with no price set — Billing has been told. */
  unpricedLines: string[];
}

export function getPharmacyQueue(): Promise<PharmacyQueue> {
  return apiFetch('/api/staff/pharmacy/queue');
}

export function lookupPrescription(number: string): Promise<{ prescriptionId: string }> {
  return apiFetch(`/api/staff/pharmacy/lookup${queryString({ number })}`);
}

export function getDispensePlan(prescriptionId: string): Promise<DispensePlan> {
  return apiFetch(`/api/staff/prescriptions/${prescriptionId}/dispense-plan`);
}

export function dispensePrescription(
  prescriptionId: string,
  body: DispenseRequest,
): Promise<DispenseResult> {
  return apiFetch(`/api/staff/prescriptions/${prescriptionId}/dispensings`, {
    method: 'POST',
    body,
  });
}

export function getDispensing(dispensingId: string): Promise<Dispensing> {
  return apiFetch(`/api/staff/dispensings/${dispensingId}`);
}

export function recordLabelPrint(dispensingId: string): Promise<void> {
  return apiFetch(`/api/staff/dispensings/${dispensingId}/label-prints`, { method: 'POST' });
}

export function reverseDispensing(
  dispensingId: string,
  body: { reason: string } & Partial<WitnessCredentials>,
): Promise<void> {
  return apiFetch(`/api/staff/dispensings/${dispensingId}/reverse`, { method: 'POST', body });
}
