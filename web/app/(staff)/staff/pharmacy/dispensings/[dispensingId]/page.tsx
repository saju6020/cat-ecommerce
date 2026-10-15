'use client';

import { use } from 'react';
import { QueryBoundary } from '@/components/inventory/QueryBoundary';
import { DispensingDetail } from '@/components/pharmacy/DispensingDetail';
import { toast } from '@/components/ui/use-toast';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { useDispensing, useReverseDispensing } from '@/lib/hooks/usePharmacy';

/** `/staff/pharmacy/dispensings/[dispensingId]` — US-PHM-05: one supply, reprint, reverse. No edit control (ADR-FE-065). */
export default function DispensingPage({ params }: { params: Promise<{ dispensingId: string }> }) {
  const { dispensingId } = use(params);
  const query = useDispensing(dispensingId);
  const reverse = useReverseDispensing(dispensingId);
  const zone = useBranchTimeZone();
  return (
    <QueryBoundary
      query={query}
      loadingRows={6}
      notFoundTitle="This dispensing could not be found"
      notFoundDescription="It may belong to another branch, or the link may be wrong."
    >
      {(dispensing) => (
        <DispensingDetail
          dispensing={dispensing}
          zone={zone}
          catId={dispensing.catId}
          onReverse={async (reason, witness) => {
            await reverse({
              reason,
              ...(witness
                ? { witnessIdentifier: witness.identifier, witnessPassword: witness.password }
                : {}),
            });
            toast({ title: `Supply ${dispensing.supplyNumber} reversed` });
          }}
        />
      )}
    </QueryBoundary>
  );
}
