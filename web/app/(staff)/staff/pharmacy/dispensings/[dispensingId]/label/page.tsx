'use client';

import { use } from 'react';
import { QueryBoundary } from '@/components/inventory/QueryBoundary';
import { LabelPrintView } from '@/components/pharmacy/LabelPrintView';
import { useDispensing, useRecordLabelPrint } from '@/lib/hooks/usePharmacy';

/** `/staff/pharmacy/dispensings/[dispensingId]/label` — US-PHM-05 AC1: the print view of the frozen label (ADR-FE-064). */
export default function LabelPage({ params }: { params: Promise<{ dispensingId: string }> }) {
  const { dispensingId } = use(params);
  const query = useDispensing(dispensingId);
  const record = useRecordLabelPrint(dispensingId);
  return (
    <QueryBoundary
      query={query}
      loadingRows={4}
      notFoundTitle="This label could not be found"
      notFoundDescription="It may belong to another branch, or the link may be wrong."
    >
      {(dispensing) => (
        <LabelPrintView dispensing={dispensing} onPrinted={() => record.mutateAsync()} />
      )}
    </QueryBoundary>
  );
}
