'use client';

import { Button } from '@/components/ui/button';
import { VaccinationStatusBadge } from '@/components/cat/VaccinationStatusBadge';
import { WeightTrendChart } from '@/components/cat/WeightTrendChart';
import { VitalsReadout } from '@/components/visit/VitalsReadout';
import { HistoryItemRow } from './HistoryItem';
import { diagnosticKindLabel } from '@/lib/format/clinical';
import { formatDateOnly, formatInZone } from '@/lib/format/time';
import type { ClinicalHistorySummary } from '@/lib/api/clinical-history';
import type { VaccinationStatus } from '@/lib/api/cats';

/**
 * `HistoryOverview` — HO-EMR-002 §11.4 (US-CLN-13). Everything from the ONE summary call, rendered
 * without any section request: active problems, the last five visits (each opens nested and
 * read-only), the last vitals with units, the weight trend (a chart AND, inside `WeightTrendChart`, a
 * table alternative for screen readers — U-F3), the vaccination status badge and the pending lab
 * orders. ADR-FE-056's NFR-PRF-4 budget (≤ 2 s p95) is for THIS view.
 */
export function HistoryOverview({
  summary,
  zone,
  onOpenVisit,
  onOpenOrder,
}: {
  summary: ClinicalHistorySummary;
  zone: string;
  onOpenVisit: (visitId: string) => void;
  onOpenOrder: (orderId: string) => void;
}) {
  const heading = 'text-sm font-bold uppercase tracking-wide text-ink-muted';
  return (
    <div className="flex flex-col gap-6">
      <section aria-labelledby="ho-problems" className="flex flex-col gap-2">
        <h3 id="ho-problems" className={heading}>
          Active problems
        </h3>
        {summary.activeProblems.length === 0 ? (
          <p className="text-sm text-ink-muted">None in the last 12 months.</p>
        ) : (
          <ul className="m-0 flex list-none flex-col gap-1 p-0 text-sm">
            {summary.activeProblems.map((p) => (
              <li key={p.diagnosisCodeId}>
                <span className="font-semibold text-ink">
                  {p.code} — {p.name}
                </span>{' '}
                <span className="text-ink-muted">
                  · {p.certainty} · last seen{' '}
                  {formatInZone(p.lastSeen, zone, { dateStyle: 'medium' })}
                </span>
              </li>
            ))}
          </ul>
        )}
      </section>

      <section aria-labelledby="ho-visits" className="flex flex-col gap-1">
        <h3 id="ho-visits" className={heading}>
          Last visits
        </h3>
        {summary.recentVisits.length === 0 ? (
          <p className="text-sm text-ink-muted">No visits recorded.</p>
        ) : (
          <ul className="m-0 list-none p-0">
            {summary.recentVisits.map((item) => (
              <HistoryItemRow key={item.id} item={item} zone={zone} onOpenVisit={onOpenVisit} />
            ))}
          </ul>
        )}
      </section>

      <section aria-labelledby="ho-vitals" className="flex flex-col gap-2">
        <h3 id="ho-vitals" className={heading}>
          Last vitals
        </h3>
        {summary.lastVitals ? (
          <VitalsReadout entry={summary.lastVitals} zone={zone} />
        ) : (
          <p className="text-sm text-ink-muted">No vitals recorded.</p>
        )}
      </section>

      <section aria-labelledby="ho-weight" className="flex flex-col gap-2">
        <h3 id="ho-weight" className={heading}>
          Weight trend
        </h3>
        {summary.weightSeries.length === 0 ? (
          <p className="text-sm text-ink-muted">No weights recorded.</p>
        ) : (
          <WeightTrendChart points={summary.weightSeries} />
        )}
      </section>

      <section aria-labelledby="ho-vacc" className="flex flex-col gap-2">
        <h3 id="ho-vacc" className={heading}>
          Vaccinations
        </h3>
        {summary.vaccinationStatus ? (
          <p className="flex flex-wrap items-center gap-2 text-sm text-ink">
            <VaccinationStatusBadge
              status={summary.vaccinationStatus.overallStatus as VaccinationStatus}
            />
            {summary.vaccinationStatus.earliestNextDueOn && (
              <span className="text-ink-muted">
                next due {formatDateOnly(summary.vaccinationStatus.earliestNextDueOn)}
              </span>
            )}
          </p>
        ) : (
          <p className="text-sm text-ink-muted">Not available.</p>
        )}
      </section>

      <section aria-labelledby="ho-pending" className="flex flex-col gap-2">
        <h3 id="ho-pending" className={heading}>
          Pending lab orders
        </h3>
        {summary.pendingDiagnostics.length === 0 ? (
          <p className="text-sm text-ink-muted">None waiting.</p>
        ) : (
          <ul className="m-0 flex list-none flex-col gap-2 p-0">
            {summary.pendingDiagnostics.map((order) => (
              <li
                key={order.id}
                className="flex flex-wrap items-center justify-between gap-2 text-sm"
              >
                <span className="text-ink">
                  {diagnosticKindLabel(order.kind)} · ordered{' '}
                  {formatInZone(order.orderedOn, zone, { dateStyle: 'medium' })}
                </span>
                <Button
                  variant="secondary"
                  size="sm"
                  onClick={() => onOpenOrder(order.id)}
                  aria-label={`Open the ${diagnosticKindLabel(order.kind)} order`}
                >
                  Open order
                </Button>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}
