'use client';

import { useRouter } from 'next/navigation';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { LoadingState } from '@/components/common/LoadingState';
import { PageHeading } from '@/components/inventory/fields';
import { StorageBreachForm } from '@/components/inventory/StorageBreachForm';
import { ApiForbiddenError } from '@/lib/api/client';
import { nowAsBranchDateTimeLocal } from '@/lib/format/inventory';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { useLocations } from '@/lib/hooks/useInventoryLookup';
import { useReportBreach } from '@/lib/hooks/useInventoryRecalls';

/** `/staff/inventory/storage-breaches/new` — US-IMP-16 AC2: report a storage-condition breach. */
export default function NewBreachPage() {
  const router = useRouter();
  const locations = useLocations();
  const report = useReportBreach();
  const zone = useBranchTimeZone();
  if (locations.error instanceof ApiForbiddenError) return <AccessRefusedState error={locations.error} />;
  if (locations.isLoading) return <LoadingState rows={4} />;
  return (
    <div className="flex flex-col gap-4">
      <PageHeading
        title="Report a storage breach"
        description="A fridge left open, a power cut, a hot delivery. Reporting flags the batches held there; quarantining them is a separate step."
      />
      <StorageBreachForm
        locations={locations.data}
        nowLocal={nowAsBranchDateTimeLocal(zone)}
        timeZoneId={zone}
        isPending={report.isPending}
        onReport={async (request) => {
          const created = await report.mutateAsync(request);
          router.push(`/staff/inventory/storage-breaches/${created.breachId}?reported=${created.affectedBatchCount}`);
        }}
      />
    </div>
  );
}
