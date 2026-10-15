'use client';

import { use, useState } from 'react';
import Link from 'next/link';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ActionDialog } from '@/components/inventory/ReasonDialog';
import { PageHeading } from '@/components/inventory/fields';
import { QueryBoundary } from '@/components/inventory/QueryBoundary';
import { SupplierForm } from '@/components/procurement/SupplierForm';
import { useSetSupplierActivation, useSupplier, useUpdateSupplier } from '@/lib/hooks/useProcurement';

/** `/staff/procurement/suppliers/[supplierId]` — US-IMP-01: edit, deactivate with confirm. */
export default function SupplierPage({ params }: { params: Promise<{ supplierId: string }> }) {
  const { supplierId } = use(params);
  const query = useSupplier(supplierId);
  const update = useUpdateSupplier(supplierId);
  const activation = useSetSupplierActivation(supplierId);
  const [confirming, setConfirming] = useState(false);

  return (
    <QueryBoundary query={query} notFoundTitle="This supplier could not be found">
      {(supplier) => (
        <div className="flex flex-col gap-4">
          <PageHeading
            title={supplier.name}
            description={
              <span className="flex flex-wrap items-center gap-2">
                <span>{supplier.code}</span>
                <Badge variant={supplier.isActive ? 'success' : 'neutral'}>
                  {supplier.isActive ? 'Active' : 'Inactive'}
                </Badge>
              </span>
            }
            actions={
              <>
                <Link href={`/staff/procurement/purchase-orders/new?supplierId=${supplier.id}`} className="text-sm font-semibold text-accent-ink">
                  New purchase order
                </Link>
                <Button
                  variant={supplier.isActive ? 'secondary' : 'primary'}
                  onClick={() => (supplier.isActive ? setConfirming(true) : void activation.mutateAsync(true))}
                >
                  {supplier.isActive ? 'Deactivate' : 'Reactivate'}
                </Button>
              </>
            }
          />
          <SupplierForm
            initial={supplier}
            submitLabel="Save changes"
            isPending={update.isPending}
            onSubmit={(values) =>
              update.mutateAsync({
                name: values.name,
                countryCode: values.countryCode,
                contactName: values.contactName,
                phone: values.phone,
                email: values.email,
                address: values.address,
                currencyCode: values.currencyCode,
                leadTimeDays: values.leadTimeDays,
                paymentTerms: values.paymentTerms,
                paymentTermDays: values.paymentTermDays,
                notes: values.notes,
              })
            }
          />
          <ActionDialog
            open={confirming}
            onOpenChange={setConfirming}
            title={`Deactivate ${supplier.name}`}
            consequence="New purchase orders can no longer be raised with this supplier. Open orders already issued are not touched. You can reactivate it at any time."
            confirmLabel="Deactivate supplier"
            variant="destructive"
            onConfirm={() => activation.mutateAsync(false)}
          />
        </div>
      )}
    </QueryBoundary>
  );
}
