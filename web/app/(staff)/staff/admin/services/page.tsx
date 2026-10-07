'use client';

import { useStaffSession } from '@/lib/hooks/useSession';
import { useAdminServices } from '@/lib/hooks/useAdminCatalogue';
import { ServiceTable } from '@/components/admin/ServiceTable';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { ApiForbiddenError } from '@/lib/api/client';

/** `/staff/admin/services` — US-ADM-04. */
export default function ServicesPage() {
  const { data: session } = useStaffSession();
  const { data, isLoading, isError, error, refetch } = useAdminServices();

  if (error instanceof ApiForbiddenError) return <PermissionDeniedState />;

  return (
    <ServiceTable
      services={data}
      isLoading={isLoading}
      isError={isError}
      onRetry={() => refetch()}
      canManage={(session?.roleIds ?? []).includes('Administrator')}
    />
  );
}
