import { Suspense } from 'react';
import { LoadingState } from '@/components/common/LoadingState';
import { LabAnalyteManager } from '@/components/clinical-catalogue/LabAnalyteManager';

/** `/staff/admin/clinical/lab-analytes` — HO-EMR-002 §8.1/§11.8: the clinical catalogue of lab analytes. */
export default function LabAnalyteManagerPage() {
  return (
    <Suspense fallback={<LoadingState variant="table" rows={5} label="Loading the catalogue…" />}>
      <LabAnalyteManager />
    </Suspense>
  );
}
