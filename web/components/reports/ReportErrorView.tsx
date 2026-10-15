'use client';

import Link from 'next/link';
import { TriangleAlert } from 'lucide-react';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { BranchAssignmentBanner } from '@/components/branches/BranchAssignmentBanner';
import { Button } from '@/components/ui/button';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';
import { describeReportError, getBranchAssignmentErrorCode, hasReportCode } from '@/lib/api/errors';

/**
 * `components/reports/ReportErrorView.tsx` — HO-RPT-002 §11.4/§11.7, NFR-USA-4. One place that turns
 * a failed Reporting read into the right surface: a `403` is `PermissionDeniedState` (permission is
 * shown by absence — no hint of what is withheld, ADR-FE-137), a branch refusal is the branch
 * banner, an unknown report links back to the catalogue, and anything else is a page-level banner
 * that says what to do. `errorCode` is read to choose a surface, never rendered.
 */
export function ReportErrorView({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  const branchCode = getBranchAssignmentErrorCode(error);
  if (branchCode) return <BranchAssignmentBanner code={branchCode} />;

  if (
    error instanceof ApiForbiddenError &&
    !hasReportCode(error, 'CrossBranchReportNotPermitted')
  ) {
    return <PermissionDeniedState />;
  }

  const unknownReport = error instanceof ApiNotFoundError || hasReportCode(error, 'UnknownReport');

  return (
    <div
      role="alert"
      className="flex items-start gap-3 rounded-[var(--radius-md)] border border-border bg-warning-bg p-4"
    >
      <TriangleAlert size={18} className="mt-0.5 flex-none text-warning-ink" aria-hidden="true" />
      <div className="min-w-0 flex-1 text-sm text-warning-ink">
        <p className="font-semibold">{describeReportError(error)}</p>
        {unknownReport && (
          <Link href="/staff/reports" className="mt-1 inline-block font-semibold underline">
            All reports
          </Link>
        )}
        {onRetry && !unknownReport && (
          <Button variant="secondary" size="sm" className="mt-2" onClick={onRetry}>
            Try again
          </Button>
        )}
      </div>
    </div>
  );
}
