'use client';

import { useState } from 'react';
import { Plus } from 'lucide-react';
import { AddressForm } from '@/components/account/AddressForm';
import { Button } from '@/components/ui/button';
import type { DeliveryAddress, DeliveryAddressRequest } from '@/lib/api/account';
import type { FulfilmentChoice, PickupBranch, QuoteDelivery } from '@/lib/api/checkout';
import { describeBlockingReason } from '@/lib/api/errors';

const RADIO = 'h-4 w-4 flex-none accent-[var(--accent)]';
const INPUT =
  'w-full rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent';

/**
 * `components/checkout/FulfilmentStep.tsx` — HO-ORD-002 §11.2 step 1, US-ORD-03/06. **Delivery** (saved
 * addresses from HO-ACC-002; *Add an address* opens its form inline and returns the id) or **Pickup**
 * (branches with address and phone). When the quote says the address is outside every zone, or the
 * shop's delivery tax is not configured, delivery is explained in words and pickup stays selectable —
 * everything shown here is the quote's.
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
}) {
  const [adding, setAdding] = useState(false);
  const outside = delivery?.available === false && delivery.unavailableReason === 'AddressOutsideServedZones';

  return (
    <div className="flex flex-col gap-4">
      <fieldset className="m-0 flex min-w-0 flex-col gap-2 border-0 p-0">
        <legend className="mb-1 text-sm font-bold text-ink">How would you like to get your order?</legend>
        <label className="flex items-start gap-3 rounded-[var(--radius-md)] border border-border-strong bg-surface p-3 text-sm">
          <input
            type="radio"
            name="fulfilment"
            className={`${RADIO} mt-0.5`}
            checked={fulfilment === 'Delivery'}
            onChange={() => onFulfilment('Delivery')}
          />
          <span>
            <b className="block text-ink">Delivery</b>
            <span className="text-ink-muted">We bring it to your door.</span>
          </span>
        </label>
        <label className="flex items-start gap-3 rounded-[var(--radius-md)] border border-border-strong bg-surface p-3 text-sm">
          <input
            type="radio"
            name="fulfilment"
            className={`${RADIO} mt-0.5`}
            checked={fulfilment === 'Pickup'}
            onChange={() => onFulfilment('Pickup')}
          />
          <span>
            <b className="block text-ink">Collect from a branch</b>
            <span className="text-ink-muted">Pick it up when it is ready.</span>
          </span>
        </label>
      </fieldset>

      {fulfilment === 'Delivery' && deliveryTaxUnavailable && (
        <p role="status" className="rounded-[var(--radius-sm)] bg-warning-bg p-3 text-sm font-semibold text-warning-ink">
          {describeBlockingReason('DeliveryTaxNotConfigured')}
        </p>
      )}
      {outside && (
        <p role="status" className="rounded-[var(--radius-sm)] bg-warning-bg p-3 text-sm font-semibold text-warning-ink">
          {districtName
            ? `We don't deliver to ${districtName} yet. You can collect your order instead.`
            : "We don't deliver to that address yet. You can collect your order instead."}
        </p>
      )}

      {fulfilment === 'Delivery' ? (
        <fieldset className="m-0 flex min-w-0 flex-col gap-2 border-0 p-0">
          <legend className="mb-1 text-sm font-bold text-ink">Delivery address</legend>
          {addresses.length === 0 && (
            <p className="text-sm text-ink-muted">You haven&rsquo;t saved an address yet.</p>
          )}
          {addresses.map((address) => (
            <label key={address.id} className="flex items-start gap-3 rounded-[var(--radius-md)] border border-border bg-surface p-3 text-sm">
              <input
                type="radio"
                name="address"
                className={`${RADIO} mt-0.5`}
                checked={addressId === address.id}
                onChange={() => onAddress(address.id)}
              />
              <span className="min-w-0 [overflow-wrap:anywhere]">
                <b className="block text-ink">
                  {address.label}
                  {address.isDefault ? ' (default)' : ''}
                </b>
                <span className="text-ink-muted">
                  {[address.line1, address.line2, address.city, address.district, address.postalCode]
                    .filter(Boolean)
                    .join(', ')}
                </span>
              </span>
            </label>
          ))}
          <div>
            <Button variant="secondary" size="sm" onClick={() => setAdding(true)}>
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
        <fieldset className="m-0 flex min-w-0 flex-col gap-2 border-0 p-0">
          <legend className="mb-1 text-sm font-bold text-ink">Collect from</legend>
          {pickupBranches.length === 0 && (
            <p className="text-sm text-ink-muted">Loading branches…</p>
          )}
          {pickupBranches.map((branch) => (
            <label key={branch.id} className="flex items-start gap-3 rounded-[var(--radius-md)] border border-border bg-surface p-3 text-sm">
              <input
                type="radio"
                name="pickup-branch"
                className={`${RADIO} mt-0.5`}
                checked={pickupBranchId === branch.id}
                onChange={() => onPickupBranch(branch.id)}
              />
              <span className="min-w-0 [overflow-wrap:anywhere]">
                <b className="block text-ink">{branch.name}</b>
                <span className="text-ink-muted">
                  {[branch.address, branch.phone].filter(Boolean).join(' · ')}
                </span>
              </span>
            </label>
          ))}
        </fieldset>
      )}

      <div className="grid gap-3 sm:grid-cols-2">
        <div className="flex flex-col gap-1.5">
          <label htmlFor="recipient-name" className="text-sm font-semibold text-ink">
            Recipient name
          </label>
          <input
            id="recipient-name"
            className={INPUT}
            value={recipientName}
            autoComplete="name"
            onChange={(event) => onRecipient({ name: event.target.value })}
          />
        </div>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="recipient-phone" className="text-sm font-semibold text-ink">
            Recipient phone
          </label>
          <input
            id="recipient-phone"
            className={INPUT}
            value={recipientPhone}
            inputMode="tel"
            autoComplete="tel"
            onChange={(event) => onRecipient({ phone: event.target.value })}
          />
        </div>
      </div>

      {error && (
        <p role="alert" className="text-sm font-semibold text-danger-ink">
          {error}
        </p>
      )}
      <div className="flex flex-wrap items-center gap-3">
        <Button disabled={continueDisabledReason !== null} aria-describedby={continueDisabledReason ? 'fulfilment-why' : undefined} onClick={onContinue}>
          Continue
        </Button>
        {continueDisabledReason && (
          <p id="fulfilment-why" className="text-sm text-ink-muted">
            {continueDisabledReason}
          </p>
        )}
      </div>
    </div>
  );
}
