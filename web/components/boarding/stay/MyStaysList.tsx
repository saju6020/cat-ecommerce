'use client';

import { useState } from 'react';
import Link from 'next/link';
import { Button, buttonVariants } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { EmptyCatIllustration, EmptyState } from '@/components/common/EmptyState';
import { QueryGate } from '@/components/boarding/shared/QueryGate';
import { DepositStateBadge, StayStatusBadge } from '@/components/boarding/shared/StatusBadges';
import { UNIT_TYPE_LABEL } from '@/components/boarding/shared/labels';
import type { MyStaySummary, WaitlistEntryDto } from '@/lib/api/types';
import type { MyStayScope } from '@/lib/api/my-boarding';
import { formatStayDates } from '@/lib/format/boarding';
import { formatMoney } from '@/lib/format/money';
import {
  useMyBoardingWaitlist,
  useMyStays,
  useWithdrawFromBoardingWaitlist,
} from '@/lib/hooks/useMyBoarding';
import {
  BOARDING_BOOK,
  BOARDING_DAYCARE,
  boardingSearchHref,
  portalStayHref,
} from '@/lib/routing/boarding-links';
import { cn } from '@/lib/utils';

const TABS: Array<{ scope: MyStayScope; label: string }> = [
  { scope: 'upcoming', label: 'Upcoming' },
  { scope: 'current', label: 'In house' },
  { scope: 'past', label: 'Past' },
];

const EMPTY_TEXT: Record<MyStayScope, string> = {
  upcoming: 'You have no upcoming stays.',
  current: 'None of your cats is with us right now.',
  past: 'You have no past stays yet.',
};

function StayRow({ stay }: { stay: MyStaySummary }) {
  return (
    <li className="flex flex-col gap-2 rounded-[var(--radius-md)] border border-border bg-surface p-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <Link
          href={portalStayHref(stay.id)}
          className="text-[15px] font-semibold text-accent-ink underline"
        >
          {stay.catNames.join(' and ')}
        </Link>
        <StayStatusBadge status={stay.status} />
      </div>
      <p className="text-sm text-ink">
        {formatStayDates(stay.arrivalOn, stay.departureOn, stay.nights)} ·{' '}
        {UNIT_TYPE_LABEL[stay.unitType]}
      </p>
      <div className="flex flex-wrap items-center gap-3 text-sm">
        <DepositStateBadge state={stay.depositState} stayStatus={stay.status} />
        {stay.balanceDue && (
          <span className="text-ink-muted">Balance due {formatMoney(stay.balanceDue)}</span>
        )}
      </div>
    </li>
  );
}

function EnquiryRow({ stay }: { stay: MyStaySummary }) {
  const href = boardingSearchHref({ stayId: stay.id }, BOARDING_BOOK);
  return (
    <li className="flex flex-col gap-2 rounded-[var(--radius-md)] border border-border bg-surface p-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <span className="text-[15px] font-semibold text-ink">{stay.catNames.join(' and ')}</span>
        <StayStatusBadge status={stay.status} />
      </div>
      <p className="text-sm text-ink">
        {formatStayDates(stay.arrivalOn, stay.departureOn, stay.nights)} ·{' '}
        {UNIT_TYPE_LABEL[stay.unitType]}
      </p>
      {stay.vaccinationBlocked && (
        <p className="text-sm font-semibold text-danger-ink">
          Waiting for a vaccination before this can be reserved.
        </p>
      )}
      <div>
        <Link href={href} className={cn(buttonVariants({ variant: 'primary', size: 'sm' }))}>
          Resume
        </Link>
      </div>
    </li>
  );
}

