import { apiFetch } from './client';
import type { Money } from '../format/money';
import { wireMoney, wireQuantity } from '../money';

/**
 * `lib/api/ordering-admin.ts` — HO-ORD-002 §8.2. Delivery zones, couriers, discount codes and the
 * bank-transfer account, as `AdminDeliveryZonesController` / `AdminCouriersController` /
 * `AdminDiscountCodesController` / `AdminBankTransferAccountController` send and take them.
 * Request money is a .NET `decimal`, so an amount is sent as a JSON number literal built from the
 * typed string (`wireMoney`) — never parsed in the browser.
 */

// ---- delivery zones --------------------------------------------------------------------------------

export interface DeliveryZoneSummary {
  id: string;
  branchId: string;
  name: string;
  priority: number;
  isActive: boolean;
  codEnabled: boolean;
  coverageCount: number;
  serviceLevels: string[];
}

export interface WeightBand {
  upToGrams: number | null;
  surcharge: Money;
}

export interface SlotWindow {
  dayOfWeek: string;
  start: string;
  end: string;
  label: string;
}

export interface ZoneService {
  serviceLevel: 'Standard' | 'Express';
  isActive: boolean;
  baseCharge: Money;
  freeOverOrderValue: Money | null;
  leadTimeDays: number;
  weightBands: WeightBand[];
  slotWindows: SlotWindow[];
}

export interface ZoneCoverage {
  district: string;
  postalCodes: string[];
}

export interface DeliveryZone {
  id: string;
  branchId: string;
  name: string;
  priority: number;
  isActive: boolean;
  codEnabled: boolean;
  codMaxOrderValue: Money | null;
  currencyCode: string;
  version: number;
  coverage: ZoneCoverage[];
  services: ZoneService[];
}

/** The form's shape: strings the person typed (money stays a string). */
export interface DeliveryZoneInput {
  branchId: string;
  name: string;
  priority: number;
  isActive: boolean;
  codEnabled: boolean;
  codMaxOrderValue: string;
  coverage: Array<{ district: string; postalCodes: string[] }>;
  services: Array<{
    serviceLevel: 'Standard' | 'Express';
    isActive: boolean;
    baseCharge: string;
    freeOverOrderValue: string;
    leadTimeDays: number;
    weightBands: Array<{ upToGrams: number | null; surcharge: string }>;
    slotWindows: Array<{ dayOfWeek: string; start: string; end: string }>;
  }>;
}

function zoneBody(input: DeliveryZoneInput): unknown {
  return {
    branchId: input.branchId,
    name: input.name,
    priority: input.priority,
    isActive: input.isActive,
    codEnabled: input.codEnabled,
    codMaxOrderValue: input.codMaxOrderValue ? wireMoney(input.codMaxOrderValue) : null,
    coverage: input.coverage,
    services: input.services.map((service) => ({
      serviceLevel: service.serviceLevel,
      isActive: service.isActive,
      baseCharge: wireMoney(service.baseCharge),
      freeOverOrderValue: service.freeOverOrderValue ? wireMoney(service.freeOverOrderValue) : null,
      leadTimeDays: service.leadTimeDays,
      weightBands: service.weightBands.map((band) => ({
        upToGrams: band.upToGrams,
        surcharge: wireMoney(band.surcharge),
      })),
      slotWindows: service.slotWindows,
    })),
  };
}

export function listDeliveryZones(): Promise<DeliveryZoneSummary[]> {
  return apiFetch('/api/admin/delivery-zones');
}

export function getDeliveryZone(zoneId: string): Promise<DeliveryZone> {
  return apiFetch(`/api/admin/delivery-zones/${encodeURIComponent(zoneId)}`);
}

export function createDeliveryZone(input: DeliveryZoneInput): Promise<{ zoneId: string }> {
  return apiFetch('/api/admin/delivery-zones', { method: 'POST', body: zoneBody(input) });
}

export function updateDeliveryZone(zoneId: string, input: DeliveryZoneInput): Promise<void> {
  return apiFetch(`/api/admin/delivery-zones/${encodeURIComponent(zoneId)}`, {
    method: 'PUT',
    body: zoneBody(input),
  });
}

export function setDeliveryZoneActivation(zoneId: string, isActive: boolean): Promise<void> {
  return apiFetch(`/api/admin/delivery-zones/${encodeURIComponent(zoneId)}/activation`, {
    method: 'PUT',
    body: { isActive },
  });
}

export interface ResolvedZone {
  matched: boolean;
  zoneId: string | null;
  zoneName: string | null;
  branchId: string | null;
  reasonCode: string | null;
}

export function resolveDeliveryZone(district: string, postalCode?: string): Promise<ResolvedZone> {
  const search = new URLSearchParams({ district });
  if (postalCode) search.set('postalCode', postalCode);
  return apiFetch(`/api/admin/delivery-zones/resolve?${search.toString()}`);
}

// ---- couriers --------------------------------------------------------------------------------------

