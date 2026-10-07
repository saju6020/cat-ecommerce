'use client';

import { useState } from 'react';
import { CheckCircle2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { describeApiError } from '@/lib/api/errors';
import type { DeliveryAddress } from '@/lib/api/account';

/**
 * `components/account/AddressCard.tsx` — US-ACC-06, UI-3 (removal confirms, naming the label).
 *
 * ARCH gate HIGH 3 (first pass) — takes `onRemove`/`onSetDefault`/`isSettingDefault` as props
 * instead of calling `useRemoveDeliveryAddress()`/`useSetDefaultDeliveryAddress()` itself; the
 * page (`app/(portal)/portal/account/addresses/page.tsx`) owns both mutations (one instance each,
 * shared across every card in the list) and threads them down through `AddressList`.
 */
export interface AddressCardProps {
  address: DeliveryAddress;
  onEdit: () => void;
  onRemove: (addressId: string) => Promise<void>;
  onSetDefault: (addressId: string) => void;
  isSettingDefault: boolean;
}

export function AddressCard({
  address,
  onEdit,
  onRemove,
  onSetDefault,
  isSettingDefault,
}: AddressCardProps) {
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [removeError, setRemoveError] = useState<string | null>(null);

  async function handleRemove() {
    setRemoveError(null);
    try {
      await onRemove(address.id);
      setConfirmOpen(false);
    } catch (error) {
      // DefaultAddressCannotBeRemoved surfaces here, as the server's own reason.
      setRemoveError(describeApiError(error));
    }
  }

  return (
    <div className="flex flex-col gap-2 rounded-[var(--radius-md)] border border-border bg-surface p-4">
      <div className="flex items-center justify-between gap-2">
        <p className="font-semibold text-ink">{address.label}</p>
        {address.isDefault && (
          <Badge variant="success" icon={<CheckCircle2 size={12} aria-hidden="true" />}>
            Default
          </Badge>
        )}
      </div>
      <p className="text-sm text-ink-muted">
        {address.line1}
        {address.line2 ? `, ${address.line2}` : ''}, {address.city}, {address.district}
        {address.postalCode ? ` ${address.postalCode}` : ''}
      </p>
      {address.deliveryInstructions && (
        <p className="text-sm text-ink-muted">{address.deliveryInstructions}</p>
      )}
      {removeError && (
        <p role="alert" className="text-xs font-semibold text-danger-ink">
          {removeError}
        </p>
      )}
      <div className="flex flex-wrap gap-2">
        <Button variant="secondary" size="sm" onClick={onEdit}>
          Edit
        </Button>
        {!address.isDefault && (
          <Button
            variant="secondary"
            size="sm"
            onClick={() => onSetDefault(address.id)}
            disabled={isSettingDefault}
          >
            Set as default
          </Button>
        )}
        <Button variant="ghost" size="sm" onClick={() => setConfirmOpen(true)}>
          Remove
        </Button>
      </div>

      <ConfirmDialog
        open={confirmOpen}
        onOpenChange={setConfirmOpen}
        title={`Remove "${address.label}"?`}
        consequence={`This removes the "${address.label}" address from your account. This cannot be undone.`}
        confirmLabel="Remove"
        variant="destructive"
        onConfirm={handleRemove}
      />
    </div>
  );
}
