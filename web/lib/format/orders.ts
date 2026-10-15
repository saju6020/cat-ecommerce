/**
 * `lib/format/orders.ts` — HO-ORD-002 §8.4. Plain-language labels for the Ordering enums the API
 * sends, and branch-zone date helpers. **No arithmetic on money** and no business rule: a label is
 * what a server value is called on screen (status is text first, UI-6). Times are formatted with
 * `Intl` in the branch's IANA zone and name that zone (ADR-FE-015).
 */

const ORDER_STATUS_LABELS: Record<string, string> = {
  PendingPayment: 'Waiting for payment',
  Confirmed: 'Confirmed',
  Packing: 'Being packed',
  Dispatched: 'Dispatched',
  Delivered: 'Delivered',
  Cancelled: 'Cancelled',
  Returned: 'Returned',
  Refunded: 'Refunded',
};

export function orderStatusLabel(status: string): string {
  return ORDER_STATUS_LABELS[status] ?? status;
}

const FULFILMENT_LABELS: Record<string, string> = {
  Delivery: 'Delivery',
  Pickup: 'Collection from a branch',
  Counter: 'Counter sale',
};

export function fulfilmentLabel(method: string): string {
  return FULFILMENT_LABELS[method] ?? method;
}

const PAYMENT_METHOD_LABELS: Record<string, string> = {
  CashOnDelivery: 'Cash on delivery',
  BankTransfer: 'Bank transfer',
  PayOnCollection: 'Pay when you collect',
  InStore: 'Paid at the counter',
};

export function paymentMethodLabel(method: string): string {
  return PAYMENT_METHOD_LABELS[method] ?? method;
}

/** What happens next, by payment method (checkout confirmation). */
export function paymentNextStep(method: string, branchName?: string | null): string {
  switch (method) {
    case 'CashOnDelivery':
      return 'Pay the courier in cash when your order arrives.';
    case 'PayOnCollection':
      return `Collect and pay at ${branchName ?? 'the branch'}.`;
    case 'BankTransfer':
      return 'Transfer the amount to our bank account using the payment reference below.';
    default:
      return '';
  }
}

export function paymentMethodExplanation(method: string): string {
  switch (method) {
    case 'CashOnDelivery':
      return 'Pay the courier in cash when your order arrives. Nothing to pay now.';
    case 'BankTransfer':
      return "You'll get our bank details after you place the order, and a deadline to transfer by.";
    case 'PayOnCollection':
      return 'Pay at the branch when you collect your order. Nothing to pay now.';
    default:
      return '';
  }
}

const FLAG_LABELS: Record<string, string> = {
  AwaitingApproval: 'Awaiting approval',
  TransferReported: 'Transfer reported',
  CourierReportsDelivered: 'Courier says delivered',
  CollectionOverdue: 'Collection overdue',
  CustomerAnonymised: 'Customer anonymised',
};

export function orderFlagLabel(flag: string): string {
  return FLAG_LABELS[flag] ?? flag;
}

const RETURN_REASON_LABELS: Record<string, string> = {
  WrongItem: 'I received the wrong item',
  Damaged: 'It arrived damaged',
  QualityDefect: 'There is a quality problem',
  ChangedMind: 'I changed my mind',
  Recalled: 'It has been recalled',
  Other: 'Another reason',
};

export function returnReasonLabel(reason: string): string {
  return RETURN_REASON_LABELS[reason] ?? reason;
}

const CONDITION_LABELS: Record<string, string> = {
  Sealed: 'Sealed / unopened',
  Opened: 'Opened',
  Used: 'Used',
};

export function itemConditionLabel(condition: string): string {
  return CONDITION_LABELS[condition] ?? condition;
}

const RETURN_STATUS_LABELS: Record<string, string> = {
  Requested: 'Waiting for review',
  Approved: 'Approved',
  Rejected: 'Not approved',
  Received: 'Received',
  Closed: 'Closed',
};

export function returnStatusLabel(status: string): string {
  return RETURN_STATUS_LABELS[status] ?? status;
}

/** What staff do next with a return, by status (returns list). */
export function returnNextStep(status: string): string {
  switch (status) {
    case 'Requested':
      return 'Review and approve or reject';
    case 'Approved':
      return 'Receive the goods when they arrive';
    case 'Received':
      return 'Goods are in quarantine — issue any refund due';
    default:
      return 'Nothing more to do';
  }
}

export function returnKindLabel(kind: string): string {
  return kind === 'DeliveryFailure' ? 'Undelivered parcel' : 'Customer return';
}

const REFUND_METHOD_LABELS: Record<string, string> = {
  CashInStore: 'Cash in store',
  BankTransfer: 'Bank transfer',
  StoreCredit: 'Store credit',
};

