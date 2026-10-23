'use client';

import { useState } from 'react';
import { House, Plus, Store, Truck, TriangleAlert } from 'lucide-react';
import { AddressForm } from '@/components/account/AddressForm';
import { bannerWarnClass, cardClass } from '@/components/hearth/card';
import { pillInputClass } from '@/components/hearth/fields';
import { OptionCard } from '@/components/hearth/OptionCard';
import { Button } from '@/components/ui/button';
import type { DeliveryAddress, DeliveryAddressRequest } from '@/lib/api/account';
import type { FulfilmentChoice, PickupBranch, QuoteDelivery } from '@/lib/api/checkout';
import { describeBlockingReason } from '@/lib/api/errors';
import { cn } from '@/lib/utils';
import { StepFooter, StepLayout, type RenderSummary } from './StepLayout';

const SECTION_HEADING = 'm-0 text-base font-bold text-ink';

/**
 * `components/checkout/FulfilmentStep.tsx` — HO-ORD-002 §11.2 step 1, US-ORD-03/06. **Delivery** (saved
 * addresses from HO-ACC-002; *Add an address* opens its form inline and returns the id) or **Pickup**
 * (branches with address and phone), as Hearth choice cards beside the order summary. When the quote
 * says the address is outside every zone, or the shop's delivery tax is not configured, delivery is
 * explained in words and pickup stays selectable — everything shown here is the quote's.
 */
