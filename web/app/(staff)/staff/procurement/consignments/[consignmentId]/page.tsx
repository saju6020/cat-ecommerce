'use client';

import { use, useState } from 'react';
import { Button } from '@/components/ui/button';
import { ReasonDialog } from '@/components/inventory/ReasonDialog';
import { Panel } from '@/components/inventory/fields';
import { QueryBoundary } from '@/components/inventory/QueryBoundary';
import { ConsignmentForm, newHsRow } from '@/components/procurement/ConsignmentForm';
import { ConsignmentStatusStepper } from '@/components/procurement/ConsignmentStatusStepper';
import { describeInventoryError, hasViolationCode } from '@/lib/api/errors';
import { todayInZone } from '@/lib/format/time';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { useConsignment, useConsignmentActions, usePurchaseOrders } from '@/lib/hooks/useProcurement';

/** `/staff/procurement/consignments/[consignmentId]` — Details tab (US-IMP-03): the status
 *  stepper, the editable details (with the version it was loaded at) and cancel. */
export default function ConsignmentDetailsPage({ params }: { params: Promise<{ consignmentId: string }> }) {
  const { consignmentId } = use(params);
  const query = useConsignment(consignmentId);
  const actions = useConsignmentActions(consignmentId);
  const orders = usePurchaseOrders({ pageSize: 100 });
  const zone = useBranchTimeZone();
  const [clearanceError, setClearanceError] = useState<string | null>(null);
  const [cancelling, setCancelling] = useState(false);

  return (
    <QueryBoundary query={query}>
      {(c) => {
        const editable = c.allowedActions.includes('update');
        return (
          <div className="flex flex-col gap-5">
            <Panel title="Progress">
              <ConsignmentStatusStepper
                status={c.status}
                allowedActions={c.allowedActions}
                today={todayInZone(zone)}
                onAdvance={async (to, on) => {
                  try {
                    await actions.advance.mutateAsync({ to, on });
                    setClearanceError(null);
                  } catch (error) {
                    if (hasViolationCode(error, 'ClearanceDocumentsMissing')) setClearanceError(describeInventoryError(error));
                    throw error;
                  }
                }}
              />
              {c.cancellationReason && <p className="mt-3 text-sm text-ink-muted">Cancelled: {c.cancellationReason}</p>}
            </Panel>
            <Panel
              title="Details"
              actions={
                c.allowedActions.includes('cancel') ? (
                  <Button variant="secondary" size="sm" onClick={() => setCancelling(true)}>
                    Cancel consignment
                  </Button>
                ) : undefined
              }
            >
              <ConsignmentForm
                key={c.version}
                initial={{
                  originCountryCode: c.originCountryCode,
                  shipmentReference: c.shipmentReference,
                  purchaseOrderIds: c.purchaseOrders.map((p) => p.id),
                  hsCodes: c.hsCodes.map((h) => newHsRow(h.code, h.description ?? '')),
                  importPermitNumber: c.importPermitNumber ?? '',
                  healthCertificateNumber: c.healthCertificateNumber ?? '',
                  customsDeclarationNumber: c.customsDeclarationNumber ?? '',
                  expectedArrivalOn: c.expectedArrivalOn ?? '',
                }}
                orders={orders.data?.items ?? []}
                readOnly={!editable}
                clearanceError={clearanceError}
                submitLabel="Save details"
                isPending={actions.update.isPending}
                onReload={() => void query.refetch()}
                onSubmit={(request) => actions.update.mutateAsync({ ...request, expectedVersion: c.version }).then(() => undefined)}
              />
            </Panel>
            <ReasonDialog
              open={cancelling}
              onOpenChange={setCancelling}
              title={`Cancel ${c.reference}`}
              consequence="The consignment is cancelled and nothing can be received against it. This cannot be undone."
              reasonLabel="Reason for cancelling"
              confirmLabel="Cancel consignment"
              variant="destructive"
              onConfirm={(reason) => actions.cancel.mutateAsync(reason)}
            />
          </div>
        );
      }}
    </QueryBoundary>
  );
}
