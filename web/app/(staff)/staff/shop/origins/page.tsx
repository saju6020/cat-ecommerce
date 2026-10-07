'use client';

import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { OriginsTable } from '@/components/staff-shop/OriginsTable';
import { ApiForbiddenError } from '@/lib/api/client';
import { useSaveOrigin, useStaffCountries, useStaffOrigins } from '@/lib/hooks/useStaffShop';

/** `/staff/shop/origins` — sourcing origins (US-SHP-05, staff). */
export default function StaffOriginsPage() {
  const origins = useStaffOrigins();
  const countries = useStaffCountries();
  const save = useSaveOrigin();
  if (origins.error instanceof ApiForbiddenError) return <PermissionDeniedState />;
  if (origins.isError) return <ErrorState onRetry={() => origins.refetch()} />;
  if (!origins.data || !countries.data) return <LoadingState rows={5} variant="table" />;
  return (
    <OriginsTable
      origins={origins.data}
      countries={countries.data}
      onSave={(values) => save.mutateAsync(values)}
    />
  );
}