export function refundMethodLabel(method: string): string {
  return REFUND_METHOD_LABELS[method] ?? method;
}

export function tenderMethodLabel(method: string): string {
  return method === 'InStoreCard' ? 'Card in store' : method === 'Cash' ? 'Cash' : method;
}

const RECONCILIATION_KIND_LABELS: Record<string, string> = {
  TransferConfirmationFailed: 'Transfer confirmation failed',
  PaymentApprovalFailed: 'Payment approval failed',
  DeliveryPaymentFailed: 'Delivery payment failed',
  CounterSaleFailed: 'Counter sale failed',
  RefundFailed: 'Refund failed',
};

export function reconciliationKindLabel(kind: string): string {
  return RECONCILIATION_KIND_LABELS[kind] ?? kind;
}

const DELIVERY_FAILURE_LABELS: Record<string, string> = {
  Refused: 'The customer refused the parcel',
  Undeliverable: 'The parcel could not be delivered',
  Uncollected: 'The customer did not collect it',
};

export function deliveryFailureLabel(reason: string): string {
  return DELIVERY_FAILURE_LABELS[reason] ?? reason;
}

export function serviceLevelLabel(level: string): string {
  return level === 'Express' ? 'Express delivery' : level === 'Standard' ? 'Standard delivery' : level;
}

const ACTION_LABELS: Record<string, string> = {
  ConfirmPayment: 'Confirm transfer',
  Cancel: 'Cancel order',
  StartPacking: 'Start packing',
  Dispatch: 'Dispatch',
  MarkReadyForCollection: 'Ready for collection',
  RecordDelivery: 'Record delivery',
  RecordDeliveryFailure: 'Record delivery failure',
  Refund: 'Issue refund',
};

export function orderActionLabel(action: string, fulfilment?: string): string {
  if (action === 'RecordDelivery' && fulfilment === 'Pickup') return 'Record collection';
  if (action === 'RecordDeliveryFailure' && fulfilment === 'Pickup') return 'Record not collected';
  return ACTION_LABELS[action] ?? action;
}

// ---- dates -------------------------------------------------------------------------------------------

/** `Mon 12 Oct, 10:30` in the branch zone (the zone is named by the caller's heading or `withZone`). */
export function formatOrderWhen(instant: string, zone: string, withZone = false): string {
  const date = new Date(instant);
  if (Number.isNaN(date.getTime())) return '—';
  const text = new Intl.DateTimeFormat('en-GB', {
    weekday: 'short',
    day: 'numeric',
    month: 'short',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
    timeZone: zone,
  })
    .format(date)
    .replace(',', '');
  const [weekday = '', ...rest] = text.split(' ');
  const body = rest.join(' ');
  // en-GB writes "Mon 12 Oct 10:30"; put the comma the design uses before the time.
  const lastSpace = body.lastIndexOf(' ');
  const day = lastSpace === -1 ? body : body.slice(0, lastSpace);
  const time = lastSpace === -1 ? '' : body.slice(lastSpace + 1);
  const label = time ? `${weekday} ${day}, ${time}` : `${weekday} ${day}`;
  return withZone ? `${label} (${zone})` : label;
}

/** `Mon 12 Oct, 10:00–13:00 (Asia/Dhaka)` for a delivery window already expressed in branch time. */
export function formatDeliverySlot(
  date: string,
  windowStart: string,
  windowEnd: string,
  zone: string,
): string {
  const [year, month, day] = date.split('-');
  if (!year || !month || !day) return date;
  const asUtc = new Date(Date.UTC(Number(year), Number(month) - 1, Number(day)));
  const label = new Intl.DateTimeFormat('en-GB', {
    weekday: 'short',
    day: 'numeric',
    month: 'short',
    timeZone: 'UTC',
  })
    .format(asUtc)
    .replace(',', '');
  return `${label}, ${windowStart}–${windowEnd} (${zone})`;
}

const HOUR_MS = 3_600_000;

/** `47 h 12 min`, `12 min`, `45 s` — a coarse time left for a payment window (not a ticking clock). */
export function formatTimeLeft(remainingMs: number): string {
  if (remainingMs <= 0) return 'now';
  const minutesLeft = Math.floor(remainingMs / 60_000);
  if (minutesLeft < 1) return `${Math.max(1, Math.ceil(remainingMs / 1000))} s`;
  const hours = Math.floor(minutesLeft / 60);
  const minutes = minutesLeft % 60;
  if (hours === 0) return `${minutes} min`;
  return `${hours} h ${String(minutes).padStart(2, '0')} min`;
}

export type CountdownMilestone = 'six-hours' | 'one-hour' | 'expired';

