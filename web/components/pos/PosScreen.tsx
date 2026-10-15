'use client';

import { useSearchParams } from 'next/navigation';
import { useEffect, useRef, useState } from 'react';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { PageHeader, Section } from '@/components/billing/PageHeader';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { OrderSummary } from '@/components/orders/OrderSummary';
import { Button } from '@/components/ui/button';
import { ApiValidationError } from '@/lib/api/client';
import { describeBlockingReason, describeOrderingError } from '@/lib/api/errors';
import type { CounterSale, PosSku } from '@/lib/api/pos';
import { formatMoney } from '@/lib/format/money';
import { tenderMethodLabel } from '@/lib/format/orders';
import { useIdempotencyKey } from '@/lib/hooks/useIdempotencyKey';
import { useCompleteCounterSale, useCounterQuote, usePosCustomerCats } from '@/lib/hooks/usePos';
import { useStaffCustomer } from '@/lib/hooks/useStaffCustomers';
import { PosBasket } from './PosBasket';
import { PosCustomerPanel } from './PosCustomerPanel';
import { PosReviewDialog } from './PosReviewDialog';
import { PosSaleComplete } from './PosSaleComplete';
import { PosScanField } from './PosScanField';
import { PosTenderPanel } from './PosTenderPanel';
import { basketLineFromSku, type BasketLine, type PosCustomer, type PosTender } from './types';

/**
 * `components/pos/PosScreen.tsx` — ⭐ HO-ORD-002 §11.11, US-ORD-16. The counter sale. The basket, the
 * customer, the payments and the scan buffer are **this component's state** (never storage); the totals
 * come from the server's counter quote (debounced 300 ms) and the change from the sale response. Scan
 * returns focus here after each item; `F2` jumps to the payment amount; Enter adds the payment and opens
 * a review whose **Complete sale** is focused. One idempotency key per attempt, kept while the payload
 * is unchanged, so a double Enter or a retry after a lost response records one sale.
 */
const EMPTY_CUSTOMER: PosCustomer = { name: '', mobile: '', customerId: null };

