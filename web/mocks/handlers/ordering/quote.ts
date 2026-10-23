import {
  CAT_NAMES,
  MAX_QUANTITY_PER_LINE,
  PICKUP_BRANCHES,
  ZONE,
  addDays,
  authorisedCatsFor,
  db,
  formatCents,
  money,
  skuInfo,
  stockOf,
  todayLocal,
  weekdayName,
  type Cents,
  type DbZone,
} from './store';

/**
 * `mocks/handlers/ordering/quote.ts` — HO-ORD-002 §12.1. The mock server's checkout/counter pricing:
 * the same shape as `CheckoutQuoteViewModel` / `CounterQuoteViewModel`, the same `changes[]` kinds and
 * blocking codes as `CheckoutPricing`. Integer minor units live here only.
 */

export interface QuoteLineInput {
  lineId: string;
  skuId: string;
  quantity: number;
  catId: string | null;
  priceWhenAddedCents: Cents;
}

export interface QuoteChoicesInput {
  fulfilment: 'Delivery' | 'Pickup' | 'Counter';
  /** `undefined` = no address chosen; `null` = an address id the customer does not own. */
  address?: { district: string; postalCode: string | null } | null;
  serviceLevel?: string;
  slotDate?: string;
  slotWindowStart?: string;
  pickupBranchId?: string;
  paymentMethod?: string;
  discountCode?: string;
  customerHasOrders: boolean;
}

export interface PricedLine {
  lineRef: string;
  skuId: string;
  productName: string;
  variantLabel: string;
  catId: string | null;
  quantity: number;
  unitCents: Cents;
  subtotalCents: Cents;
  discountCents: Cents;
  shareCents: Cents;
  taxRatePercent: number;
  taxCents: Cents;
  totalCents: Cents;
  returnClass: 'Food' | 'Litter' | 'Other';
}

export interface QuoteResult {
  wire: Record<string, unknown>;
  priced: PricedLine[];
  totals: {
    subtotal: Cents;
    lineDiscount: Cents;
    orderDiscount: Cents;
    delivery: Cents;
    tax: Cents;
    grand: Cents;
  };
  canPlace: boolean;
  blocking: string[];
  fingerprint: string;
  zone: DbZone | null;
  level: string | null;
  discountApplied: boolean;
}

const catRef = (catId: string | null) =>
  catId ? { id: catId, name: CAT_NAMES[catId] ?? 'Your cat' } : null;

function hash(text: string): string {
  let h = 5381;
  for (let i = 0; i < text.length; i += 1) h = ((h << 5) + h + text.charCodeAt(i)) | 0;
  return `fp-${(h >>> 0).toString(16)}`;
}

function percentOf(cents: Cents, percent: number): Cents {
  return Math.round((cents * percent) / 100);
}

function slotsFor(zone: DbZone, level: string): Array<{ date: string; windowStart: string; windowEnd: string; label: string }> {
  const service = zone.services.find((s) => s.serviceLevel === level);
  if (!service || service.slotWindows.length === 0) return [];
  const slots: Array<{ date: string; windowStart: string; windowEnd: string; label: string }> = [];
  const first = addDays(todayLocal(), Math.max(1, service.leadTimeDays));
  for (let offset = 0; offset < 7; offset += 1) {
    const date = addDays(first, offset);
    for (const window of service.slotWindows.filter((w) => w.dayOfWeek === weekdayName(date))) {
      slots.push({
        date,
        windowStart: window.start,
        windowEnd: window.end,
        label: `${window.start} to ${window.end}`,
      });
    }
  }
  return slots;
}

function findZone(address: { district: string; postalCode: string | null } | null | undefined): DbZone | null {
  if (!address) return null;
  return (
    db.zones
      .filter((z) => z.isActive)
      .sort((a, b) => a.priority - b.priority)
      .find((z) =>
        z.coverage.some(
          (c) =>
            c.district.toLowerCase() === address.district.toLowerCase() &&
            (c.postalCodes.length === 0 ||
              (address.postalCode !== null && c.postalCodes.includes(address.postalCode))),
        ),
      ) ?? null
  );
}

export function deliveryChargeFor(zone: DbZone, level: string, goodsValue: Cents): Cents {
  const service = zone.services.find((s) => s.serviceLevel === level);
  if (!service) return 0;
  if (service.freeOverCents !== null && goodsValue >= service.freeOverCents) return 0;
  return service.baseChargeCents;
}

