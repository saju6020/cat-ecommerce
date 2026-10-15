'use client';

import { Suspense } from 'react';
import Link from 'next/link';
import { ActiveBranchZoneGate } from '@/components/appointments/ActiveBranchZoneGate';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { LoadingState } from '@/components/common/LoadingState';
import { buttonVariants } from '@/components/ui/button';
import { PageHeading } from '@/components/inventory/fields';
import { SurgeryBookingForm } from '@/components/surgery/SurgeryBookingForm';
import { ApiForbiddenError } from '@/lib/api/client';
import { useSurgeryServices } from '@/lib/hooks/useSurgeries';

/** `/staff/surgeries/new` — HO-SRG-002 §11.1 (US-SRG-01, 11): book a surgery. */
export default function NewSurgeryPage() {
  return (
    <div className="flex flex-col gap-5">
      <PageHeading
        title="Book a surgery"
        description="Choose the procedure, a date, the surgeon, the assistant and the theatre. Only times when all three are free are offered."
        actions={
          <Link href="/staff/surgeries" className={buttonVariants({ variant: 'secondary' })}>
            Theatre day list
          </Link>
        }
      />
      <Suspense fallback={<LoadingState variant="card" rows={4} label="Loading…" />}>
        <ActiveBranchZoneGate>{(zone) => <Gated zone={zone} />}</ActiveBranchZoneGate>
      </Suspense>
    </div>
  );
}

function Gated({ zone }: { zone: string }) {
  const services = useSurgeryServices();
  if (services.error instanceof ApiForbiddenError)
    return <AccessRefusedState error={services.error} />;
  return <SurgeryBookingForm zone={zone} />;
}
