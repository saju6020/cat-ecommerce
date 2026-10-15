'use client';

import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { LoadingState } from '@/components/common/LoadingState';
import { AdjustmentForm } from '@/components/inventory/AdjustmentForm';
import { PageHeading } from '@/components/inventory/fields';
import { ApiForbiddenError } from '@/lib/api/client';
import { useRequestAdjustment } from '@/lib/hooks/useInventory';
import { useBarcodeLookup, useLocations } from '@/lib/hooks/useInventoryLookup';

/** `/staff/inventory/adjustments/new` — US-IMP-13 AC2. */
export default function NewAdjustmentPage() {
  const locations = useLocations();
  const lookup = useBarcodeLookup();
  const request = useRequestAdjustment();
  if (locations.error instanceof ApiForbiddenError) return <AccessRefusedState error={locations.error} />;
  if (locations.isLoading) return <LoadingState rows={4} />;
  return (
    <div className="flex flex-col gap-4">
      <PageHeading
        title="New adjustment"
        description="A reason and a note are required. The server decides whether it needs a second person's approval."
      />
      <AdjustmentForm
        locations={locations.data}
        onScan={lookup}
        isPending={request.isPending}
        onRequest={(body) => request.mutateAsync(body)}
      />
    </div>
  );
}
