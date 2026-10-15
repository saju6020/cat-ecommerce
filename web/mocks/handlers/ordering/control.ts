import { findSku, persist as persistShop } from '../shop/store';
import { orderingIds as ids } from './fixtures';
import {
  centsOfString,
  db,
  money,
  persist,
  resetOrderingMockData,
  skuInfo,
  uuid,
} from './store';

/**
 * `mocks/handlers/ordering/control.ts` — HO-ORD-002 §12.1. Test controls for the mock **server**
 * (never app code): a few things an end-to-end test cannot cause through the UI — "the price changed
 * between cart and checkout", "an authorisation lapsed", "the transfer window is 30 seconds", "someone
 * else is signed in as staff", "the next placement answers 409". Exposed on `window.__orderingMock`
 * only in a development/test build; mocks load only when `NEXT_PUBLIC_USE_MOCKS=true`.
 */
export const testControl: {
  failNextPlacement: null | 'RequestInProgress' | 'StockNoLongerAvailable' | 'CheckoutQuoteChanged';
} = { failNextPlacement: null };

export interface OrderingMockControls {
  /** Switch the staff identity the mock acts as ("a different approver"). */
  actAs(staffId: string): void;
  actAsOther(): void;
  actAsAdmin(): void;
  setTransferWindowSeconds(seconds: number): void;
  expireTransfersNow(): void;
  lapseAuthorisation(catId: string): void;
  restoreAuthorisation(catId: string): void;
  setPrice(skuId: string, amount: string): void;
  setStock(skuId: string, quantity: number): void;
  recallBatchOf(orderId: string): void;
  setTaxRatePercent(percent: number): void;
  addPendingPolicy(title: string): void;
  clearPendingPolicies(): void;
  failNextPlacement(kind: 'RequestInProgress' | 'StockNoLongerAvailable' | 'CheckoutQuoteChanged' | null): void;
  seedGuestCart(skuId: string, quantity: number): void;
  reset(): void;
}

export function createControls(): OrderingMockControls {
  return {
    actAs: (staffId) => {
      db.actor = staffId;
      persist();
    },
    actAsOther: () => {
      db.actor = ids.otherStaffId;
      persist();
    },
    actAsAdmin: () => {
      db.actor = ids.adminStaffId;
      persist();
    },
    setTransferWindowSeconds: (seconds) => {
      db.transferWindowSeconds = seconds;
      persist();
    },
    expireTransfersNow: () => {
      for (const o of db.orders) {
        if (o.status === 'PendingPayment' && o.paymentMethod === 'BankTransfer') {
          o.paymentDueBy = new Date(Date.now() - 1000).toISOString();
        }
      }
      persist();
    },
    lapseAuthorisation: (catId) => {
      if (!db.lapsedCats.includes(catId)) db.lapsedCats.push(catId);
      persist();
    },
    restoreAuthorisation: (catId) => {
      db.lapsedCats = db.lapsedCats.filter((id) => id !== catId);
      persist();
    },
    setPrice: (skuId, amount) => {
      const found = findSku(skuId);
      if (!found) return;
      found.sku.price = money(centsOfString(amount)).amount;
      persistShop();
    },
    setStock: (skuId, quantity) => {
      db.stock[skuId] = quantity;
      persist();
    },
    recallBatchOf: (orderId) => {
      const order = db.orders.find((o) => o.id === orderId);
      for (const line of order?.lines ?? []) {
        for (const allocation of line.allocations) {
          if (!db.recalledBatches.includes(allocation.batchId)) db.recalledBatches.push(allocation.batchId);
        }
      }
      persist();
    },
    setTaxRatePercent: (percent) => {
      db.taxRatePercent = percent;
      persist();
    },
    addPendingPolicy: (title) => {
      db.pendingPolicies = [{ key: 'terms', versionNumber: 3, title }];
      persist();
    },
    clearPendingPolicies: () => {
      db.pendingPolicies = [];
      persist();
    },
    failNextPlacement: (kind) => {
      testControl.failNextPlacement = kind;
    },
    seedGuestCart: (skuId, quantity) => {
      const info = skuInfo(skuId);
      if (!info?.priceCents) return;
      db.guestLines.push({
        lineId: uuid(),
        skuId,
        quantity,
        catId: null,
        priceWhenAddedCents: info.priceCents,
      });
      persist();
    },
    reset: () => resetOrderingMockData(),
  };
}

declare global {
  interface Window {
    __orderingMock?: OrderingMockControls;
  }
}

export function installOrderingMockControls(): void {
  if (typeof window === 'undefined') return;
  window.__orderingMock = createControls();
}
