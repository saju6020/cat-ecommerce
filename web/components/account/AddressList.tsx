'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { EmptyState } from '@/components/common/EmptyState';
import { AddressCard } from './AddressCard';
import { AddressForm } from './AddressForm';
import type {
  DeliveryAddress,
  DeliveryAddressBook,
  DeliveryAddressRequest,
} from '@/lib/api/account';

/**
 * `components/account/AddressList.tsx` — US-ACC-06. Limit reached → the add button explains why
 * rather than disappearing.
 *
 * ARCH gate HIGH 3 (first pass) — takes `onAdd`/`onUpdate`/`onRemove`/`onSetDefault`/
 * `settingDefaultAddressId` as props; the page (`app/(portal)/portal/account/addresses/page.tsx`)
 * owns all four mutations (one instance each, shared across every card) and this component only
 * decides WHICH address is currently being edited, which used to also mean it had to instantiate
 * `useUpdateDeliveryAddress` itself with that address's id baked in.
 */
export interface AddressListProps {
  book: DeliveryAddressBook;
  onAdd: (values: DeliveryAddressRequest) => Promise<unknown>;
  onUpdate: (addressId: string, values: DeliveryAddressRequest) => Promise<unknown>;
  onRemove: (addressId: string) => Promise<void>;
  onSetDefault: (addressId: string) => void;
  settingDefaultAddressId: string | null;
}

export function AddressList({
  book,
  onAdd,
  onUpdate,
  onRemove,
  onSetDefault,
  settingDefaultAddressId,
}: AddressListProps) {
  const [formOpen, setFormOpen] = useState(false);
  const [editingAddress, setEditingAddress] = useState<DeliveryAddress | undefined>(undefined);
  const limitReached = book.addresses.length >= book.maxAddresses;

  function openAdd() {
    setEditingAddress(undefined);
    setFormOpen(true);
  }
  function openEdit(address: DeliveryAddress) {
    setEditingAddress(address);
    setFormOpen(true);
  }

  async function handleSubmit(values: DeliveryAddressRequest) {
    if (editingAddress) return onUpdate(editingAddress.id, values);
    return onAdd(values);
  }

  return (
    <div className="flex flex-col gap-4">
      {book.addresses.length === 0 ? (
        <EmptyState
          title="No addresses yet"
          description="Add a delivery address to use at checkout."
          action={
            <Button onClick={openAdd} disabled={limitReached}>
              Add address
            </Button>
          }
        />
      ) : (
        <>
          <div className="flex flex-col gap-3">
            {book.addresses.map((address) => (
              <AddressCard
                key={address.id}
                address={address}
                onEdit={() => openEdit(address)}
                onRemove={onRemove}
                onSetDefault={onSetDefault}
                isSettingDefault={settingDefaultAddressId === address.id}
              />
            ))}
          </div>
          <div className="flex flex-col gap-1">
            <Button onClick={openAdd} disabled={limitReached} className="w-fit">
              Add address
            </Button>
            {limitReached && (
              <p className="text-xs text-ink-muted">
                You have reached the maximum of {book.maxAddresses} addresses. Remove one to add
                another.
              </p>
            )}
          </div>
        </>
      )}

      <AddressForm
        open={formOpen}
        onOpenChange={setFormOpen}
        address={editingAddress}
        onSubmit={handleSubmit}
      />
    </div>
  );
}
