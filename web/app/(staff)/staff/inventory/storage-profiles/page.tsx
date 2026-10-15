'use client';

import { useState } from 'react';
import { LoadingState } from '@/components/common/LoadingState';
import { PageHeading } from '@/components/inventory/fields';
import { SkuPicker } from '@/components/inventory/SkuPicker';
import { StorageChip } from '@/components/inventory/StorageChip';
import { StorageProfileForm } from '@/components/inventory/StorageProfileForm';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';
import type { SkuLookup } from '@/lib/api/inventory-lookup';
import { useSetStorageProfile, useStorageProfile } from '@/lib/hooks/useInventoryLookup';

/** `/staff/inventory/storage-profiles` — US-IMP-16: how a product must be kept. There is no list of
 *  profiles on the server, so the page starts from an item. */
export default function StorageProfilesPage() {
  const [sku, setSku] = useState<SkuLookup | null>(null);
  const profile = useStorageProfile(sku?.productId ?? null);
  const save = useSetStorageProfile();
  if (profile.error instanceof ApiForbiddenError) return <AccessRefusedState error={profile.error} />;
  const noneYet = profile.error instanceof ApiNotFoundError;
  return (
    <div className="flex flex-col gap-4">
      <PageHeading title="Storage profiles" description="How each product must be kept. Receiving and stock screens show this beside the item." />
      <div className="max-w-md">
        <SkuPicker id="storage-sku" label="Find the item" onSelect={setSku} />
      </div>
      {sku && (
        <section className="flex flex-col gap-3" aria-label={`Storage profile for ${sku.displayName}`}>
          {profile.isLoading ? (
            <LoadingState rows={3} />
          ) : (
            <>
              <p className="flex flex-wrap items-center gap-2 text-sm text-ink-muted">
                Currently: {noneYet || !profile.data ? 'no profile recorded' : <StorageChip storage={profile.data.storage} />}
              </p>
              <StorageProfileForm
                key={`${sku.productId}-${profile.data?.version ?? 0}`}
                productLabel={`${sku.displayName} — ${sku.skuCode}`}
                initial={profile.data?.storage ?? null}
                isPending={save.isPending}
                onSave={(request) => save.mutateAsync({ productId: sku.productId, request })}
              />
            </>
          )}
        </section>
      )}
    </div>
  );
}
