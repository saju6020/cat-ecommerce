'use client';

import { useRouter } from 'next/navigation';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { FormularyMedicineForm } from '@/components/formulary/FormularyMedicineForm';
import { PageHeading } from '@/components/inventory/fields';
import { toast } from '@/components/ui/use-toast';
import { useCreateMedicine } from '@/lib/hooks/useFormulary';
import { useStaffRoles } from '@/lib/hooks/useStaffRoles';

/** `/staff/admin/formulary/new` — US-PHM-04: create a medicine (Formulary.Manage). */
export default function NewMedicinePage() {
  const router = useRouter();
  const roles = useStaffRoles();
  const create = useCreateMedicine();
  if (roles.ready && !roles.has('Administrator')) return <PermissionDeniedState />;
  return (
    <div className="flex flex-col gap-4">
      <PageHeading
        title="New medicine"
        description="Add the medicine, its strength, routes and dose range. Mark it controlled afterwards, as a separate step."
      />
      <FormularyMedicineForm
        onSubmit={async (input) => {
          const created = await create.mutateAsync(input);
          toast({ title: 'Medicine created', description: input.name });
          return created;
        }}
        onSaved={(created) =>
          router.push(`/staff/admin/formulary/${(created as { medicineId: string }).medicineId}`)
        }
      />
    </div>
  );
}
