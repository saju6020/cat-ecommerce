'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { buttonVariants } from '@/components/ui/button';
import { Banner, PageHeading } from '@/components/inventory/fields';
import { DispensaryQueue } from '@/components/pharmacy/DispensaryQueue';
import { PrescriptionLookup } from '@/components/pharmacy/PrescriptionLookup';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { useLookupPrescription, usePharmacyQueue } from '@/lib/hooks/usePharmacy';

/** `/staff/pharmacy` — US-PHM-05, 06: the dispensary queue and the scan-or-type prescription lookup. */
export default function PharmacyPage() {
  const router = useRouter();
  const queue = usePharmacyQueue();
  const lookup = useLookupPrescription();
  const zone = useBranchTimeZone();
  if (queue.error instanceof ApiForbiddenError) return <AccessRefusedState error={queue.error} />;
  return (
    <div className="flex flex-col gap-4">
      <PageHeading
        title="Pharmacy"
        description="Dispense issued prescriptions and owners’ repeat requests for the active branch. The queue refreshes every 30 seconds."
        actions={
          <Link
            href="/staff/pharmacy/controlled-drugs"
            className={buttonVariants({ variant: 'secondary', size: 'md' })}
          >
            Controlled-drug register
          </Link>
        }
      />
      {queue.isError && queue.data && (
        <Banner tone="warning">
          The queue could not be refreshed just now; this is the last version we have.
        </Banner>
      )}
      <PrescriptionLookup
        onFind={async (number) => {
          try {
            const found = await lookup.mutateAsync(number);
            router.push(`/staff/pharmacy/dispense/${found.prescriptionId}`);
            return 'found';
          } catch (error) {
            return error instanceof ApiNotFoundError ? 'notFound' : 'failed';
          }
        }}
      />
      <DispensaryQueue
        queue={queue.data}
        zone={zone}
        isLoading={queue.isLoading}
        isError={queue.isError && !queue.data}
        onRetry={() => void queue.refetch()}
      />
    </div>
  );
}
