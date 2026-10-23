'use client';

import type { ReactNode } from 'react';
import { Landmark, House, Store, Banknote } from 'lucide-react';
import { IconTile } from '@/components/hearth/IconTile';
import type { FulfilmentChoice, PaymentMethodChoice } from '@/lib/api/checkout';

const CHANGE =
  'inline-flex min-h-11 items-center whitespace-nowrap text-[13px] font-semibold text-accent-ink hover:text-accent';

function ChoiceCard({
  id,
  icon,
  title,
  change,
  children,
}: {
  id: string;
  icon: ReactNode;
  title: string;
  change: ReactNode;
  children: ReactNode;
}) {
  return (
    <section
      aria-labelledby={id}
      className="flex gap-3.5 rounded-card border border-border bg-surface p-5 sm:px-6"
    >
      <IconTile shape="tile" size="md">
        {icon}
      </IconTile>
      <div className="flex min-w-0 flex-1 flex-col gap-1">
        <div className="-my-3 flex items-center justify-between gap-2">
          <strong id={id} className="text-[15px]">
            {title}
          </strong>
          {change}
        </div>
        {children}
      </div>
    </section>
  );
}

/**
 * The review step's two cards (docs/design/orders/CheckoutReview.dc.html `.choice`): where the order is
 * going and how it is paid, each with a **Change** link back to that step. Every line is what the
 * quote and the customer's choices say (the summaries are built by the checkout screen).
 */
export function ReviewChoices({
  fulfilment,
  fulfilmentSummary,
  slotSummary,
  paymentLabel,
  paymentNote,
  paymentMethod,
  onChangeFulfilment,
  onChangeSlot,
  onChangePayment,
}: {
  fulfilment: FulfilmentChoice;
  fulfilmentSummary: string;
  /** Service level and window (or lead time) for delivery; `null` for collection. */
  slotSummary: string | null;
  paymentLabel: string | null;
  paymentNote: string | null;
  paymentMethod: PaymentMethodChoice | undefined;
  onChangeFulfilment: () => void;
  /** Absent when the delivery-time step is skipped, so there is nothing to change. */
  onChangeSlot?: () => void;
  onChangePayment: () => void;
}) {
  return (
    <div className="grid gap-[18px] sm:grid-cols-2">
      <ChoiceCard
        id="review-delivery"
        icon={
          fulfilment === 'Pickup' ? (
            <Store size={20} aria-hidden="true" />
          ) : (
            <House size={20} aria-hidden="true" />
          )
        }
        title={fulfilment === 'Pickup' ? 'Collection' : 'Delivery'}
        change={
          <button
            type="button"
            className={CHANGE}
            onClick={onChangeFulfilment}
            aria-label="Change delivery"
          >
            Change
          </button>
        }
      >
        <span className="text-sm leading-relaxed [overflow-wrap:anywhere]">
          {fulfilmentSummary}
        </span>
        {slotSummary && (
          <span className="text-[13px] text-ink-muted [overflow-wrap:anywhere]">
            {slotSummary}
            {onChangeSlot && (
              <>
                {' · '}
                <button
                  type="button"
                  className="font-semibold text-accent-ink underline hover:text-accent"
                  onClick={onChangeSlot}
                  aria-label="Change delivery time"
                >
                  change time
                </button>
              </>
            )}
          </span>
        )}
      </ChoiceCard>
      <ChoiceCard
        id="review-payment"
        icon={
          paymentMethod === 'BankTransfer' ? (
            <Landmark size={20} aria-hidden="true" />
          ) : (
            <Banknote size={20} aria-hidden="true" />
          )
        }
        title="Payment"
        change={
          <button
            type="button"
            className={CHANGE}
            onClick={onChangePayment}
            aria-label="Change payment method"
          >
            Change
          </button>
        }
      >
        {paymentLabel && <span className="text-sm leading-relaxed">{paymentLabel}</span>}
        {paymentNote && <span className="text-[13px] text-ink-muted">{paymentNote}</span>}
      </ChoiceCard>
    </div>
  );
}
