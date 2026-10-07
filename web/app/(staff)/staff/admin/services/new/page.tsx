'use client';

import { useRouter } from 'next/navigation';
import { ServiceForm } from '@/components/admin/ServiceForm';
import { useCreateClinicService, useTaxClasses } from '@/lib/hooks/useAdminCatalogue';
import { useStaffRoleCatalogue } from '@/lib/hooks/useStaffAdmin';

/** `/staff/admin/services/new` — US-ADM-04. */
export default function NewServicePage() {
  const router = useRouter();
  const createMutation = useCreateClinicService();
  const roleCatalogue = useStaffRoleCatalogue();
  const taxClasses = useTaxClasses();

  return (
    <div className="flex flex-col gap-4">
      <h1 className="font-display text-xl font-semibold text-ink">New service</h1>
      <ServiceForm
        roleCatalogue={roleCatalogue}
        taxClasses={taxClasses}
        isPending={createMutation.isPending}
        onSave={async (values) => {
          const result = await createMutation.mutateAsync({ ...values, code: values.code! });
          router.push(`/staff/admin/services/${result.serviceId}`);
        }}
      />
    </div>
  );
}
