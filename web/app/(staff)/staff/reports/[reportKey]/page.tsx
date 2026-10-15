'use client';

import { Suspense } from 'react';
import { useParams } from 'next/navigation';
import { LoadingState } from '@/components/common/LoadingState';
import { ReportViewer } from '@/components/reports/ReportViewer';

/**
 * `/staff/reports/[reportKey]` — ⭐ HO-RPT-002 §11.3, ADR-FE-129. The generic report viewer: one
 * screen for every report, driven by the catalogue. All filters live in the URL search params.
 */
export default function ReportPage() {
  const params = useParams<{ reportKey: string }>();
  return (
    <Suspense fallback={<LoadingState variant="card" rows={6} label="Loading report…" />}>
      <ReportViewer reportKey={decodeURIComponent(params.reportKey)} />
    </Suspense>
  );
}
