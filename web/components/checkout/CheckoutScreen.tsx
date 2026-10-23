'use client';

import Link from 'next/link';
import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { useRouter } from 'next/navigation';
import { useQueryClient } from '@tanstack/react-query';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { GreetingHeader } from '@/components/hearth/GreetingHeader';
import { HearthEmpty } from '@/components/hearth/HearthEmpty';
import { PillSteps, type PillStep } from '@/components/hearth/PillSteps';
import { summaryCardClass } from '@/components/hearth/card';
import { OrderSummary } from '@/components/orders/OrderSummary';
import { buttonVariants } from '@/components/ui/button';
import type { CheckoutChoices, PlaceOrderRequest } from '@/lib/api/checkout';
import { describeOrderingError, orderingViolationCodes } from '@/lib/api/errors';
import {
  formatDeliverySlot,
  paymentMethodExplanation,
  paymentMethodLabel,
  serviceLevelLabel,
} from '@/lib/format/orders';
import { useDeliveryAddresses, useCustomerProfile } from '@/lib/hooks/useAccount';
import { useAddDeliveryAddress } from '@/lib/hooks/useAccountMutations';
import { useCart, useCartCats, useUpdateCartLine } from '@/lib/hooks/useCart';
import { firstNameOf, useCustomerFirstName } from '@/lib/hooks/useCustomerFirstName';
import {
  CHECKOUT_STEPS,
  useCheckoutQuote,
  useCheckoutUrlState,
  usePlaceOrder,
  type CheckoutStep,
} from '@/lib/hooks/useCheckout';
import { useIdempotencyKey } from '@/lib/hooks/useIdempotencyKey';
import { usePublicBranches } from '@/lib/hooks/useMyOrders';
import { useAcceptPolicyVersion } from '@/lib/hooks/usePublicContent';
import { ChangesPanel } from './ChangesPanel';
import { DiscountCodeField } from './DiscountCodeField';
import { FulfilmentStep } from './FulfilmentStep';
import { PaymentStep } from './PaymentStep';
import { PendingPoliciesPanel } from './PendingPoliciesPanel';
import { PrescriptionLinesPanel } from './PrescriptionLinesPanel';
import { ReviewChoices } from './ReviewChoices';
import { ReviewStep } from './ReviewStep';
import { SlotStep, leadTimeText } from './SlotStep';
import type { RenderSummary } from './StepLayout';

/** Until the branches load (or if they cannot): the clinic's own zone. */
const FALLBACK_ZONE = 'Asia/Dhaka';
/** How long Place order stays disabled after `409 RequestInProgress` before the customer may press it again. */
const IN_PROGRESS_RECHECK_MS = 4000;
const STEP_TITLES: Record<CheckoutStep, string> = {
  fulfilment: 'Delivery or collection',
  slot: 'Delivery time',
  payment: 'Payment',
  review: 'Review and place your order',
};

type StepStatus = 'current' | 'done' | 'todo' | 'skipped';

interface StepDescriptor {
  id: CheckoutStep;
  title: string;
  status: StepStatus;
  /** One line shown once the step has been passed (and for a skipped step, why). */
  summary: string | null;
  body: ReactNode;
}

/**
 * `components/checkout/CheckoutScreen.tsx` — ⭐ HO-ORD-002 §11.2, ADR-FE-090/091/097. One route, four
 * addressable steps, **every choice in the URL**, and **the quote as the single source of truth** for
 * prices, charges, slots, payment availability and whether the order can be placed. The screen sends
 * the choices and the quote's fingerprint back and renders what it is told; it adds nothing up. Place
 * order is single-shot (one idempotency key per attempt, disabled while pending, no retry); a changed
 * quote re-opens the acknowledgement.
 */