export function PosScreen() {
  const searchParams = useSearchParams();
  const presetCustomerId = searchParams.get('customerId') ?? '';
  const [lines, setLinesState] = useState<BasketLine[]>([]);
  // The basket is also held in a ref that is updated synchronously, so two scans arriving between renders each
  // see the other's line (a closure over `lines` would make both set the same quantity).
  const linesRef = useRef<BasketLine[]>([]);
  const setLines = (update: BasketLine[] | ((current: BasketLine[]) => BasketLine[])) => {
    const next = typeof update === 'function' ? update(linesRef.current) : update;
    linesRef.current = next;
    setLinesState(next);
  };
  const [customer, setCustomer] = useState<PosCustomer>(EMPTY_CUSTOMER);
  const [tenders, setTenders] = useState<PosTender[]>([]);
  const [discountCode, setDiscountCode] = useState('');
  const [sale, setSale] = useState<CounterSale | null>(null);
  const [reviewOpen, setReviewOpen] = useState(false);
  const [voidOpen, setVoidOpen] = useState(false);
  const [rootError, setRootError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<{ name?: string; mobile?: string }>({});
  const scanRef = useRef<HTMLInputElement>(null);
  const amountRef = useRef<HTMLInputElement>(null);
  const key = useIdempotencyKey();
  const complete = useCompleteCounterSale();

  const preset = useStaffCustomer(presetCustomerId);
  useEffect(() => {
    if (preset.data && !customer.customerId && customer.name === '' && customer.mobile === '') {
      setCustomer({ name: preset.data.fullName, mobile: preset.data.mobileNumber ?? '', customerId: preset.data.id });
    }
  }, [preset.data, customer]);

  const quoteLines = lines.map((l) => ({ skuId: l.skuId, quantity: l.quantity, catId: l.catId ?? undefined }));
  const quote = useCounterQuote({ customerId: customer.customerId ?? undefined, lines: quoteLines, discountCode: discountCode.trim() || undefined });
  const cats = usePosCustomerCats(customer.customerId);
  const currency = quote.data?.currencyCode ?? 'BDT';

  const missingCat = lines.some((l) => l.requiresPrescription && !l.catId);
  // Settled = the quote on screen is for the basket on screen (not the debounce window, not a refetch in flight,
  // not the previous basket's quote kept as placeholder data).
  const settled = !quote.isSettling && !quote.isFetching && !quote.isPlaceholderData;
  const ready = lines.length > 0 && tenders.length > 0 && !!quote.data?.canSell && settled && !missingCat && customer.name.trim() !== '' && customer.mobile.trim() !== '';
  // Enter in the amount field adds the payment and asks for the review. The payment lands in state a render
  // later, so the request waits here until the quote has settled, then opens the review only if the sale is ready.
  const [reviewRequested, setReviewRequested] = useState(false);
  useEffect(() => {
    if (!reviewRequested || !settled) return;
    setReviewRequested(false);
    if (ready) setReviewOpen(true);
  }, [reviewRequested, settled, ready]);

  function addSku(sku: PosSku): string {
    const existing = linesRef.current.find((l) => l.skuId === sku.skuId);
    const name = `${sku.productName} ${sku.variantLabel}`.trim();
    const quantity = existing ? existing.quantity + 1 : 1;
    setLines((cur) => (existing ? cur.map((l) => (l.skuId === sku.skuId ? { ...l, quantity } : l)) : [...cur, basketLineFromSku(sku)]));
    scanRef.current?.focus();
    return `Added ${name}, quantity ${quantity}`;
  }

  function payload() {
    return {
      customerName: customer.name.trim(),
      customerMobile: customer.mobile.trim(),
      lines: quoteLines,
      discountCode: discountCode.trim() || undefined,
      tenders: tenders.map((t) => ({ method: t.method, amount: t.amount, reference: t.reference || undefined })),
    };
  }

  async function send() {
    setRootError(null);
    setFieldErrors({});
    const request = payload();
    try {
      const result = await complete.mutateAsync({ sale: request, idempotencyKey: key.keyFor(JSON.stringify(request)) });
      key.renew();
      setReviewOpen(false);
      setSale(result);
    } catch (error) {
      // A definitive refusal ends this key: the API replays it for an unchanged body. In-progress / network keep it.
      key.settleFailure(error);
      setReviewOpen(false);
      const perField: { name?: string; mobile?: string } = {};
      const other: string[] = [];
      if (error instanceof ApiValidationError) {
        for (const v of error.response.errors) {
          if (/customername/i.test(v.propertyName)) perField.name = v.errorMessage;
          else if (/customermobile/i.test(v.propertyName) && v.errorCode !== 'AccountNotActive') perField.mobile = v.errorMessage;
          else other.push(v.errorMessage);
        }
      }
      setFieldErrors(perField);
      setRootError(other.length > 0 ? other.join(' ') : Object.keys(perField).length > 0 ? 'Check the customer’s details.' : describeOrderingError(error));
    }
  }

  function reset() {
    setLines([]);
    setCustomer(EMPTY_CUSTOMER);
    setTenders([]);
    setDiscountCode('');
    setSale(null);
    setRootError(null);
    setFieldErrors({});
    key.renew();
    setTimeout(() => scanRef.current?.focus(), 0);
  }

  const reviewText = quote.data
    ? `Total ${formatMoney(quote.data.totals.grandTotal)}. Payments: ${tenders.map((t) => `${tenderMethodLabel(t.method)} ${formatMoney({ amount: t.amount, currencyCode: currency })}`).join(', ')}.`
    : '';

  if (sale) {
    return (
      <div className="flex flex-col gap-5">
        <PageHeader title="Point of sale" />
        <PosSaleComplete sale={sale} onNewSale={reset} />
      </div>
    );
  }

  return (
    <div
      className="flex flex-col gap-5"
      onKeyDown={(event) => {
        if (event.key === 'F2') {
          event.preventDefault();
          amountRef.current?.focus();
        }
      }}
    >
      <UnsavedChangesGuard when={lines.length > 0} />
      <PageHeader
        title="Point of sale"
        description="Scan items, take payment, print the receipt."
        actions={
          <Button variant="secondary" size="sm" disabled={lines.length === 0} onClick={() => setVoidOpen(true)}>
            Void basket
          </Button>
        }
      />
      <div className="grid gap-5 lg:grid-cols-[minmax(0,3fr)_minmax(0,2fr)]">
        <div className="flex min-w-0 flex-col gap-5">
          <PosScanField ref={scanRef} onAdd={addSku} />
          <Section title="Basket">
            <PosBasket
              lines={lines}
              quote={quote.data}
              cats={cats.data?.items ?? []}
              hasCustomer={!!customer.customerId}
              onQuantity={(skuId, quantity) => setLines((cur) => cur.map((l) => (l.skuId === skuId ? { ...l, quantity } : l)))}
              onRemove={(skuId) => setLines((cur) => cur.filter((l) => l.skuId !== skuId))}
              onCat={(skuId, catId) => setLines((cur) => cur.map((l) => (l.skuId === skuId ? { ...l, catId: catId || null } : l)))}
            />
          </Section>
          <PosCustomerPanel customer={customer} onChange={setCustomer} errors={fieldErrors} />
        </div>
        <div className="flex min-w-0 flex-col gap-5">
          <Section title="Summary">
            {quote.data ? (
              <OrderSummary totals={quote.data.totals} fulfilment="Counter" heading="Sale total" discountCode={discountCode.trim().toUpperCase() || null} />
            ) : (
              <p className="text-sm text-ink-muted">{lines.length === 0 ? 'Totals appear when you scan an item.' : 'Working out the total…'}</p>
            )}
            {quote.data && quote.data.blockingReasons.length > 0 && (
              <ul aria-label="Why this sale can’t be completed" className="m-0 list-none rounded-[var(--radius-sm)] bg-danger-bg p-3 text-sm font-semibold text-danger-ink">
                {quote.data.blockingReasons.map((code) => (
                  <li key={code}>{describeBlockingReason(code)}</li>
                ))}
              </ul>
            )}
            <div className="flex flex-col gap-1">
              <label htmlFor="pos-discount" className="text-sm font-semibold">Discount code (optional)</label>
              <input id="pos-discount" value={discountCode} onChange={(e) => setDiscountCode(e.target.value.toUpperCase())} maxLength={40} className="w-full rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink" />
            </div>
          </Section>
          <PosTenderPanel
            ref={amountRef}
            tenders={tenders}
            currencyCode={currency}
            disabled={lines.length === 0}
            onAdd={(t) => setTenders((cur) => [...cur, { ...t, id: crypto.randomUUID() }])}
            onRemove={(id) => setTenders((cur) => cur.filter((t) => t.id !== id))}
            onReview={() => setReviewRequested(true)}
          />
          {rootError && <p role="alert" className="rounded-[var(--radius-sm)] bg-danger-bg p-3 text-sm font-semibold text-danger-ink">{rootError}</p>}
          <Button size="md" disabled={!ready || complete.isPending} onClick={() => setReviewOpen(true)}>
            Complete sale
          </Button>
          {!ready && lines.length > 0 && (
            <p className="text-sm text-ink-muted">
              {missingCat ? 'Choose a cat for the prescription item. ' : ''}
              {customer.name.trim() === '' || customer.mobile.trim() === '' ? 'Enter the customer’s name and mobile. ' : ''}
              {tenders.length === 0 ? 'Add a payment.' : ''}
            </p>
          )}
        </div>
      </div>
      <PosReviewDialog open={reviewOpen} onOpenChange={setReviewOpen} summary={reviewText} pending={complete.isPending} onConfirm={() => void send()} />
      <ConfirmDialog
        open={voidOpen}
        onOpenChange={setVoidOpen}
        title="Void this basket"
        consequence={`All ${lines.length} item${lines.length === 1 ? '' : 's'} are removed and the payments entered are cleared. Nothing is recorded.`}
        confirmLabel="Void basket"
        variant="destructive"
        onConfirm={() => { reset(); setVoidOpen(false); }}
      />
    </div>
  );
}