export function evaluateDiscount(
  codeText: string,
  goodsCents: Cents,
  hasOrders: boolean,
): { code: string; applied: boolean; reasonCode: string | null; message: string | null; cents: Cents } {
  const code = codeText.trim().toUpperCase();
  const found = db.codes.find((c) => c.code === code);
  const refuse = (reasonCode: string, message: string) => ({
    code,
    applied: false,
    reasonCode,
    message,
    cents: 0,
  });
  if (!found) return refuse('DiscountCodeNotFound', "We don't recognise that code.");
  if (!found.isActive) return refuse('DiscountCodeInactive', 'That code is not active.');
  if (found.validFrom && Date.parse(found.validFrom) > Date.now())
    return refuse('DiscountCodeNotYetValid', "That code isn't valid yet.");
  if (found.validTo && Date.parse(found.validTo) < Date.now())
    return refuse('DiscountCodeExpired', `That code expired on ${found.validTo.slice(0, 10)}.`);
  if (found.usageLimit !== null && found.timesRedeemed >= found.usageLimit)
    return refuse('DiscountUsageLimitReached', 'That code has been used up.');
  if (found.firstOrderOnly && hasOrders)
    return refuse('DiscountFirstOrderOnly', 'That code is for first orders only.');
  if (found.minimumSpendCents !== null && goodsCents < found.minimumSpendCents)
    return refuse(
      'DiscountMinimumSpendNotMet',
      `Spend ${formatCents(found.minimumSpendCents)} or more to use ${found.code}.`,
    );
  const cents =
    found.kind === 'Percentage'
      ? percentOf(goodsCents, found.percent ?? 0)
      : Math.min(found.amountCents ?? 0, goodsCents);
  return { code, applied: true, reasonCode: null, message: null, cents };
}

