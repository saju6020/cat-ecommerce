'use client';

import Link from 'next/link';
import { Printer } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { CatHeaderCompact } from '@/components/boarding/shared/CatHeaderCompact';
import { QueryGate } from '@/components/boarding/shared/QueryGate';
import {
  BELONGING_KIND_LABEL,
  INCIDENT_KIND_LABEL,
  LITTER_OUTPUT_LABEL,
  MOOD_LABEL,
  PORTION_SPOKEN,
  WATER_LABEL,
} from '@/components/boarding/shared/labels';
import type { StaySummary, StaySummaryCat } from '@/lib/api/types';
import { formatBranchInstant, formatFullDate, formatStayDates } from '@/lib/format/boarding';
import { useMyStaySummary } from '@/lib/hooks/useMyBoarding';
import { portalStayHref } from '@/lib/routing/boarding-links';
import { StayCharges } from './StayCharges';

function CatSummary({ cat, zone }: { cat: StaySummaryCat; zone: string }) {
  return (
    <section
      aria-label={`${cat.catName}’s stay`}
      className="flex flex-col gap-4 rounded-[var(--radius-md)] border border-border bg-surface p-5 print:break-inside-avoid"
    >
      <CatHeaderCompact
        name={cat.catName}
        isHighRisk={cat.catIsHighRisk}
        handlingNote={cat.handlingNote}
      />
      <dl className="grid gap-x-6 gap-y-2 text-sm sm:grid-cols-[11rem_1fr]">
        <dt className="font-semibold text-ink-muted">Weight at check-in</dt>
        <dd>{cat.weightAtCheckInKg ? `${cat.weightAtCheckInKg} kg` : 'Not recorded'}</dd>
        <dt className="font-semibold text-ink-muted">Weight at check-out</dt>
        <dd>
          {cat.weightAtCheckOutKg ? `${cat.weightAtCheckOutKg} kg` : 'Not recorded'}
          {cat.weightChangeKg ? ` (change ${cat.weightChangeKg} kg)` : ''}
        </dd>
        <dt className="font-semibold text-ink-muted">Signed out by</dt>
        <dd>
          {cat.checkOutSignature
            ? `${cat.checkOutSignature.signerName}, ${formatBranchInstant(cat.checkOutSignature.signedOn, zone)} ${cat.checkOutSignature.method ? ` (${cat.checkOutSignature.method === 'Typed' ? 'typed name' : 'drawn signature'})` : ''}`
            : 'Not signed yet'}
        </dd>
      </dl>

      <div>
        <h3 className="mb-1 font-display text-base font-semibold text-ink">Daily log</h3>
        {cat.dailyLogs.length === 0 ? (
          <p className="text-sm text-ink-muted">No daily entries were recorded.</p>
        ) : (
          <ul className="flex flex-col gap-1 text-sm">
            {cat.dailyLogs.map((log) => (
              <li key={log.date}>
                <strong>{formatFullDate(log.date)}</strong>:{' '}
                {log.observations
                  .map(
                    (o) =>
                      `${PORTION_SPOKEN[o.portionEaten].toLowerCase()}, water ${WATER_LABEL[o.waterIntake].toLowerCase()}, litter ${LITTER_OUTPUT_LABEL[o.litterOutput].toLowerCase()}, ${MOOD_LABEL[o.mood].toLowerCase()}`,
                  )
                  .join('; ')}
              </li>
            ))}
          </ul>
        )}
      </div>

      <div>
        <h3 className="mb-1 font-display text-base font-semibold text-ink">Incidents</h3>
        {cat.incidents.length === 0 ? (
          <p className="text-sm text-ink-muted">No incidents.</p>
        ) : (
          <ul className="list-disc pl-5 text-sm">
            {cat.incidents.map((i) => (
              <li key={i.id}>
                {INCIDENT_KIND_LABEL[i.kind]} ({i.severity.toLowerCase()} severity),{' '}
                {formatBranchInstant(i.occurredOn, zone)}
              </li>
            ))}
          </ul>
        )}
      </div>

      <div>
        <h3 className="mb-1 font-display text-base font-semibold text-ink">Medication given</h3>
        {cat.medicationsGiven.length === 0 ? (
          <p className="text-sm text-ink-muted">No medication was given.</p>
        ) : (
          <ul className="list-disc pl-5 text-sm">
            {cat.medicationsGiven.map((m, i) => (
              <li key={i}>
                {m.name}
                {m.dose ? `, ${m.dose}` : ''} — {formatBranchInstant(m.givenOn, zone)}
                {m.givenBy ? ` by ${m.givenBy}` : ''}
              </li>
            ))}
          </ul>
        )}
      </div>

      <div>
        <h3 className="mb-1 font-display text-base font-semibold text-ink">Vet visits</h3>
        {cat.vetVisits.length === 0 ? (
          <p className="text-sm text-ink-muted">No vet visits.</p>
        ) : (
          <ul className="list-disc pl-5 text-sm">
            {cat.vetVisits.map((v) => (
              <li key={v.referralId}>
                {v.reason} — {formatBranchInstant(v.referredOn, zone)}
              </li>
            ))}
          </ul>
        )}
      </div>

      <div>
        <h3 className="mb-1 font-display text-base font-semibold text-ink">Belongings</h3>
        {cat.belongings.length === 0 ? (
          <p className="text-sm text-ink-muted">No belongings were handed in.</p>
        ) : (
          <ul className="list-disc pl-5 text-sm">
            {cat.belongings.map((b) => (
              <li key={b.id}>
                {BELONGING_KIND_LABEL[b.kind]}: {b.description} —{' '}
                {b.returned === null
                  ? 'not yet returned'
                  : b.returned
                    ? 'returned'
                    : `not returned (${b.returnNote ?? 'no reason given'})`}
              </li>
            ))}
          </ul>
        )}
      </div>
    </section>
  );
}

