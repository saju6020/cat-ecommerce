'use client';

import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { QuoteBreakdown } from '@/components/boarding/shared/QuoteBreakdown';
import { UNIT_TYPE_LABEL } from '@/components/boarding/shared/labels';
import type { AvailableUnitType, BoardingAvailability, BoardingUnitType } from '@/lib/api/types';
import { formatMoney } from '@/lib/format/money';
import { formatShortDate, formatStayDates } from '@/lib/format/boarding';

/**
 * `components/boarding/booking/UnitTypeResults.tsx` — HO-BRD-002 §11.1 step 2, US-BRD-02/03: one
 * card per type from `GET /api/boarding/availability` — name, capacity, how many are free, the
 * **itemised quote** (`QuoteBreakdown`) with the discount as its own line. A type that is too small
 * for the cats is disabled **with the reason**; a full range says so and offers the nearest dates
 * and the waitlist (US-BRD-02 AC3). Everything shown is the server's answer (ADR-FE-105).
 */
export interface UnitTypeResultsProps {
  availability: BoardingAvailability;
  catCount: number;
  selectedType?: BoardingUnitType | null;
  onChoose: (type: AvailableUnitType) => void;
  onPickAlternative: (alternative: BoardingAvailability['alternatives'][number]) => void;
  onJoinWaitlist: () => void;
  /** Staff mode lists the free units of each type. */
  staff?: boolean;
}

export function UnitTypeResults({
  availability,
  catCount,
  selectedType,
  onChoose,
  onPickAlternative,
  onJoinWaitlist,
  staff,
}: UnitTypeResultsProps) {
  const anyFree = availability.unitTypes.some((t) => t.freeUnitCount > 0);

  return (
    <section aria-labelledby="results-heading" className="flex flex-col gap-4">
      <h2 id="results-heading" className="font-display text-lg font-semibold text-ink">
        Available units · {availability.nights} night{availability.nights === 1 ? '' : 's'}
      </h2>

      {!anyFree && (
        <div
          role="status"
          className="rounded-[var(--radius-md)] border border-border bg-surface p-5"
        >
          <p className="text-[15px] font-semibold text-ink">No units free for those dates.</p>
          {availability.alternatives.length > 0 && (
            <div className="mt-3">
              <p className="text-sm text-ink-muted">These nearby dates have a unit free:</p>
              <NearestDatesList
                alternatives={availability.alternatives}
                onPick={onPickAlternative}
              />
            </div>
          )}
          {availability.waitlistAvailable && (
            <div className="mt-4">
              <p className="mb-2 text-sm text-ink-muted">
                Or ask us to tell you if a unit frees up for these dates.
              </p>
              <Button variant="secondary" onClick={onJoinWaitlist}>
                Join the waitlist
              </Button>
            </div>
          )}
        </div>
      )}

      <ul className="grid gap-4 md:grid-cols-2" aria-label="Unit types">
        {availability.unitTypes.map((type) => {
          const tooSmall = type.capacityCats < catCount;
          const full = type.freeUnitCount === 0;
          const disabled = tooSmall || full;
          const reason = tooSmall
            ? `A ${UNIT_TYPE_LABEL[type.unitType]} unit holds up to ${type.capacityCats} cat${type.capacityCats === 1 ? '' : 's'}; you are booking ${catCount}.`
            : full
              ? 'None free for these dates.'
              : null;
          return (
            <li
              key={type.unitType}
              className="flex flex-col gap-3 rounded-[var(--radius-md)] border border-border bg-surface p-5"
            >
              <div className="flex flex-wrap items-center justify-between gap-2">
                <h3 className="font-display text-base font-semibold text-ink">
                  {UNIT_TYPE_LABEL[type.unitType]}
                </h3>
                <Badge variant={full ? 'danger' : 'success'}>
                  {full ? 'Full' : `${type.freeUnitCount} free`}
                </Badge>
              </div>
              <p className="text-sm text-ink-muted">
                Holds up to {type.capacityCats} cat{type.capacityCats === 1 ? '' : 's'}
              </p>
              {staff && type.freeUnits && type.freeUnits.length > 0 && (
                <p className="text-sm text-ink">
                  Free: {type.freeUnits.map((u) => u.code).join(', ')}
                </p>
              )}
              <QuoteBreakdown quote={type.quote} />
              <div>
                <Button
                  disabled={disabled}
                  aria-describedby={reason ? `why-${type.unitType}` : undefined}
                  variant={selectedType === type.unitType ? 'secondary' : 'primary'}
                  onClick={() => onChoose(type)}
                >
                  {selectedType === type.unitType
                    ? `${UNIT_TYPE_LABEL[type.unitType]} chosen`
                    : `Choose ${UNIT_TYPE_LABEL[type.unitType]} · ${formatMoney(type.quote.total)}`}
                </Button>
                {reason && (
                  <p id={`why-${type.unitType}`} className="mt-1.5 text-xs text-ink-muted">
                    {reason}
                  </p>
                )}
              </div>
            </li>
          );
        })}
      </ul>
    </section>
  );
}

export function NearestDatesList({
  alternatives,
  onPick,
}: {
  alternatives: BoardingAvailability['alternatives'];
  onPick: (alternative: BoardingAvailability['alternatives'][number]) => void;
}) {
  return (
    <ul aria-label="Nearest dates" className="mt-2 flex flex-col gap-2">
      {alternatives.map((alt) => (
        <li
          key={`${alt.arrivalOn}-${alt.departureOn}`}
          className="flex flex-wrap items-center justify-between gap-3 rounded-[var(--radius-sm)] border border-border px-3 py-2"
        >
          <span className="text-sm text-ink">
            <strong>
              {formatShortDate(alt.arrivalOn)} – {formatShortDate(alt.departureOn)}
            </strong>{' '}
            <span className="text-ink-muted">
              · {UNIT_TYPE_LABEL[alt.unitType]} from {formatMoney(alt.total)}
            </span>
          </span>
          <Button
            size="sm"
            variant="secondary"
            onClick={() => onPick(alt)}
            aria-label={`Search ${formatStayDates(alt.arrivalOn, alt.departureOn)}`}
          >
            Search these dates
          </Button>
        </li>
      ))}
    </ul>
  );
}
