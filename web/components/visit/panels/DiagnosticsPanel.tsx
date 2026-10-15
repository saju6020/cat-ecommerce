'use client';

import { useState } from 'react';
import { TriangleAlert } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { SideSheet } from '../SideSheet';
import { DiagnosticOrderView } from './DiagnosticOrderView';
import { OrderDiagnosticDialog } from './OrderDiagnosticDialog';
import { useOrderDiagnostic } from '@/lib/hooks/useDiagnostics';
import { diagnosticKindLabel, diagnosticStatusLabel } from '@/lib/format/clinical';
import { formatInZone } from '@/lib/format/time';
import type { VisitDetail } from '@/lib/api/visits';

/**
 * `DiagnosticsPanel` — HO-EMR-002 §11.3 (US-CLN-14). The visit's orders with their status and — from the
 * visit payload's own summary — whether any result is abnormal, in words with an icon. **Order test**
 * appears only with `orderDiagnostic` in `allowedActions`. A row opens `DiagnosticOrderDrawer` over the
 * workspace (ADR-FE-049): the note form stays mounted, so looking at a result can never cost the draft.
 * Result entry inside the drawer is offered when the caller may write (`canWriteResults`).
 */
export function DiagnosticsPanel({
  visit,
  canWriteResults,
}: {
  visit: VisitDetail;
  canWriteResults: boolean;
}) {
  const [orderOpen, setOrderOpen] = useState(false);
  const [openOrderId, setOpenOrderId] = useState<string | null>(null);
  const place = useOrderDiagnostic(visit.id, visit.catId);
  const zone = visit.branchTimeZoneId;
  const pending = visit.diagnostics.filter((d) => d.status === 'Pending').length;

  return (
    <section
      aria-labelledby="diagnostics-heading"
      className="rounded-[var(--radius-md)] border border-border bg-surface"
    >
      <div className="flex items-center justify-between gap-2 border-b border-border px-4 py-3">
        <h2 id="diagnostics-heading" className="text-sm font-bold text-ink">
          Diagnostics
        </h2>
        {visit.allowedActions.includes('orderDiagnostic') && (
          <Button variant="secondary" size="sm" onClick={() => setOrderOpen(true)}>
            Order test
          </Button>
        )}
      </div>
      <div className="p-4">
        {visit.diagnostics.length === 0 ? (
          <p className="text-sm text-ink-muted">No tests ordered for this visit.</p>
        ) : (
          <>
            {pending > 0 && (
              <p className="mb-2 text-xs text-ink-muted">
                {pending} {pending === 1 ? 'test is' : 'tests are'} waiting for results.
              </p>
            )}
            <ul className="m-0 flex list-none flex-col divide-y divide-border p-0">
              {visit.diagnostics.map((order) => (
                <li key={order.id} className="py-2 first:pt-0 last:pb-0">
                  <button
                    type="button"
                    onClick={() => setOpenOrderId(order.id)}
                    className="flex w-full flex-col items-start gap-0.5 rounded-[var(--radius-sm)] p-1 text-left hover:bg-surface-sunken focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
                  >
                    <span className="text-sm font-semibold text-ink">
                      {diagnosticKindLabel(order.kind)}
                    </span>
                    <span className="text-xs text-ink-muted">
                      {diagnosticStatusLabel(order.status)} · ordered{' '}
                      {formatInZone(order.orderedOn, zone, { timeStyle: 'short' })}
                    </span>
                    {order.hasAbnormal && (
                      <span className="inline-flex items-center gap-1 text-xs font-semibold text-warning-ink">
                        <TriangleAlert size={12} aria-hidden="true" /> Has abnormal results
                      </span>
                    )}
                  </button>
                </li>
              ))}
            </ul>
          </>
        )}
      </div>
      <OrderDiagnosticDialog
        open={orderOpen}
        onOpenChange={setOrderOpen}
        catName={visit.catName}
        order={(request) => place.mutateAsync(request)}
      />
      <SideSheet
        open={openOrderId !== null}
        onOpenChange={(next) => !next && setOpenOrderId(null)}
        title="Diagnostic order"
      >
        {openOrderId && (
          <DiagnosticOrderView orderId={openOrderId} canWrite={canWriteResults} zone={zone} />
        )}
      </SideSheet>
    </section>
  );
}