function Body({ summary }: { summary: StaySummary }) {
  return (
    <article className="flex flex-col gap-6">
      <header className="flex flex-col gap-2">
        <h1 className="font-display text-2xl font-semibold text-ink">Stay summary</h1>
        <p className="text-sm text-ink">
          {formatStayDates(summary.arrivalOn, summary.departureOn)}
        </p>
        <div className="print:hidden">
          <Button variant="secondary" size="sm" onClick={() => window.print()}>
            <Printer size={14} aria-hidden="true" /> Print this summary
          </Button>
        </div>
      </header>
      {summary.cats.map((cat) => (
        <CatSummary key={cat.catId} cat={cat} zone={summary.branchTimeZoneId} />
      ))}
      <StayCharges
        charges={summary.charges}
        invoices={summary.invoices}
        depositReceived={summary.depositReceived}
        billing={summary.billing}
      />
      <Link
        href={portalStayHref(summary.stayId)}
        className="text-sm font-semibold text-accent-ink underline print:hidden"
      >
        ← Back to the stay
      </Link>
    </article>
  );
}

/** `components/boarding/stay/StaySummaryView.tsx` — HO-BRD-002 §11.3 `StaySummary`, US-BRD-17 AC1,
 *  OQ-5: per cat the daily logs, the weight change (as the server computed it), incidents, medication
 *  given, vet visits, belongings returned and the signature record; then the charges. A print
 *  stylesheet (Tailwind `print:` utilities) — no PDF. No payment control. */
export function StaySummaryView({
  query,
}: {
  query: Parameters<typeof QueryGate<StaySummary>>[0]['query'];
}) {
  return (
    <QueryGate
      query={query}
      loadingLabel="Loading the summary…"
      notFoundTitle="We can’t find that stay."
    >
      {(summary) => <Body summary={summary} />}
    </QueryGate>
  );
}

/** The customer's own summary page body (`GET /api/customers/me/boarding/stays/{id}/summary`). */
export function MyStaySummaryView({ stayId }: { stayId: string }) {
  return <StaySummaryView query={useMyStaySummary(stayId)} />;
}
