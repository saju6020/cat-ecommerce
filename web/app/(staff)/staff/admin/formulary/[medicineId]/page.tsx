'use client';

import { use } from 'react';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { QueryBoundary } from '@/components/inventory/QueryBoundary';
import { Banner, PageHeading } from '@/components/inventory/fields';
import { FormularyMedicineForm } from '@/components/formulary/FormularyMedicineForm';
import { ActivationToggle, ControlledToggle } from '@/components/formulary/MedicineActions';
import { toast } from '@/components/ui/use-toast';
import {
  useFormularyMedicine,
  useSetMedicineActive,
  useSetMedicineControlled,
  useUpdateMedicine,
} from '@/lib/hooks/useFormulary';
import { useStaffRoles } from '@/lib/hooks/useStaffRoles';

/** `/staff/admin/formulary/[medicineId]` — US-PHM-04: edit, controlled flag and activation (each its own action). */
export default function MedicinePage({ params }: { params: Promise<{ medicineId: string }> }) {
  const { medicineId } = use(params);
  const roles = useStaffRoles();
  const query = useFormularyMedicine(medicineId);
  const update = useUpdateMedicine(medicineId);
  const setControlled = useSetMedicineControlled(medicineId);
  const setActive = useSetMedicineActive(medicineId);
  const canManage = roles.has('Administrator');

  return (
    <QueryBoundary query={query} loadingRows={6} notFoundTitle="This medicine could not be found">
      {(medicine) => (
        <div className="flex flex-col gap-5">
          <PageHeading
            title={medicine.name}
            description={`${medicine.code} · ${medicine.strength.text}`}
          />
          {!canManage && roles.ready && (
            <Banner tone="neutral">
              Only an administrator can change the formulary. You can read it here.
            </Banner>
          )}
          {canManage ? (
            <>
              <div className="grid gap-4 sm:grid-cols-2">
                <ControlledToggle
                  medicine={medicine}
                  onSet={async (input) => {
                    await setControlled.mutateAsync(input);
                    toast({
                      title: input.isControlled
                        ? 'Marked as controlled'
                        : 'Controlled flag cleared',
                    });
                  }}
                />
                <ActivationToggle
                  medicine={medicine}
                  onSet={async (isActive) => {
                    await setActive.mutateAsync(isActive);
                    toast({ title: isActive ? 'Medicine activated' : 'Medicine deactivated' });
                  }}
                />
              </div>
              <FormularyMedicineForm
                key={medicine.version}
                initial={medicine}
                onReload={() => void query.refetch()}
                onSubmit={async (input) => {
                  await update.mutateAsync(input);
                  toast({ title: 'Medicine saved' });
                }}
              />
            </>
          ) : (
            <PermissionDeniedState />
          )}
        </div>
      )}
    </QueryBoundary>
  );
}
