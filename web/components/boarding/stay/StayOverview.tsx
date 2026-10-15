'use client';

import Link from 'next/link';
import { buttonVariants } from '@/components/ui/button';
import { CareProfileTabs } from '@/components/boarding/booking/CareProfileTabs';
import { CatHeaderCompact } from '@/components/boarding/shared/CatHeaderCompact';
import { QueryGate } from '@/components/boarding/shared/QueryGate';
import {
  GateStatusBadge,
  SeverityBadge,
  StayStatusBadge,
} from '@/components/boarding/shared/StatusBadges';
import {
  INCIDENT_KIND_LABEL,
  STAY_NEXT_STEP,
  UNIT_TYPE_LABEL,
} from '@/components/boarding/shared/labels';
import type { MyStayDetail } from '@/lib/api/types';
import { formatMoney } from '@/lib/format/money';
import { depositReceivedOf, formatStayDates } from '@/lib/format/boarding';
import { timeOnlyInZone } from '@/lib/format/time';
import { useMyStay, useMyStaySummary, useSaveCareProfile } from '@/lib/hooks/useMyBoarding';
import {
  portalApprovalHref,
  portalIncidentHref,
  portalStayDiaryHref,
  portalStaySummaryHref,
} from '@/lib/routing/boarding-links';
import { cn } from '@/lib/utils';
import { CancelStayControl } from './CancelStayControl';
import { DepositSummary } from './DepositSummary';
import { StayCharges } from './StayCharges';

const EDITABLE = new Set(['Enquiry', 'Reserved', 'Confirmed']);

function Links({ stay }: { stay: MyStayDetail }) {
  const inHouseOrAfter = ['CheckedIn', 'CheckedOut'].includes(stay.status);
  return (
    <div className="flex flex-wrap gap-2">
      {inHouseOrAfter && (
        <Link
          href={portalStayDiaryHref(stay.id)}
          className={cn(buttonVariants({ variant: 'primary' }))}
        >
          See the diary
        </Link>
      )}
      {stay.status === 'CheckedOut' && (
        <Link
          href={portalStaySummaryHref(stay.id)}
          className={cn(buttonVariants({ variant: 'secondary' }))}
        >
          Stay summary
        </Link>
      )}
    </div>
  );
}

function Body({ stay }: { stay: MyStayDetail }) {
  // The built stay carries no invoices: Billing's ids come from the summary (`billing.invoiceIds`).
  const invoiceSource = useMyStaySummary(stay.id, stay.status !== 'Enquiry');
  const save = useSaveCareProfile(stay.id);
  const currency = stay.quote?.total.currencyCode ?? stay.deposit.required.currencyCode;
  return (
    <div className="flex flex-col gap-8">
      <header className="flex flex-col gap-2">
        <div className="flex flex-wrap items-center gap-3">
          <h1 className="font-display text-2xl font-semibold text-ink">
            {stay.cats.map((c) => c.catName).join(' and ')}
          </h1>
          <StayStatusBadge status={stay.status} />
        </div>
        <p className="text-sm text-ink">
          {formatStayDates(stay.arrivalOn, stay.departureOn, stay.nights)} ·{' '}
          {UNIT_TYPE_LABEL[stay.requestedUnitType]}
          {stay.unitName ? ` · ${stay.unitName}` : ''}
        </p>
        {stay.occupiedFrom && (
          <p className="text-sm text-ink-muted">
            Check-in from {timeOnlyInZone(stay.occupiedFrom, stay.branchTimeZoneId)} (
            {stay.branchTimeZoneId})
          </p>
        )}
        <p className="text-sm font-semibold text-ink">{STAY_NEXT_STEP[stay.status]}</p>
        <Links stay={stay} />
      </header>

      <section aria-labelledby="cats-heading" className="flex flex-col gap-3">
        <h2 id="cats-heading" className="font-display text-lg font-semibold text-ink">
          Cats
        </h2>
        <ul className="grid gap-3 sm:grid-cols-2">
          {stay.cats.map((cat) => (
            <li
              key={cat.catId}
              className="flex flex-col gap-2 rounded-[var(--radius-sm)] border border-border bg-surface p-4"
            >
              <CatHeaderCompact
                name={cat.catName}
                isHighRisk={cat.catIsHighRisk}
                handlingNote={cat.handlingNote}
              />
              <GateStatusBadge status={cat.gateStatus} />
              {cat.awayFromUnit && (
                <p className="text-sm font-semibold text-warning-ink">
                  Away — {cat.awayReason ?? 'at the clinic'}
                </p>
              )}
            </li>
          ))}
        </ul>
      </section>

      <DepositSummary
        status={stay.status}
        deposit={stay.deposit}
        balanceDue={stay.balanceDue}
        zone={stay.branchTimeZoneId}
      />

      {stay.pendingApprovals.length > 0 && (
        <section aria-labelledby="approvals-heading" className="flex flex-col gap-2">
          <h2 id="approvals-heading" className="font-display text-lg font-semibold text-ink">
            Waiting for your answer
          </h2>
          <ul className="flex flex-col gap-2">
            {stay.pendingApprovals.map((a) => (
              <li
                key={a.id}
                className="rounded-[var(--radius-sm)] border border-border bg-warning-bg p-3 text-sm"
              >
                <Link
                  href={portalApprovalHref(stay.id, a.id)}
                  className="font-semibold text-warning-ink underline"
                >
                  The vet would like to treat {a.catName} — {formatMoney(a.amount)}
                </Link>
              </li>
            ))}
          </ul>
        </section>
      )}

      {stay.incidents.length > 0 && (
        <section aria-labelledby="incidents-heading" className="flex flex-col gap-2">
          <h2 id="incidents-heading" className="font-display text-lg font-semibold text-ink">
            Incidents
          </h2>
          <ul className="flex flex-col gap-2">
            {stay.incidents.map((i) => (
              <li key={i.id} className="flex flex-wrap items-center gap-3 text-sm">
                <Link
                  href={portalIncidentHref(stay.id, i.id)}
                  className="font-semibold text-accent-ink underline"
                >
                  {INCIDENT_KIND_LABEL[i.kind]} — {i.catName}
                </Link>
                <SeverityBadge severity={i.severity} />
                <span className="text-ink-muted">
                  {i.acknowledged ? 'You have seen this' : 'Not yet seen'}
                </span>
              </li>
            ))}
          </ul>
        </section>
      )}

      <CareProfileTabs
        cats={stay.cats}
        currencyCode={currency}
        readOnly={!EDITABLE.has(stay.status)}
        onSave={(catId, profile) => save.mutateAsync({ catId, profile })}
      />

      <StayCharges
        charges={stay.charges}
        invoices={invoiceSource.data?.invoices ?? stay.invoices}
        depositReceived={depositReceivedOf(stay)}
        balanceDue={stay.balanceDue}
      />

      <CancelStayControl stay={stay} />
    </div>
  );
}

/** `components/boarding/stay/StayOverview.tsx` — HO-BRD-002 §11.3, US-BRD-06: the owner's stay page. */
export function StayOverview({ stayId }: { stayId: string }) {
  const query = useMyStay(stayId);
  return (
    <QueryGate
      query={query}
      loadingLabel="Loading your stay…"
      notFoundTitle="We can’t find that stay."
      notFoundDescription="It may have been removed, or it may belong to someone else."
    >
      {(stay) => <Body stay={stay} />}
    </QueryGate>
  );
}
