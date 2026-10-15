'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { VitalsForm } from './VitalsForm';
import { VitalsReadout } from '../VitalsReadout';
import { useRecordVitals } from '@/lib/hooks/useVisit';
import type { VisitDetail } from '@/lib/api/visits';

/**
 * `VitalsPanel` — HO-EMR-002 §11.2. The visit's vitals entries newest first, each sign with its
 * unit and a *"confirmed outside usual range"* marker in words; **Record vitals** only when the
 * server's `allowedActions` has `recordVitals` (ADR-FE-051). The form opens in the panel, so a nurse
 * can add an entry while the vet's note stays untouched in the other column (OQ-1).
 */
export function VitalsPanel({ visit }: { visit: VisitDetail }) {
  const [adding, setAdding] = useState(false);
  const record = useRecordVitals(visit.id, visit.catId);
  const canRecord = visit.allowedActions.includes('recordVitals');
  const entries = [...visit.vitals].sort((a, b) => b.measuredOn.localeCompare(a.measuredOn));

  return (
    <section
      aria-labelledby="vitals-panel-heading"
      className="rounded-[var(--radius-md)] border border-border bg-surface"
    >
      <div className="flex items-center justify-between gap-2 border-b border-border px-4 py-3">
        <h2 id="vitals-panel-heading" className="text-sm font-bold text-ink">
          Vitals
        </h2>
        {canRecord && !adding && (
          <Button variant="secondary" size="sm" onClick={() => setAdding(true)}>
            Record vitals
          </Button>
        )}
      </div>
      <div className="flex flex-col gap-4 p-4">
        {adding && canRecord && (
          <div className="rounded-[var(--radius-sm)] border border-border p-3">
            <VitalsForm
              zone={visit.branchTimeZoneId}
              idPrefix="panel-vitals"
              save={(request) => record.mutateAsync(request)}
              onSaved={() => setAdding(false)}
            />
            <Button variant="ghost" size="sm" className="mt-2" onClick={() => setAdding(false)}>
              Cancel
            </Button>
          </div>
        )}
        {entries.length === 0 ? (
          <p className="text-sm text-ink-muted">No vitals recorded yet.</p>
        ) : (
          entries.map((entry) => (
            <div key={entry.id} className="border-b border-border pb-4 last:border-0 last:pb-0">
              <VitalsReadout entry={entry} zone={visit.branchTimeZoneId} />
            </div>
          ))
        )}
      </div>
    </section>
  );
}
