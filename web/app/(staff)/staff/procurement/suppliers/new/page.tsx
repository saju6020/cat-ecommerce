'use client';

import { useRouter } from 'next/navigation';
import { PageHeading } from '@/components/inventory/fields';
import { SupplierForm } from '@/components/procurement/SupplierForm';
import { useCreateSupplier } from '@/lib/hooks/useProcurement';

/** `/staff/procurement/suppliers/new` — US-IMP-01. */
export default function NewSupplierPage() {
  const router = useRouter();
  const create = useCreateSupplier();
  return (
    <div className="flex flex-col gap-4">
      <PageHeading title="New supplier" />
      <SupplierForm
        submitLabel="Create supplier"
        isPending={create.isPending}
        onSubmit={async (values) => {
          const created = await create.mutateAsync(values);
          router.push(`/staff/procurement/suppliers/${created.supplierId}`);
        }}
      />
    </div>
  );
}