export function computeQuote(lines: QuoteLineInput[], choices: QuoteChoicesInput): QuoteResult {
  const changes: Array<Record<string, unknown>> = [];
  const excluded: Array<Record<string, unknown>> = [];
  const blocking: string[] = [];
  const priced: PricedLine[] = [];
  const rate = db.taxRatePercent;

  for (const line of lines) {
    const info = skuInfo(line.skuId);
    const name = info?.productName ?? 'This item';
    const exclude = (kind: string, reasonCode: string, message: string) => {
      excluded.push({
        lineRef: line.lineId,
        skuId: line.skuId,
        productName: name,
        cat: catRef(line.catId),
        quantity: line.quantity,
        reasonCode,
      });
      changes.push({ kind, lineRef: line.lineId, productName: name, before: null, after: null, message });
    };
    if (!info || !info.purchasable || info.priceCents === null) {
      exclude('NowUnavailable', 'NowUnavailable', `'${name}' is no longer available and has been left out of this order.`);
      continue;
    }
    if (info.requiresPrescription && !line.catId) {
      exclude('PrescriptionCatRequired', 'PrescriptionCatRequired', `Choose which cat '${name}' is for before you check out.`);
      if (!blocking.includes('PrescriptionCatRequired')) blocking.push('PrescriptionCatRequired');
      continue;
    }
    if (info.requiresPrescription && line.catId && !authorisedCatsFor(line.skuId).includes(line.catId)) {
      exclude(
        'PrescriptionAuthorisationExpired',
        'PrescriptionAuthorisationExpired',
        `'${name}' needs a current veterinary authorisation for ${CAT_NAMES[line.catId] ?? 'your cat'}, and it is no longer valid. Book a consultation to renew it.`,
      );
      continue;
    }
    const sellable = stockOf(line.skuId);
    if (sellable <= 0) {
      exclude('NowUnavailable', 'NowUnavailable', `'${name}' has just sold out and has been left out of this order.`);
      continue;
    }
    const quantity = Math.min(line.quantity, sellable);
    if (quantity < line.quantity) {
      changes.push({
        kind: 'QuantityReduced',
        lineRef: line.lineId,
        productName: name,
        before: String(line.quantity),
        after: String(quantity),
        message: `Only ${quantity} of '${name}' ${quantity === 1 ? 'is' : 'are'} left. The quantity has been reduced.`,
      });
    }
    if (info.priceCents !== line.priceWhenAddedCents) {
      const up = info.priceCents > line.priceWhenAddedCents;
      changes.push({
        kind: up ? 'PriceIncreased' : 'PriceDecreased',
        lineRef: line.lineId,
        productName: name,
        before: money(line.priceWhenAddedCents).amount,
        after: money(info.priceCents).amount,
        message: `The price of '${name}' has ${up ? 'increased' : 'decreased'} from ${formatCents(line.priceWhenAddedCents)} to ${formatCents(info.priceCents)}.`,
      });
    }
    priced.push({
      lineRef: line.lineId,
      skuId: line.skuId,
      productName: name,
      variantLabel: info.variantLabel,
      catId: info.requiresPrescription ? line.catId : null,
      quantity,
      unitCents: info.priceCents,
      subtotalCents: info.priceCents * quantity,
      discountCents: 0,
      shareCents: 0,
      taxRatePercent: rate,
      taxCents: 0,
      totalCents: 0,
      returnClass: info.returnClass,
    });
  }

  if (lines.length === 0) blocking.unshift('CartEmpty');
  else if (priced.length === 0 && !blocking.includes('PrescriptionCatRequired')) blocking.unshift('NothingToPlace');

  const goods = priced.reduce((sum, l) => sum + l.subtotalCents, 0);

  // ---- discount -----
  let discount: ReturnType<typeof evaluateDiscount> | null = null;
  if (choices.discountCode?.trim()) {
    discount = evaluateDiscount(choices.discountCode, goods, choices.customerHasOrders);
  }
  const orderDiscount = discount?.applied ? discount.cents : 0;
  let assigned = 0;
  priced.forEach((l, index) => {
    const share =
      index === priced.length - 1
        ? orderDiscount - assigned
        : goods === 0
          ? 0
          : Math.floor((orderDiscount * l.subtotalCents) / goods);
    assigned += share;
    l.shareCents = share;
    const base = Math.max(l.subtotalCents - l.discountCents - l.shareCents, 0);
    l.taxCents = percentOf(base, l.taxRatePercent);
    l.totalCents = base + l.taxCents;
  });

  // ---- delivery -----
  const isDelivery = choices.fulfilment === 'Delivery';
  const isPickup = choices.fulfilment === 'Pickup';
  let zone: DbZone | null = null;
  let deliveryWire: Record<string, unknown> = {
    available: false,
    unavailableReason: null,
    zoneId: null,
    zoneName: null,
    serviceLevels: [],
  };
  let selectedLevel: string | null = null;
  let deliveryCharge = 0;
  const goodsValue = goods - orderDiscount;
  if (choices.fulfilment !== 'Counter') {
    if (choices.address === undefined) {
      if (isDelivery) blocking.push('DeliveryAddressNotFound');
    } else if (choices.address === null) {
      if (isDelivery) blocking.push('DeliveryAddressNotFound');
    } else {
      zone = findZone(choices.address);
      if (!zone) {
        deliveryWire = { ...deliveryWire, unavailableReason: 'AddressOutsideServedZones' };
        if (isDelivery) blocking.push('AddressOutsideServedZones');
      } else {
        const levels = zone.services
          .filter((s) => s.isActive)
          .map((s) => ({
            level: s.serviceLevel,
            charge: money(deliveryChargeFor(zone!, s.serviceLevel, goodsValue)),
            // The server's free-delivery threshold and what is still needed (never below zero); null with no threshold.
            freeOver: s.freeOverCents === null ? null : money(s.freeOverCents),
            amountToFreeDelivery:
              s.freeOverCents === null ? null : money(Math.max(0, s.freeOverCents - goodsValue)),
            leadTimeDays: s.leadTimeDays,
            slots: slotsFor(zone!, s.serviceLevel),
          }));
        deliveryWire = {
          available: true,
          unavailableReason: null,
          zoneId: zone.id,
          zoneName: zone.name,
          serviceLevels: levels,
        };
        if (isDelivery) {
          const wanted = choices.serviceLevel;
          const chosen = wanted
            ? levels.find((l) => l.level === wanted)
            : (levels.find((l) => l.level === 'Standard') ?? levels[0]);
          if (!chosen) blocking.push('ServiceLevelUnavailable');
          else {
            selectedLevel = chosen.level;
            deliveryCharge = priced.length === 0 ? 0 : deliveryChargeFor(zone, chosen.level, goodsValue);
            if (choices.slotDate) {
              const offered = chosen.slots.some(
                (s) => s.date === choices.slotDate && s.windowStart === choices.slotWindowStart,
              );
              if (!offered) blocking.push('DeliverySlotUnavailable');
            }
          }
        }
      }
    }
    if (isPickup && !PICKUP_BRANCHES.some((b) => b.id === choices.pickupBranchId)) {
      blocking.push('PickupBranchUnavailable');
    }
  }

  const lineTax = priced.reduce((sum, l) => sum + l.taxCents, 0);
  const deliveryTax = percentOf(deliveryCharge, db.taxRatePercent);
  const tax = lineTax + deliveryTax;
  const lineDiscount = priced.reduce((sum, l) => sum + l.discountCents, 0);
  const grand = goods - lineDiscount - orderDiscount + deliveryCharge + tax;

  // ---- payment -----
  let paymentOptions: Array<Record<string, unknown>> = [];
  if (choices.fulfilment !== 'Counter') {
    const ceiling = zone?.codMaxCents ?? 500_000;
    const cod =
      !isDelivery
        ? { available: false, reason: 'NotForPickup', message: 'Cash on delivery is for delivery orders only.' }
        : !zone || !zone.codEnabled
          ? { available: false, reason: 'CodNotOfferedInZone', message: 'Cash on delivery is not offered in your delivery area.' }
          : grand > ceiling
            ? { available: false, reason: 'CodAboveOrderLimit', message: `Cash on delivery is available for orders up to ${formatCents(ceiling)}.` }
            : { available: true, reason: null, message: null };
    const bank = db.bank.isActive && db.bank.configured
      ? { available: true, reason: null, message: null }
      : { available: false, reason: 'BankTransferDisabled', message: 'Bank transfer is not available right now.' };
    const collect = !isPickup
      ? { available: false, reason: 'NotForDelivery', message: 'Pay on collection is for pickup orders only.' }
      : { available: true, reason: null, message: null };
    paymentOptions = [
      { method: 'CashOnDelivery', available: cod.available, unavailableReason: cod.reason, message: cod.message },
      { method: 'BankTransfer', available: bank.available, unavailableReason: bank.reason, message: bank.message },
      { method: 'PayOnCollection', available: collect.available, unavailableReason: collect.reason, message: collect.message },
    ];
    const chosenOption = paymentOptions.find((o) => o.method === choices.paymentMethod);
    if (!chosenOption || chosenOption.available !== true) blocking.push('PaymentMethodUnavailable');
    if (db.pendingPolicies.length > 0) blocking.push('PolicyAcceptanceRequired');
  }

  const distinct = [...new Set(blocking)];
  const totals = {
    subtotal: goods,
    lineDiscount,
    orderDiscount,
    delivery: deliveryCharge,
    tax,
    grand,
  };

  const fingerprint = hash(
    JSON.stringify([
      choices.fulfilment,
      choices.address === undefined ? null : choices.address,
      zone?.id ?? null,
      selectedLevel,
      choices.slotDate ?? null,
      choices.slotWindowStart ?? null,
      choices.pickupBranchId ?? null,
      choices.paymentMethod ?? null,
      discount?.applied ? discount.code : null,
      priced.map((l) => [l.skuId, l.quantity, l.unitCents, l.catId]),
      totals,
    ]),
  );

  const wireLines = priced.map((l) => ({
    lineRef: l.lineRef,
    skuId: l.skuId,
    productName: l.productName,
    variantLabel: l.variantLabel,
    cat: catRef(l.catId),
    quantity: l.quantity,
    unitPrice: money(l.unitCents),
    lineSubtotal: money(l.subtotalCents),
    lineDiscount: money(l.discountCents),
    orderDiscountShare: money(l.shareCents),
    taxRatePercent: l.taxRatePercent,
    taxAmount: money(l.taxCents),
    lineTotal: money(l.totalCents),
  }));
  const totalsWire = {
    subtotal: money(totals.subtotal),
    lineDiscountTotal: money(totals.lineDiscount),
    orderDiscount: money(totals.orderDiscount),
    deliveryCharge: money(totals.delivery),
    taxTotal: money(totals.tax),
    grandTotal: money(totals.grand),
  };
  const discountWire = discount
    ? { code: discount.code, applied: discount.applied, reasonCode: discount.reasonCode, message: discount.message }
    : null;

  const wire: Record<string, unknown> =
    choices.fulfilment === 'Counter'
      ? {
          lines: wireLines,
          excludedLines: excluded,
          changes,
          discount: discountWire,
          totals: totalsWire,
          canSell: distinct.length === 0 && excluded.length === 0 && priced.length > 0,
          blockingReasons: distinct,
          currencyCode: 'BDT',
        }
      : {
          lines: wireLines,
          excludedLines: excluded,
          changes,
          delivery: deliveryWire,
          selectedServiceLevel: selectedLevel,
          pickupBranches: PICKUP_BRANCHES,
          paymentOptions,
          discount: discountWire,
          totals: totalsWire,
          pendingPolicies: db.pendingPolicies,
          canPlace: distinct.length === 0,
          blockingReasons: distinct,
          fingerprint,
          currencyCode: 'BDT',
        };

  return {
    wire,
    priced,
    totals,
    canPlace: distinct.length === 0,
    blocking: distinct,
    fingerprint,
    zone,
    level: selectedLevel,
    discountApplied: discount?.applied === true,
  };
}

export { MAX_QUANTITY_PER_LINE, ZONE };
