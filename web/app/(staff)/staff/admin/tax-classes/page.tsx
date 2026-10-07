'use client';

import { useStaffSession } from '@/lib/hooks/useSession';
import { useTaxClasses, useCreateTaxClass } from '@/lib/hooks/useAdminCatalogue';
import { TaxClassTable } from '@/components/admin/TaxClassTable';
import { TaxClassForm } from '@/components/admin/TaxClassForm';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { ApiForbiddenError } from '@/lib/api/client';

/** `/staff/admin/tax-classes` — US-ADM-04. */
export default function TaxClassesPage() {
  const { data: session } = useStaffSession();
  const { data, isLoading, isError, error, refetch } = useTaxClasses();
  const createMutation = useCreateTaxClass();
  const canManage = (session?.roleIds ?? []).includes('Administrator');

  if (error instanceof ApiForbiddenError) return <PermissionDeniedState />;

  return (
    <div className="flex flex-col gap-8">
      <h1 className="font-display text-xl font-semibold text-ink">Tax classes</h1>
      {canManage && (
        <TaxClassForm
          isPending={createMutation.isPending}
          onSave={(values) => createMutation.mutateAsync({ code: values.code!, name: values.name })}
        />
      )}
      <TaxClassTable
        taxClasses={data}
        isLoading={isLoading}
        isError={isError}
        onRetry={() => refetch()}
      />
    </div>
  );
}
