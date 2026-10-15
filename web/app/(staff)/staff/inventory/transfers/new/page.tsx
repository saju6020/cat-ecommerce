'use client';

import { useRouter } from 'next/navigation';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { LoadingState } from '@/components/common/LoadingState';
import { PageHeading } from '@/components/inventory/fields';
import { TransferForm } from '@/components/inventory/TransferForm';
import { ApiForbiddenError } from '@/lib/api/client';
import { usePostTransfer } from '@/lib/hooks/useInventory';
import { useBarcodeLookup, useLocations } from '@/lib/hooks/useInventoryLookup';

/** `/staff/inventory/transfers/new` — US-IMP-13 AC1. */
export default function NewTransferPage() {
  const router = useRouter();
  const locations = useLocations();
  const lookup = useBarcodeLookup();
  const post = usePostTransfer();
  if (locations.error instanceof ApiForbiddenError) return <AccessRefusedState error={locations.error} />;
  if (locations.isLoading) return <LoadingState rows={4} />;
  return (
    <div className="flex flex-col gap-4">
      <PageHeading title="New transfer" description="Move stock from one location to another. Both ends are recorded." />
      <TransferForm
        locations={locations.data}
        onScan={lookup}
        isPending={post.isPending}
        onPost={async (request) => {
          await post.mutateAsync(request);
          router.push('/staff/inventory/transfers');
        }}
      />
    </div>
  );
}
