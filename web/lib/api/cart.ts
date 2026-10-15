import { apiFetch } from './client';
import type { Money } from '../format/money';

/**
 * `lib/api/cart.ts` — HO-ORD-002 §8.2. The cart endpoints of HO-ORD-001 §12.4, as the real
 * `CartController` / `CartViewModels.cs` send them (verified against `src/`, not only the
 * handover). Guest or customer: the guest identity is the API's `HttpOnly` `pc_cart` cookie, which
 * this code never reads (ADR-FE-089). Money is `{ amount: "850.00", currencyCode }`; the browser
 * renders it and does nothing else with it.
 */

export interface CartCat {
  id: string;
  name: string;
}

export type CartLineAvailability = 'InStock' | 'Limited' | 'Unavailable';
export type CartPrescriptionStatus = 'Authorised' | 'NotAuthorised' | 'CatRequired';

export interface CartLine {
  lineId: string;
  skuId: string;
  productId: string;
  productName: string;
  variantLabel: string;
  imagePath: string | null;
  quantity: number;
  cat: CartCat | null;
  unitPrice: Money | null;
  priceWhenAdded: Money;
  priceChanged: boolean;
  availability: CartLineAvailability;
  availableQuantity: number | null;
  requiresPrescription: boolean;
  prescriptionStatus: CartPrescriptionStatus | null;
  lineSubtotal: Money | null;
}

export interface Cart {
  cartId: string | null;
  lines: CartLine[];
  itemCount: number;
  subtotal: Money;
  maxQuantityPerLine: number;
  guestCartPending: boolean;
}

export function getCart(): Promise<Cart> {
  return apiFetch<Cart>('/api/cart');
}

export interface AddCartLineRequest {
  skuId: string;
  quantity: number;
  catId?: string;
}

export function addCartLine(request: AddCartLineRequest): Promise<{ lineId: string; cartId: string }> {
  return apiFetch('/api/cart/lines', { method: 'POST', body: request });
}

export function updateCartLine(
  lineId: string,
  change: { quantity?: number; catId?: string },
): Promise<void> {
  return apiFetch(`/api/cart/lines/${encodeURIComponent(lineId)}`, { method: 'PUT', body: change });
}

export function removeCartLine(lineId: string): Promise<void> {
  return apiFetch(`/api/cart/lines/${encodeURIComponent(lineId)}`, { method: 'DELETE' });
}

export interface DroppedCartLine {
  productName: string;
  /** A code (`CartFull`); `describeDroppedReason` turns it into a sentence. Never rendered. */
  reason: string;
}

export interface MergeCartResult {
  mergedLineCount: number;
  droppedLines: DroppedCartLine[];
}

export function mergeGuestCart(): Promise<MergeCartResult> {
  return apiFetch('/api/customers/me/cart/merge', { method: 'POST' });
}
