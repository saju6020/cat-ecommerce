'use client';

import { useRouter } from 'next/navigation';
import { PageHeading } from '@/components/inventory/fields';
import { ConsignmentForm } from '@/components/procurement/ConsignmentForm';
import { usePlanConsignment, usePurchaseOrders } from '@/lib/hooks/useProcurement';

/** `/staff/procurement/consignments/new` — US-IMP-03: plan a consignment. */
export default function NewConsignmentPage() {
  const router = useRouter();
  const plan = usePlanConsignment();
  const orders = usePurchaseOrders({ status: 'Issued', pageSize: 100 });
  return (
    <div className="flex flex-col gap-4">
      <PageHeading
        title="Plan a consignment"
        description="Record what is coming and which orders it covers. Documents and costs are added on the consignment itself."
      />
      <ConsignmentForm
        initial={{
          originCountryCode: '',
          shipmentReference: '',
          purchaseOrderIds: [],
          hsCodes: [],
          importPermitNumber: '',
          healthCertificateNumber: '',
          customsDeclarationNumber: '',
          expectedArrivalOn: '',
        }}
        orders={orders.data?.items ?? []}
        submitLabel="Plan consignment"
        isPending={plan.isPending}
        onSubmit={async (request) => {
          const created = await plan.mutateAsync(request);
          router.push(`/staff/procurement/consignments/${created.consignmentId}`);
        }}
      />
    </div>
  );
}