export function CheckoutScreen() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { state, update } = useCheckoutUrlState();
  const cart = useCart();
  // The quote carries no images; the review shows each line's photo from the cart it was built from.
  const imageBySku = useMemo(
    () => new Map((cart.data?.lines ?? []).map((line) => [line.skuId, line.imagePath] as const)),
    [cart.data],
  );
  const addresses = useDeliveryAddresses();
  const profile = useCustomerProfile();
  const addAddress = useAddDeliveryAddress();
  const cats = useCartCats(true);
  const acceptPolicy = useAcceptPolicyVersion();
  const updateLine = useUpdateCartLine();
  const place = usePlaceOrder();
  const [placedOrderId, setPlacedOrderId] = useState<string | null>(null);
  const branches = usePublicBranches();
  // Delivery windows are the branch's: label them in its zone, not a hard-coded one.
  const zone = branches.data?.[0]?.timeZoneId ?? FALLBACK_ZONE;
  const idempotency = useIdempotencyKey();
  const sessionFirstName = useCustomerFirstName();
  const firstName = sessionFirstName ?? firstNameOf(profile.data?.fullName);

  const [recipient, setRecipient] = useState<{ name: string; phone: string } | null>(null);
  const [acknowledged, setAcknowledged] = useState<string | null>(null);
  const [placeError, setPlaceError] = useState<string | null>(null);
  const [stepErrors, setStepErrors] = useState<Partial<Record<CheckoutStep, string>>>({});
  const [inProgress, setInProgress] = useState(false);
  const [policyError, setPolicyError] = useState<string | null>(null);
  const [acceptingKey, setAcceptingKey] = useState<string | null>(null);
  const [rxError, setRxError] = useState<string | null>(null);
  const [rxPending, setRxPending] = useState<string | null>(null);
  const submitting = useRef(false);

  // 409 RequestInProgress: the first attempt is still running. Offer a way to its result (the link in the review
  // step) and, after a short wait, allow Place order again - with the SAME key, so the API replays that result.
  useEffect(() => {
    if (!inProgress) return undefined;
    const timer = setTimeout(() => {
      setInProgress(false);
      setPlaceError(null);
      void queryClient.invalidateQueries({ queryKey: ['me', 'orders'] });
    }, IN_PROGRESS_RECHECK_MS);
    return () => clearTimeout(timer);
  }, [inProgress, queryClient]);
  const switchedFor = useRef<string | null>(null);

  const hasLines = (cart.data?.lines.length ?? 0) > 0;
  const choices: CheckoutChoices = {
    fulfilment: state.fulfilment,
    addressId: state.addressId,
    serviceLevel: state.serviceLevel,
    slotDate: state.slotDate,
    slotWindowStart: state.slotWindowStart,
    pickupBranchId: state.pickupBranchId,
    paymentMethod: state.paymentMethod,
    discountCode: state.discountCode,
  };
  const quoteQuery = useCheckoutQuote(choices, hasLines);
  const quote = quoteQuery.data;
  const settling = quoteQuery.isSettling || quoteQuery.isPlaceholderData || quoteQuery.isFetching;

  // Recipient defaults from the profile, editable.
  useEffect(() => {
    if (recipient === null && profile.data) {
      setRecipient({ name: profile.data.fullName ?? '', phone: profile.data.mobileNumber ?? '' });
    }
  }, [profile.data, recipient]);

  const addressList = useMemo(() => addresses.data?.addresses ?? [], [addresses.data]);
  // Default delivery address.
  useEffect(() => {
    if (state.fulfilment === 'Delivery' && !state.addressId && addressList.length > 0) {
      update({ addressId: (addressList.find((a) => a.isDefault) ?? addressList[0])!.id });
    }
  }, [state.fulfilment, state.addressId, addressList, update]);

  // Default pickup branch.
  useEffect(() => {
    if (
      state.fulfilment === 'Pickup' &&
      !state.pickupBranchId &&
      quote &&
      quote.pickupBranches.length > 0
    ) {
      update({ pickupBranchId: quote.pickupBranches[0]!.id });
    }
  }, [state.fulfilment, state.pickupBranchId, quote, update]);

  // US-ORD-06 AC2: outside every zone -> collection is preselected (once per address).
  const outsideZone =
    quote?.delivery.available === false &&
    quote.delivery.unavailableReason === 'AddressOutsideServedZones';
  useEffect(() => {
    if (
      state.fulfilment === 'Delivery' &&
      outsideZone &&
      state.addressId &&
      switchedFor.current !== state.addressId
    ) {
      switchedFor.current = state.addressId;
      update({ fulfilment: 'Pickup', pickupBranchId: undefined, step: 'fulfilment' });
    }
  }, [outsideZone, state.fulfilment, state.addressId, update]);

  const levels = quote?.delivery.serviceLevels ?? [];
  const effectiveLevel = state.serviceLevel ?? quote?.selectedServiceLevel ?? null;
  const chosenLevel = levels.find((level) => level.level === effectiveLevel);
  const slotSkipped =
    state.fulfilment === 'Pickup' || (levels.length <= 1 && (levels[0]?.slots.length ?? 0) === 0);
  const deliveryTaxUnavailable =
    quote?.blockingReasons.includes('DeliveryTaxNotConfigured') ?? false;
  const selectedPayment = quote?.paymentOptions.find(
    (option) => option.method === state.paymentMethod,
  );

  const fulfilmentIncomplete =
    state.fulfilment === 'Delivery'
      ? !state.addressId || !quote?.delivery.available || deliveryTaxUnavailable
      : !state.pickupBranchId;
  const slotIncomplete =
    !slotSkipped &&
    state.fulfilment === 'Delivery' &&
    (!chosenLevel || (chosenLevel.slots.length > 0 && !state.slotDate));
  const paymentIncomplete = !selectedPayment || !selectedPayment.available;

  const incompleteStep: CheckoutStep | null = fulfilmentIncomplete
    ? 'fulfilment'
    : slotIncomplete
      ? 'slot'
      : paymentIncomplete
        ? 'payment'
        : null;
  const order = CHECKOUT_STEPS;
  const requested: CheckoutStep = state.step === 'slot' && slotSkipped ? 'payment' : state.step;
  const currentStep: CheckoutStep =
    quote && incompleteStep && order.indexOf(requested) > order.indexOf(incompleteStep)
      ? incompleteStep
      : requested;

  const goTo = useCallback((step: CheckoutStep) => update({ step }), [update]);

  const handlePlaceError = (error: unknown) => {
    const codes = orderingViolationCodes(error);
    const message = describeOrderingError(error);
    // Any definitive refusal ends this key (the API replays it for an unchanged body); RequestInProgress keeps it.
    idempotency.settleFailure(error);
    if (codes.includes('CheckoutQuoteChanged') || codes.includes('StockNoLongerAvailable')) {
      setAcknowledged(null);
      void queryClient.invalidateQueries({ queryKey: ['checkout', 'quote'] });
      void queryClient.invalidateQueries({ queryKey: ['cart'] });
      goTo('review');
      setPlaceError(message);
    } else if (codes.includes('RequestInProgress')) {
      setInProgress(true);
      setPlaceError(message);
      void queryClient.invalidateQueries({ queryKey: ['me', 'orders'] });
    } else if (codes.includes('PaymentMethodUnavailable')) {
      setStepErrors((current) => ({ ...current, payment: message }));
      goTo('payment');
    } else if (
      codes.some((c) =>
        [
          'DeliveryTaxNotConfigured',
          'AddressOutsideServedZones',
          'DeliveryAddressNotFound',
          'PickupBranchUnavailable',
          'RecipientPhoneRequired',
        ].includes(c),
      )
    ) {
      setStepErrors((current) => ({ ...current, fulfilment: message }));
      goTo('fulfilment');
    } else if (
      codes.some((c) => ['ServiceLevelUnavailable', 'DeliverySlotUnavailable'].includes(c))
    ) {
      setStepErrors((current) => ({ ...current, slot: message }));
      goTo('slot');
    } else if (codes.includes('PolicyAcceptanceRequired')) {
      setPolicyError(message);
      // The quote now lists the policies still to accept; without a refetch the panel to accept them is empty.
      void queryClient.invalidateQueries({ queryKey: ['checkout', 'quote'] });
      goTo('review');
    } else if (codes.includes('PrescriptionCatRequired')) {
      setRxError(message);
      void queryClient.invalidateQueries({ queryKey: ['checkout', 'quote'] });
      void queryClient.invalidateQueries({ queryKey: ['cart'] });
      goTo('review');
    } else {
      setPlaceError(message);
    }
  };

  async function handlePlace() {
    if (!quote || submitting.current) return;
    submitting.current = true;
    setPlaceError(null);
    const request: PlaceOrderRequest = {
      ...choices,
      serviceLevel:
        state.fulfilment === 'Delivery'
          ? (quote.selectedServiceLevel ?? choices.serviceLevel)
          : undefined,
      discountCode: quote.discount?.applied ? quote.discount.code : undefined,
      recipientName: recipient?.name.trim() || undefined,
      recipientPhone: recipient?.phone.trim() || undefined,
      quoteFingerprint: quote.fingerprint,
    };
    try {
      const placed = await place.mutateAsync({
        request,
        idempotencyKey: idempotency.keyFor(JSON.stringify(request)),
      });
      idempotency.renew();
      // Owner, 2026-10-19: straight to the order's details page (it shows the transfer instructions when the order
      // waits for one). `placedOrderId` holds this screen on "Opening your order…" meanwhile: placing the order empties
      // the cart, and the refetched cart would otherwise show "Your cart is empty" before the page changes.
      setPlacedOrderId(placed.orderId);
      router.push(`/portal/orders/${encodeURIComponent(placed.orderId)}?placed=1`);
    } catch (error) {
      handlePlaceError(error);
    } finally {
      submitting.current = false;
    }
  }

  if (placedOrderId) return <LoadingState rows={3} label="Opening your order…" />;
  if (cart.isPending) return <LoadingState rows={5} label="Loading your checkout…" />;
  if (cart.isError) {
    return (
      <ErrorState
        title="We couldn't load your cart."
        description="Your items are safe — please try again."
        headingLevel={2}
        onRetry={() => void cart.refetch()}
      />
    );
  }
  if (!hasLines) {
    return (
      <HearthEmpty
        title="Your cart is empty."
        description="Add something to your cart before checking out."
        action={
          <Link href="/shop" className={buttonVariants({ variant: 'pill', size: 'pillLg' })}>
            Browse the shop
          </Link>
        }
      />
    );
  }

  const selectedAddress = addressList.find((a) => a.id === state.addressId);
  const pickupBranch = quote?.pickupBranches.find((b) => b.id === state.pickupBranchId);
  const fulfilmentSummary =
    state.fulfilment === 'Delivery'
      ? selectedAddress
        ? `Delivery to ${selectedAddress.label} — ${selectedAddress.line1}, ${selectedAddress.district}`
        : 'Delivery'
      : `Collect from ${pickupBranch?.name ?? 'a branch'}`;
  const chosenSlot = chosenLevel?.slots.find(
    (slot) => slot.date === state.slotDate && slot.windowStart === state.slotWindowStart,
  );
  const slotSummary = slotSkipped
    ? state.fulfilment === 'Pickup'
      ? 'Not needed for collection.'
      : chosenLevel
        ? `${serviceLevelLabel(chosenLevel.level)} · ${leadTimeText(chosenLevel.leadTimeDays)}`
        : null
    : chosenLevel
      ? `${serviceLevelLabel(chosenLevel.level)}${chosenSlot ? ` · ${formatDeliverySlot(chosenSlot.date, chosenSlot.windowStart, chosenSlot.windowEnd, zone)}` : ''}`
      : null;

  const statusOf = (step: CheckoutStep): StepDescriptor['status'] => {
    if (step === 'slot' && slotSkipped) return 'skipped';
    if (step === currentStep) return 'current';
    const done =
      order.indexOf(step) < order.indexOf(currentStep) &&
      !(step === 'fulfilment' && fulfilmentIncomplete) &&
      !(step === 'slot' && slotIncomplete) &&
      !(step === 'payment' && paymentIncomplete);
    return done ? 'done' : 'todo';
  };

  const nextAfterFulfilment: CheckoutStep = slotSkipped ? 'payment' : 'slot';
  const previousBeforePayment: CheckoutStep = slotSkipped ? 'fulfilment' : 'slot';

  // Steps 1-3 sit beside the quote's neutral order summary, with the step's Continue block inside it.
  const renderSummary: RenderSummary = (footer) =>
    quote ? (
      <OrderSummary
        appearance="hearth"
        totals={quote.totals}
        discountCode={quote.discount?.applied ? quote.discount.code : null}
        serviceLevel={quote.selectedServiceLevel}
        fulfilment={state.fulfilment}
        footer={footer}
      />
    ) : (
      <section aria-label="Order summary" className={`${summaryCardClass} flex flex-col gap-4`}>
        <h2 className="m-0 font-display text-2xl font-semibold text-ink">Order summary</h2>
        {quoteQuery.isError ? (
          <p role="alert" className="m-0 text-sm text-ink-muted">
            We couldn&rsquo;t price your order just now.
          </p>
        ) : (
          <LoadingState rows={4} label="Pricing your order…" />
        )}
        {footer}
      </section>
    );
  const rxLines = cart.data?.lines ?? [];
  const needsAcknowledgement = (quote?.changes.length ?? 0) > 0;

  const steps: StepDescriptor[] = [
    {
      id: 'fulfilment',
      title: STEP_TITLES.fulfilment,
      status: statusOf('fulfilment'),
      summary: fulfilmentSummary,
      body: (
        <FulfilmentStep
          fulfilment={state.fulfilment}
          onFulfilment={(value) => {
            setStepErrors({});
            update({
              fulfilment: value,
              serviceLevel: undefined,
              slotDate: undefined,
              slotWindowStart: undefined,
              paymentMethod: undefined,
            });
          }}
          addresses={addressList}
          addressId={state.addressId}
          onAddress={(id) =>
            update({
              addressId: id,
              slotDate: undefined,
              slotWindowStart: undefined,
              paymentMethod: undefined,
            })
          }
          onAddAddress={(values) => addAddress.mutateAsync(values)}
          pickupBranches={quote?.pickupBranches ?? []}
          pickupBranchId={state.pickupBranchId}
          onPickupBranch={(id) => update({ pickupBranchId: id })}
          delivery={quote?.delivery}
          deliveryTaxUnavailable={deliveryTaxUnavailable}
          districtName={selectedAddress?.district ?? null}
          recipientName={recipient?.name ?? ''}
          recipientPhone={recipient?.phone ?? ''}
          onRecipient={(patch) =>
            setRecipient((current) => ({
              name: patch.name ?? current?.name ?? '',
              phone: patch.phone ?? current?.phone ?? '',
            }))
          }
          error={stepErrors.fulfilment ?? null}
          renderSummary={renderSummary}
          continueLabel={slotSkipped ? 'Continue to payment' : 'Continue to delivery time'}
          onContinue={() => goTo(nextAfterFulfilment)}
          continueDisabledReason={
            quoteQuery.isError
              ? "We couldn't check delivery just now."
              : !quote
                ? 'Checking delivery options…'
                : fulfilmentIncomplete
                  ? state.fulfilment === 'Delivery'
                    ? deliveryTaxUnavailable
                      ? 'Delivery is unavailable — choose collection to continue.'
                      : !state.addressId
                        ? 'Choose or add a delivery address.'
                        : 'We can’t deliver to this address — choose another or collect instead.'
                    : 'Choose a branch to collect from.'
                  : null
          }
        />
      ),
    },
    {
      id: 'slot',
      title: STEP_TITLES.slot,
      status: statusOf('slot'),
      summary: slotSummary,
      body: (
        <SlotStep
          levels={levels}
          selectedLevel={effectiveLevel}
          onLevel={(level) =>
            update({ serviceLevel: level, slotDate: undefined, slotWindowStart: undefined })
          }
          slotDate={state.slotDate}
          slotWindowStart={state.slotWindowStart}
          onSlot={(date, start) => update({ slotDate: date, slotWindowStart: start })}
          zone={zone}
          renderSummary={renderSummary}
          continueLabel="Continue to payment"
          onBack={() => goTo('fulfilment')}
          onContinue={() => goTo('payment')}
          continueDisabledReason={slotIncomplete ? 'Choose a delivery time to continue.' : null}
        />
      ),
    },
    {
      id: 'payment',
      title: STEP_TITLES.payment,
      status: statusOf('payment'),
      summary: state.paymentMethod ? paymentMethodLabel(state.paymentMethod) : null,
      body: (
        <>
          {stepErrors.payment && (
            <p role="alert" className="mb-3 text-sm font-semibold text-danger-ink">
              {stepErrors.payment}
            </p>
          )}
          <PaymentStep
            options={quote?.paymentOptions ?? []}
            selected={state.paymentMethod}
            onSelect={(method) => {
              setStepErrors((current) => ({ ...current, payment: undefined }));
              update({ paymentMethod: method });
            }}
            renderSummary={renderSummary}
            continueLabel="Continue to review"
            onBack={() => goTo(previousBeforePayment)}
            onContinue={() => goTo('review')}
            continueDisabledReason={
              paymentIncomplete ? 'Choose how you will pay to continue.' : null
            }
          />
        </>
      ),
    },
    {
      id: 'review',
      title: STEP_TITLES.review,
      status: statusOf('review'),
      summary: null,
      body: quote ? (
        <ReviewStep
          quote={quote}
          imageBySku={imageBySku}
          fulfilment={state.fulfilment}
          settling={settling}
          paymentMethod={state.paymentMethod}
          choiceCards={
            <ReviewChoices
              fulfilment={state.fulfilment}
              fulfilmentSummary={fulfilmentSummary}
              slotSummary={state.fulfilment === 'Delivery' ? slotSummary : null}
              paymentLabel={state.paymentMethod ? paymentMethodLabel(state.paymentMethod) : null}
              paymentNote={
                state.paymentMethod ? paymentMethodExplanation(state.paymentMethod) : null
              }
              paymentMethod={state.paymentMethod}
              onChangeFulfilment={() => goTo('fulfilment')}
              onChangeSlot={slotSkipped ? undefined : () => goTo('slot')}
              onChangePayment={() => goTo('payment')}
            />
          }
          needsAcknowledgement={needsAcknowledgement}
          acknowledged={acknowledged === quote.fingerprint}
          placing={place.isPending}
          inProgress={inProgress}
          placeError={placeError}
          onPlace={() => void handlePlace()}
          rxPanel={
            <PrescriptionLinesPanel
              lines={rxLines}
              cats={(cats.data?.items ?? []).map((cat) => ({ id: cat.id, name: cat.name }))}
              pendingLineId={rxPending}
              error={rxError}
              onChoose={(lineId, catId) => {
                setRxError(null);
                setRxPending(lineId);
                updateLine.mutate(
                  { lineId, catId },
                  {
                    onSuccess: () => {
                      void queryClient.invalidateQueries({ queryKey: ['checkout', 'quote'] });
                    },
                    onError: (error) => setRxError(describeOrderingError(error)),
                    onSettled: () => setRxPending(null),
                  },
                );
              }}
            />
          }
          changesPanel={
            <ChangesPanel
              changes={quote.changes}
              fingerprint={quote.fingerprint}
              acknowledgedFingerprint={acknowledged}
              onAcknowledge={setAcknowledged}
              bookHref="/book?category=Consultation"
              settling={settling}
            />
          }
          policiesPanel={
            <PendingPoliciesPanel
              policies={quote.pendingPolicies}
              acceptingKey={acceptingKey}
              error={policyError}
              onAccept={(policy) => {
                setPolicyError(null);
                setAcceptingKey(policy.key);
                acceptPolicy.mutate(
                  { policyKey: policy.key, versionNumber: policy.versionNumber },
                  {
                    onSuccess: () => {
                      void queryClient.invalidateQueries({ queryKey: ['checkout', 'quote'] });
                    },
                    onError: (error) => setPolicyError(describeOrderingError(error)),
                    onSettled: () => setAcceptingKey(null),
                  },
                );
              }}
            />
          }
          discountField={
            <DiscountCodeField
              appliedCode={state.discountCode}
              discount={quote.discount}
              checking={settling}
              onApply={(code) => update({ discountCode: code })}
              onRemove={() => update({ discountCode: undefined })}
            />
          }
        />
      ) : quoteQuery.isError ? (
        <ErrorState
          title="We couldn't price your order just now."
          description="Your choices are saved in this page's address — please try again."
          onRetry={() => void quoteQuery.refetch()}
        />
      ) : (
        <LoadingState rows={4} label="Pricing your order…" />
      ),
    },
  ];

  const currentIndex = steps.findIndex((step) => step.status === 'current');
  const currentDescriptor = steps[currentIndex] ?? steps[0]!;
  const pillSteps: PillStep[] = steps.map((step, index) => {
    const label =
      step.id === 'fulfilment'
        ? step.status === 'done'
          ? state.fulfilment === 'Pickup'
            ? 'Pickup'
            : 'Delivery'
          : 'Delivery or pickup'
        : step.id === 'review'
          ? 'Review'
          : step.title;
    let hint: string | undefined;
    if (step.status === 'done' || step.status === 'skipped') hint = step.summary ?? undefined;
    else if (step.status === 'todo')
      hint =
        step.id === 'review'
          ? 'Then place your order'
          : index === currentIndex + 1
            ? 'Next'
            : undefined;
    return { label, status: step.status, hint };
  });
  // Earlier steps stay re-openable (the old "Change" buttons). The review step has its own Change links.
  const reopenable =
    currentDescriptor.id === 'review' ? [] : steps.filter((step) => step.status === 'done');

  const GREETING: Record<CheckoutStep, { title: string; lead: string }> = {
    fulfilment: {
      title: firstName
        ? `How should we get it to you, ${firstName}?`
        : 'How should we get it to you?',
      lead: 'Choose delivery or collect it from a branch. You can change this until you place the order.',
    },
    slot: {
      title: firstName ? `When suits you, ${firstName}?` : 'When suits you?',
      lead: `Pick a speed, then a day and a window. All times are ${zone}.`,
    },
    payment: {
      title: 'How would you like to pay?',
      lead: 'You pay a person later: the courier, our bank, or the counter. Nothing is charged on this website.',
    },
    review: {
      title: firstName ? `Almost there, ${firstName}` : 'Almost there',
      lead: `Check everything once, then place your order.${state.paymentMethod === 'BankTransfer' ? ' Our bank details appear straight after.' : ''}`,
    },
  };
  const greeting = GREETING[currentDescriptor.id];

  return (
    <div className="flex flex-col gap-7">
      <GreetingHeader title={greeting.title}>{greeting.lead}</GreetingHeader>
      <PillSteps steps={pillSteps} />
      {reopenable.length > 0 && (
        <ul
          aria-label="Change an earlier step"
          className="-mb-4 -mt-5 flex list-none flex-wrap gap-x-5 p-0"
        >
          {reopenable.map((step) => (
            <li key={step.id}>
              <button
                type="button"
                onClick={() => goTo(step.id)}
                aria-label={`Change ${step.title.toLowerCase()}`}
                className="inline-flex min-h-11 items-center text-[13px] font-semibold text-accent-ink hover:text-accent"
              >
                {`Change ${step.id === 'fulfilment' ? 'delivery' : step.id === 'slot' ? 'delivery time' : 'payment'}`}
              </button>
            </li>
          ))}
        </ul>
      )}
      <div>{currentDescriptor.body}</div>
    </div>
  );
}