export interface Courier {
  id: string;
  name: string;
  trackingUrlTemplate: string | null;
  supportsCod: boolean;
  isActive: boolean;
}

export interface CourierInput {
  name: string;
  trackingUrlTemplate: string;
  supportsCod: boolean;
  isActive: boolean;
}

const courierBody = (input: CourierInput) => ({
  ...input,
  trackingUrlTemplate: input.trackingUrlTemplate.trim() || null,
});

export function listCouriers(): Promise<Courier[]> {
  return apiFetch('/api/admin/couriers');
}

export function createCourier(input: CourierInput): Promise<{ courierId: string }> {
  return apiFetch('/api/admin/couriers', { method: 'POST', body: courierBody(input) });
}

export function updateCourier(courierId: string, input: CourierInput): Promise<void> {
  return apiFetch(`/api/admin/couriers/${encodeURIComponent(courierId)}`, {
    method: 'PUT',
    body: courierBody(input),
  });
}

// ---- discount codes --------------------------------------------------------------------------------

export type DiscountKind = 'Percentage' | 'FixedAmount';
export type DiscountScope = 'Order' | 'Products' | 'Categories';

export interface DiscountCode {
  id: string;
  code: string;
  description: string | null;
  kind: DiscountKind;
  percent: number | null;
  amount: Money | null;
  minimumSpend: Money | null;
  scope: DiscountScope;
  productIds: string[];
  categoryIds: number[];
  firstOrderOnly: boolean;
  usageLimit: number | null;
  perCustomerLimit: number | null;
  validFrom: string | null;
  validTo: string | null;
  isActive: boolean;
  timesRedeemed: number;
  version: number;
}

export interface DiscountCodeInput {
  code: string;
  description: string;
  kind: DiscountKind;
  percent: string;
  amount: string;
  minimumSpend: string;
  scope: DiscountScope;
  productIds: string[];
  categoryIds: number[];
  firstOrderOnly: boolean;
  usageLimit: string;
  perCustomerLimit: string;
  /** ISO instants or empty. */
  validFrom: string;
  validTo: string;
  isActive: boolean;
}

function intOrNull(text: string): number | null {
  return /^\d{1,9}$/.test(text.trim()) ? Math.trunc(Number(text.trim())) : null;
}

function discountBody(input: DiscountCodeInput, includeCode: boolean): unknown {
  return {
    ...(includeCode ? { code: input.code } : {}),
    description: input.description.trim() || null,
    kind: input.kind,
    percent: input.kind === 'Percentage' && input.percent ? wireQuantity(input.percent) : null,
    amount: input.kind === 'FixedAmount' && input.amount ? wireMoney(input.amount) : null,
    minimumSpend: input.minimumSpend ? wireMoney(input.minimumSpend) : null,
    scope: input.scope,
    productIds: input.scope === 'Products' ? input.productIds : [],
    categoryIds: input.scope === 'Categories' ? input.categoryIds : [],
    firstOrderOnly: input.firstOrderOnly,
    usageLimit: intOrNull(input.usageLimit),
    perCustomerLimit: intOrNull(input.perCustomerLimit),
    validFrom: input.validFrom || null,
    validTo: input.validTo || null,
    isActive: input.isActive,
  };
}

export function listDiscountCodes(): Promise<DiscountCode[]> {
  return apiFetch('/api/admin/discount-codes');
}

export function createDiscountCode(input: DiscountCodeInput): Promise<{ discountCodeId: string }> {
  return apiFetch('/api/admin/discount-codes', { method: 'POST', body: discountBody(input, true) });
}

export function updateDiscountCode(codeId: string, input: DiscountCodeInput): Promise<void> {
  return apiFetch(`/api/admin/discount-codes/${encodeURIComponent(codeId)}`, {
    method: 'PUT',
    body: discountBody(input, false),
  });
}

export function setDiscountCodeActivation(codeId: string, isActive: boolean): Promise<void> {
  return apiFetch(`/api/admin/discount-codes/${encodeURIComponent(codeId)}/activation`, {
    method: 'PUT',
    body: { isActive },
  });
}

// ---- bank-transfer account -------------------------------------------------------------------------

export interface BankTransferAccount {
  configured: boolean;
  accountName: string | null;
  accountNumber: string | null;
  bankName: string | null;
  branchName: string | null;
  routingNumber: string | null;
  isActive: boolean;
}

export interface BankTransferAccountInput {
  accountName: string;
  accountNumber: string;
  bankName: string;
  branchName: string;
  routingNumber: string;
  isActive: boolean;
}

export function getBankTransferAccount(): Promise<BankTransferAccount> {
  return apiFetch('/api/admin/bank-transfer-account');
}

export function setBankTransferAccount(input: BankTransferAccountInput): Promise<void> {
  return apiFetch('/api/admin/bank-transfer-account', {
    method: 'PUT',
    body: { ...input, routingNumber: input.routingNumber.trim() || null },
  });
}
