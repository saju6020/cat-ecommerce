'use client';

import { useRouter } from 'next/navigation';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { LoadingState } from '@/components/common/LoadingState';
import { CountOpenForm } from '@/components/inventory/CountOpenForm';
import { PageHeading } from '@/components/inventory/fields';
import { ApiForbiddenError } from '@/lib/api/client';
import { useOpenCount } from '@/lib/hooks/useInventory';
import { useLocations } from '@/lib/hooks/useInventoryLookup';

/** `/staff/inventory/counts/new` — US-IMP-14: open a count. */
export default function NewCountPage() {
  const router = useRouter();
  const locations = useLocations();
  const open = useOpenCount();
  if (locations.error instanceof ApiForbiddenError) return <AccessRefusedState error={locations.error} />;
  if (locations.isLoading) return <LoadingState rows={3} />;
  return (
    <div className="flex flex-col gap-4">
      <PageHeading title="Open a count" description="Counting is blind: you will not see what the books say until you submit." />
      <CountOpenForm
        locations={locations.data}
        isPending={open.isPending}
        onOpen={async (request) => {
          const opened = await open.mutateAsync(request);
          router.push(`/staff/inventory/counts/${opened.countId}`);
        }}
      />
    </div>
  );
}