export function FulfilmentStep({
  fulfilment,
  onFulfilment,
  addresses,
  addressId,
  onAddress,
  onAddAddress,
  pickupBranches,
  pickupBranchId,
  onPickupBranch,
  delivery,
  deliveryTaxUnavailable,
  districtName,
  recipientName,
  recipientPhone,
  onRecipient,
  error,
  onContinue,
  continueDisabledReason,
  continueLabel,
  renderSummary,
}: {
  fulfilment: FulfilmentChoice;
  onFulfilment: (value: FulfilmentChoice) => void;
  addresses: DeliveryAddress[];
  addressId: string | undefined;
  onAddress: (id: string) => void;
  onAddAddress: (values: DeliveryAddressRequest) => Promise<unknown>;
  pickupBranches: PickupBranch[];
  pickupBranchId: string | undefined;
  onPickupBranch: (id: string) => void;
  delivery: QuoteDelivery | undefined;
  deliveryTaxUnavailable: boolean;
  districtName: string | null;
  recipientName: string;
  recipientPhone: string;
  onRecipient: (patch: { name?: string; phone?: string }) => void;
  error: string | null;
  onContinue: () => void;
  continueDisabledReason: string | null;
  continueLabel?: string;
  renderSummary?: RenderSummary;
}) {
  const [adding, setAdding] = useState(false);
  const outside =
    delivery?.available === false && delivery.unavailableReason === 'AddressOutsideServedZones';

  return (
    <StepLayout
      renderSummary={renderSummary}
      footer={
        <StepFooter
          id="fulfilment"
          label={continueLabel}
          onContinue={onContinue}
          disabledReason={continueDisabledReason}
          back={{ label: 'Back to cart', href: '/cart' }}
        />
      }
    >
      <fieldset className="m-0 flex min-w-0 flex-col gap-3.5 border-0 p-0">
        <legend className="sr-only">How would you like to get your order?</legend>
        <div className="grid gap-3.5 sm:grid-cols-2">
          <OptionCard
            name="fulfilment"
            checked={fulfilment === 'Delivery'}
            onChange={() => onFulfilment('Delivery')}
            icon={<Truck size={20} aria-hidden="true" />}
            title="Delivery"
          >
            We bring it to your door.
          </OptionCard>
          <OptionCard
            name="fulfilment"
            checked={fulfilment === 'Pickup'}
            onChange={() => onFulfilment('Pickup')}
            icon={<Store size={22} aria-hidden="true" />}
            title="Collect from a branch"
          >
            Pick it up when it is ready.
          </OptionCard>
        </div>
      </fieldset>

      {fulfilment === 'Delivery' && deliveryTaxUnavailable && (
        <p role="status" className={cn(bannerWarnClass, 'm-0 font-semibold')}>
          <TriangleAlert size={15} aria-hidden="true" className="mt-0.5 flex-none" />
          {describeBlockingReason('DeliveryTaxNotConfigured')}
        </p>
      )}
      {outside && (
        <p role="status" className={cn(bannerWarnClass, 'm-0 font-semibold')}>
          <TriangleAlert size={15} aria-hidden="true" className="mt-0.5 flex-none" />
          {districtName
            ? `We don't deliver to ${districtName} yet. You can collect your order instead.`
            : "We don't deliver to that address yet. You can collect your order instead."}
        </p>
      )}

      {fulfilment === 'Delivery' ? (
        <fieldset className="m-0 flex min-w-0 flex-col gap-3 border-0 p-0">
          <legend className={cn(SECTION_HEADING, 'mb-3')}>Delivery address</legend>
          {addresses.length === 0 && (
            <p className="m-0 text-sm text-ink-muted">You haven&rsquo;t saved an address yet.</p>
          )}
          {addresses.map((address) => (
            <OptionCard
              key={address.id}
              name="address"
              checked={addressId === address.id}
              onChange={() => onAddress(address.id)}
              icon={<House size={22} aria-hidden="true" />}
              title={`${address.label}${address.isDefault ? ' (default)' : ''}`}
            >
              {[address.line1, address.line2, address.city, address.district, address.postalCode]
                .filter(Boolean)
                .join(', ')}
            </OptionCard>
          ))}
          <div>
            <Button variant="pillSecondary" size="pillMd" onClick={() => setAdding(true)}>
              <Plus size={14} aria-hidden="true" />
              Add an address
            </Button>
          </div>
          <AddressForm
            open={adding}
            onOpenChange={setAdding}
            onSubmit={async (values) => {
              const result = (await onAddAddress(values)) as { addressId?: string } | undefined;
              if (result?.addressId) onAddress(result.addressId);
              setAdding(false);
            }}
          />
        </fieldset>
      ) : (
        <fieldset className="m-0 flex min-w-0 flex-col gap-3 border-0 p-0">
          <legend className={cn(SECTION_HEADING, 'mb-3')}>Collect from</legend>
          {pickupBranches.length === 0 && (
            <p className="m-0 text-sm text-ink-muted">Loading branches…</p>
          )}
          {pickupBranches.map((branch) => (
            <OptionCard
              key={branch.id}
              name="pickup-branch"
              checked={pickupBranchId === branch.id}
              onChange={() => onPickupBranch(branch.id)}
              icon={<Store size={22} aria-hidden="true" />}
              title={branch.name}
            >
              {[branch.address, branch.phone].filter(Boolean).join(' · ')}
            </OptionCard>
          ))}
        </fieldset>
      )}

      <section aria-labelledby="recipient-heading" className={cn(cardClass, 'flex flex-col gap-3')}>
        <h2 id="recipient-heading" className={SECTION_HEADING}>
          {fulfilment === 'Pickup' ? "Who's collecting" : "Who's receiving"}
        </h2>
        <div className="grid gap-3.5 sm:grid-cols-2">
          <div className="flex flex-col gap-1.5">
            <label htmlFor="recipient-name" className="text-[13px] font-semibold text-ink">
              Recipient name
            </label>
            <input
              id="recipient-name"
              className={pillInputClass}
              value={recipientName}
              autoComplete="name"
              onChange={(event) => onRecipient({ name: event.target.value })}
            />
          </div>
          <div className="flex flex-col gap-1.5">
            <label htmlFor="recipient-phone" className="text-[13px] font-semibold text-ink">
              Recipient phone
            </label>
            <input
              id="recipient-phone"
              className={pillInputClass}
              value={recipientPhone}
              inputMode="tel"
              autoComplete="tel"
              onChange={(event) => onRecipient({ phone: event.target.value })}
            />
          </div>
        </div>
        <p className="m-0 text-xs text-ink-muted">
          Filled from your profile. Change them if someone else is{' '}
          {fulfilment === 'Pickup' ? 'collecting' : 'receiving it'}.
        </p>
      </section>

      {error && (
        <p role="alert" className="m-0 text-sm font-semibold text-danger-ink">
          {error}
        </p>
      )}
    </StepLayout>
  );
}