function WaitlistSection({ entries }: { entries: WaitlistEntryDto[] }) {
  const withdraw = useWithdrawFromBoardingWaitlist();
  const [target, setTarget] = useState<WaitlistEntryDto | null>(null);
  if (entries.length === 0) return null;
  return (
    <section aria-labelledby="waitlist-heading" className="flex flex-col gap-3">
      <h2 id="waitlist-heading" className="font-display text-lg font-semibold text-ink">
        Waitlist
      </h2>
      <ul className="flex flex-col gap-3">
        {entries.map((entry) => (
          <li
            key={entry.id}
            className="flex flex-wrap items-center justify-between gap-3 rounded-[var(--radius-md)] border border-border bg-surface p-4"
          >
            <span className="text-sm text-ink">
              {formatStayDates(entry.arrivalOn, entry.departureOn)} · {entry.catCount} cat
              {entry.catCount === 1 ? '' : 's'} ·{' '}
              <strong>
                {entry.status === 'Notified' ? 'A unit is free — book now' : 'Waiting'}
              </strong>
            </span>
            <Button variant="secondary" size="sm" onClick={() => setTarget(entry)}>
              Withdraw
            </Button>
          </li>
        ))}
      </ul>
      <ConfirmDialog
        open={!!target}
        onOpenChange={(open) => !open && setTarget(null)}
        title="Leave the waitlist?"
        consequence={
          target
            ? `You will stop being told if a unit frees up for ${formatStayDates(target.arrivalOn, target.departureOn)}.`
            : ''
        }
        confirmLabel="Withdraw"
        variant="destructive"
        onConfirm={async () => {
          if (target) await withdraw.mutateAsync(target.id);
          setTarget(null);
        }}
      />
    </section>
  );
}

/**
 * `components/boarding/stay/MyStaysList.tsx` — HO-BRD-002 §11.2, US-BRD-04 AC2. Tabs *Upcoming* /
 * *In house* / *Past* (a tab row of buttons, not colour); each row: cats, dates, unit type, status as
 * text, deposit state and balance due. **Enquiries** (blocked or unfinished drafts) are listed apart
 * with **Resume**, which re-enters the booking flow at the right step. The **Waitlist** has
 * **Withdraw**. **Book a stay** and **Book daycare** links.
 */
export function MyStaysList() {
  const [scope, setScope] = useState<MyStayScope>('upcoming');
  const stays = useMyStays(scope);
  const waitlist = useMyBoardingWaitlist();

  return (
    <div className="flex flex-col gap-8">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div role="tablist" aria-label="Stays" className="flex flex-wrap gap-2">
          {TABS.map((tab) => (
            <button
              key={tab.scope}
              role="tab"
              type="button"
              aria-selected={scope === tab.scope}
              onClick={() => setScope(tab.scope)}
              className={cn(
                'min-h-11 rounded-[var(--radius-sm)] border px-4 text-sm font-semibold',
                scope === tab.scope
                  ? 'border-accent bg-accent-soft text-accent-ink'
                  : 'border-border bg-surface text-ink',
              )}
            >
              {tab.label}
            </button>
          ))}
        </div>
        <div className="flex flex-wrap gap-2">
          <Link href={BOARDING_BOOK} className={cn(buttonVariants({ variant: 'primary' }))}>
            Book a stay
          </Link>
          <Link href={BOARDING_DAYCARE} className={cn(buttonVariants({ variant: 'secondary' }))}>
            Book daycare
          </Link>
        </div>
      </div>

      <QueryGate query={stays} loadingLabel="Loading your stays…">
        {(page) => {
          const enquiries =
            scope === 'upcoming' ? page.items.filter((s) => s.status === 'Enquiry') : [];
          const rows = page.items.filter((s) => !(scope === 'upcoming' && s.status === 'Enquiry'));
          return (
            <>
              {enquiries.length > 0 && (
                <section aria-labelledby="enquiries-heading" className="flex flex-col gap-3">
                  <h2
                    id="enquiries-heading"
                    className="font-display text-lg font-semibold text-ink"
                  >
                    Enquiries to finish
                  </h2>
                  <ul className="flex flex-col gap-3">
                    {enquiries.map((s) => (
                      <EnquiryRow key={s.id} stay={s} />
                    ))}
                  </ul>
                </section>
              )}
              {rows.length === 0 && enquiries.length === 0 ? (
                <EmptyState
                  icon={<EmptyCatIllustration />}
                  title={EMPTY_TEXT[scope]}
                  description="When you book a stay it appears here, with its deposit and balance."
                />
              ) : (
                rows.length > 0 && (
                  <ul
                    aria-label={`${TABS.find((t) => t.scope === scope)?.label} stays`}
                    className="flex flex-col gap-3"
                  >
                    {rows.map((s) => (
                      <StayRow key={s.id} stay={s} />
                    ))}
                  </ul>
                )
              )}
            </>
          );
        }}
      </QueryGate>

      {waitlist.data && <WaitlistSection entries={waitlist.data} />}
    </div>
  );
}
