'use client';

import Link from 'next/link';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { buttonVariants } from '@/components/ui/button';
import { PageHeading } from '@/components/inventory/fields';
import { SurgeryProtocolTable } from '@/components/surgery/SurgeryProtocolTable';
import { ApiForbiddenError } from '@/lib/api/client';
import { useStaffRoles } from '@/lib/hooks/useStaffRoles';
import { useSurgeryProtocols } from '@/lib/hooks/useSurgeryCatalogue';

/** `/staff/surgeries/protocols` — HO-SRG-002 §11.7 (US-SRG-07): fasting and post-op protocols per surgical service. */
export default function SurgeryProtocolsPage() {
  const protocols = useSurgeryProtocols();
  const roles = useStaffRoles();
  if (protocols.error instanceof ApiForbiddenError)
    return <AccessRefusedState error={protocols.error} />;
  return (
    <div className="flex flex-col gap-5">
      <PageHeading
        title="Surgery protocols"
        description="The fasting instructions, post-op instructions and follow-up visits each procedure starts from. A procedure with no protocol of its own uses the defaults."
        actions={
          <Link href="/staff/surgeries" className={buttonVariants({ variant: 'secondary' })}>
            Theatre day list
          </Link>
        }
      />
      <SurgeryProtocolTable
        protocols={protocols.data}
        canEdit={roles.has('Vet', 'Administrator')}
        isLoading={protocols.isPending}
        isError={protocols.isError}
        onRetry={() => void protocols.refetch()}
      />
    </div>
  );
}
