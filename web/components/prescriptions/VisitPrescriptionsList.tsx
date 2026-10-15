'use client';

import Link from 'next/link';
import { QueryBoundary } from '@/components/inventory/QueryBoundary';
import { Quantity } from '@/components/common/Quantity';
import { useVisitPrescriptions } from '@/lib/hooks/usePrescriptions';
import { formatDay, routeLabel } from '@/lib/format/pharmacy';
import { PrescriptionStatusBadge } from './PrescriptionStatusBadge';

/**
 * `VisitPrescriptionsList` — HO-PHM-002 §8.6. What was prescribed and authorised during one visit, for
 * HO-EMR-002's discharge-summary preview. It takes only a `visitId` and links to the prescription's own
 * page, so it has no route dependency beyond those links (the prescription's cat comes from the API's
 * summary when present; without it the number is plain text).
 */
export function VisitPrescriptionsList({ visitId, catId }: { visitId: string; catId?: string }) {
  const query = useVisitPrescriptions(visitId);
  return (
    <QueryBoundary query={query} loadingRows={2} notFoundTitle="This visit has no prescriptions">
      {(data) =>
        data.prescriptions.length === 0 && data.dietAuthorisations.length === 0 ? (
          <p className="text-sm text-ink-muted">
            Nothing was prescribed or authorised during this visit.
          </p>
        ) : (
          <div className="flex flex-col gap-3">
            {data.prescriptions.length > 0 && (
              <ul
                aria-label="Prescriptions from this visit"
                className="m-0 flex list-none flex-col gap-2 p-0 text-sm"
              >
                {data.prescriptions.map((prescription) => (
                  <li key={prescription.id} className="flex flex-wrap items-center gap-x-3 gap-y-1">
                    {catId ? (
                      <Link
                        href={`/staff/cats/${catId}/prescriptions/${prescription.id}`}
                        className="font-semibold text-accent-ink underline"
                      >
                        {prescription.prescriptionNumber}
                      </Link>
                    ) : (
                      <b>{prescription.prescriptionNumber}</b>
                    )}
                    <span>{prescription.medicine.name}</span>
                    <Quantity value={prescription.dose} />
                    <span>
                      {routeLabel(prescription.route)}, {prescription.frequency}
                    </span>
                    <PrescriptionStatusBadge status={prescription.status} />
                  </li>
                ))}
              </ul>
            )}
            {data.dietAuthorisations.length > 0 && (
              <ul
                aria-label="Diets authorised during this visit"
                className="m-0 flex list-none flex-col gap-1 p-0 text-sm"
              >
                {data.dietAuthorisations.map((diet) => (
                  <li key={diet.id}>
                    {diet.productName} — {formatDay(diet.validFrom)} to {formatDay(diet.validTo)} (
                    {diet.status})
                  </li>
                ))}
              </ul>
            )}
          </div>
        )
      }
    </QueryBoundary>
  );
}
