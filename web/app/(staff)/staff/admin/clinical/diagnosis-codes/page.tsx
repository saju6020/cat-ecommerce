import { Suspense } from 'react';
import { LoadingState } from '@/components/common/LoadingState';
import { DiagnosisCodeManager } from '@/components/clinical-catalogue/DiagnosisCodeManager';

/** `/staff/admin/clinical/diagnosis-codes` — HO-EMR-002 §8.1/§11.8: the clinical catalogue of diagnosis codes. */
export default function DiagnosisCodeManagerPage() {
  return (
    <Suspense fallback={<LoadingState variant="table" rows={5} label="Loading the catalogue…" />}>
      <DiagnosisCodeManager />
    </Suspense>
  );
}