/**
 * UI-6: a countdown announces at 6 h, 1 h and expiry only, never continuously. Returns the
 * milestone `remainingMs` has reached (the nearest one passed), or `null` while more than 6 h remain.
 */
export function countdownMilestone(remainingMs: number): CountdownMilestone | null {
  if (remainingMs <= 0) return 'expired';
  if (remainingMs <= HOUR_MS) return 'one-hour';
  if (remainingMs <= 6 * HOUR_MS) return 'six-hours';
  return null;
}

export function milestoneAnnouncement(milestone: CountdownMilestone): string {
  switch (milestone) {
    case 'six-hours':
      return 'About 6 hours left to make the transfer.';
    case 'one-hour':
      return 'About 1 hour left to make the transfer.';
    case 'expired':
      return 'The time to make the transfer has passed.';
  }
}

/** `Mon 12 Oct` for a local calendar date (`yyyy-MM-dd`), never zone-converted. */
export function formatSlotDay(date: string): string {
  const [year, month, day] = date.split('-');
  if (!year || !month || !day) return date;
  return new Intl.DateTimeFormat('en-GB', {
    weekday: 'short',
    day: 'numeric',
    month: 'short',
    timeZone: 'UTC',
  })
    .format(new Date(Date.UTC(Number(year), Number(month) - 1, Number(day))))
    .replace(',', '');
}

// ---- branch-local date-times ---------------------------------------------------------------------------

function zoneOffsetMs(zone: string, at: Date): number {
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone: zone,
    hourCycle: 'h23',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  }).formatToParts(at);
  const pick = (type: string) => Number(parts.find((p) => p.type === type)?.value ?? 0);
  const asIfUtc = Date.UTC(pick('year'), pick('month') - 1, pick('day'), pick('hour'), pick('minute'), pick('second'));
  return asIfUtc - (at.getTime() - at.getMilliseconds());
}

/** Now as a `yyyy-MM-ddTHH:mm` wall-clock value in the branch's zone (a `datetime-local` default). */
export function nowInBranchLocal(zone: string): string {
  const at = new Date();
  return new Date(at.getTime() + zoneOffsetMs(zone, at)).toISOString().slice(0, 16);
}

/** A `yyyy-MM-ddTHH:mm` wall-clock value read in the branch's zone -> the UTC instant (ISO). */
export function branchLocalToInstant(local: string, zone: string): string {
  const [date = '', time = '00:00'] = local.split('T');
  const [year, month, day] = date.split('-').map(Number);
  const [hour, minute] = time.split(':').map(Number);
  const guess = Date.UTC(year ?? 1970, (month ?? 1) - 1, day ?? 1, hour ?? 0, minute ?? 0);
  const first = zoneOffsetMs(zone, new Date(guess));
  let instant = guess - first;
  const second = zoneOffsetMs(zone, new Date(instant));
  if (second !== first) instant = guess - second;
  return new Date(instant).toISOString();
}

/**
 * A calendar-day filter (`yyyy-MM-dd`, what a date input gives) -> the instants the order queue needs: the
 * start of the `from` day and the last millisecond of the `to` day, both read in the BRANCH's zone. Sent as a
 * bare date the server would bind it as midnight UTC, which drops the whole `to` day.
 */
export function dayStartInstant(day: string, zone: string): string {
  return branchLocalToInstant(`${day}T00:00`, zone);
}

export function dayEndInstant(day: string, zone: string): string {
  const [year = 1970, month = 1, dayOfMonth = 1] = day.split('-').map(Number);
  const next = new Date(Date.UTC(year, month - 1, dayOfMonth + 1)).toISOString().slice(0, 10);
  return new Date(Date.parse(branchLocalToInstant(`${next}T00:00`, zone)) - 1).toISOString();
}

/** `2 h 05 min`, `35 min`, `3 d 4 h` — how long an order has been waiting (whole minutes from the server). */
export function formatAgeMinutes(minutes: number): string {
  if (minutes < 60) return `${minutes} min`;
  const hours = Math.floor(minutes / 60);
  if (hours < 48) return `${hours} h ${String(minutes % 60).padStart(2, '0')} min`;
  return `${Math.floor(hours / 24)} d ${hours % 24} h`;
}

/** A sort key for an age column that compares as text (the loaded page is sorted client-side). */
export function ageSortKey(minutes: number): string {
  return String(minutes).padStart(9, '0');
}

/** An instant (ISO) as a `yyyy-MM-ddTHH:mm` wall-clock value in the branch's zone (a `datetime-local` value). */
export function instantToBranchLocal(instant: string, zone: string): string {
  const at = new Date(instant);
  if (Number.isNaN(at.getTime())) return '';
  return new Date(at.getTime() + zoneOffsetMs(zone, at)).toISOString().slice(0, 16);
}
