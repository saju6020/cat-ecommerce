'use client';

import { useState } from 'react';
import { AlertTriangle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { CareProfileTabs } from '@/components/boarding/booking/CareProfileTabs';
import { ExtendDialog, RelocateDialog } from '@/components/boarding/board/ExtendRelocateDialogs';
import { FormErrorAlert } from '@/components/boarding/shared/FormBits';
import { QueryGate } from '@/components/boarding/shared/QueryGate';
import { ReasonDialog } from '@/components/boarding/shared/ReasonDialog';
import {
  DepositStateBadge,
  GateStatusBadge,
  StayStatusBadge,
} from '@/components/boarding/shared/StatusBadges';
import { ATTENTION_LABEL, UNIT_TYPE_LABEL } from '@/components/boarding/shared/labels';
import { describeBoardingError } from '@/lib/api/errors';
import type { StaffStayDetail } from '@/lib/api/types';
import { depositReceivedOf, formatStayDates } from '@/lib/format/boarding';
import { formatMoney } from '@/lib/format/money';
import { useUrlParams } from '@/lib/hooks/useUrlParams';
import {
  useCancelStaffStay,
  useClearStayAttention,
  useOverrideVaccinationGate,
  useRecordStayNoShow,
  useSaveStaffCareProfile,
  useStaffStay,
  useStaffStaySummary,
} from '@/lib/hooks/useStaffBoarding';
import { CheckInWizard } from './CheckInWizard';
import { CheckOutWizard } from './CheckOutWizard';
import { DepositPanel } from './DepositPanel';
import { IsolationPanel } from './IsolationPanel';
import { ReferralPanel } from './ReferralPanel';
import { GateOverrideNote } from './GateOverrideNote';
import { StayCharges } from './StayCharges';
import {
  DiaryTab,
  HistoryTab,
  HouseFoodPanel,
  IncidentsTab,
  StayCatHeaders,
  TasksTab,
} from './StayWorkspaceParts';

const TABS = [
  'overview',
  'care',
  'tasks',
  'diary',
  'incidents',
  'isolation',
  'charges',
  'history',
] as const;
type Tab = (typeof TABS)[number];
const TAB_LABEL: Record<Tab, string> = {
  overview: 'Overview',
  care: 'Care',
  tasks: 'Tasks',
  diary: 'Diary',
  incidents: 'Incidents',
  isolation: 'Isolation',
  charges: 'Charges',
  history: 'History',
};

type Dialog =
  | 'cancel'
  | 'noShow'
  | 'extend'
  | 'relocate'
  | 'checkIn'
  | 'checkOut'
  | { override: string }
  | null;

function Body({ stay }: { stay: StaffStayDetail }) {
  // The built stay carries no invoices: Billing's ids come from the summary (`billing.invoiceIds`).
  const invoiceSource = useStaffStaySummary(stay.id, stay.status !== 'Enquiry');
  const url = useUrlParams();
  const tabParam = url.get('tab');
  const tab: Tab = (TABS as readonly string[]).includes(tabParam) ? (tabParam as Tab) : 'overview';
  const [dialog, setDialog] = useState<Dialog>(null);
  const [error, setError] = useState<string | null>(null);
  const cancel = useCancelStaffStay(stay.id);
  const noShow = useRecordStayNoShow(stay.id);
  const clearAttention = useClearStayAttention(stay.id);
  const override = useOverrideVaccinationGate(stay.id);
  const saveProfile = useSaveStaffCareProfile(stay.id);
  const can = (a: StaffStayDetail['allowedTransitions'][number]) =>
    stay.allowedTransitions.includes(a);
  const blocked = stay.cats.filter((c) => c.gateStatus === 'Blocked');
  const currency = stay.deposit.required.currencyCode;

  return (
    <div className="flex flex-col gap-5">
      <header className="flex flex-col gap-3">
        <StayCatHeaders stay={stay} />
        <div className="flex flex-wrap items-center gap-3">
          <StayStatusBadge status={stay.status} />
          <DepositStateBadge state={stay.deposit.state} stayStatus={stay.status} />
          <span className="text-sm text-ink">
            {formatStayDates(stay.arrivalOn, stay.departureOn, stay.nights)} ·{' '}
            {UNIT_TYPE_LABEL[stay.requestedUnitType]}
            {stay.unitName
              ? ` · ${stay.unitName}${stay.unitBlock ? ` (${stay.unitBlock})` : ''}`
              : ''}
          </span>
          <span className="text-sm text-ink-muted">
            {stay.customerName}
            {stay.balanceDue ? ` · balance ${formatMoney(stay.balanceDue)}` : ''}
          </span>
        </div>
        <ul aria-label="Vaccination gate" className="flex flex-wrap gap-2">
          {stay.cats.map((c) => (
            <li key={c.catId} className="flex items-center gap-2 text-sm">
              {c.catName}: <GateStatusBadge status={c.gateStatus} />
              <GateOverrideNote
                by={c.gateOverriddenBy}
                reason={c.gateOverrideReason}
                on={c.gateOverriddenOn}
                zone={stay.branchTimeZoneId}
              />
              {c.awayFromUnit && (
                <span className="font-semibold text-warning-ink">
                  Away — {c.awayReason ?? 'in surgery'}
                </span>
              )}
            </li>
          ))}
        </ul>
        {stay.attention.needsAttention && stay.attention.reason && (
          <div
            role="alert"
            className="flex flex-wrap items-center justify-between gap-3 rounded-[var(--radius-sm)] bg-warning-bg px-3 py-2 text-sm font-semibold text-warning-ink"
          >
            <span className="flex items-center gap-2">
              <AlertTriangle size={16} aria-hidden="true" /> Needs attention —{' '}
              {ATTENTION_LABEL[stay.attention.reason]}
            </span>
            <Button
              size="sm"
              variant="secondary"
              onClick={() => void clearAttention.mutateAsync('Checked and dealt with')}
            >
              Clear
            </Button>
          </div>
        )}
        <FormErrorAlert message={error} />
        <div className="flex flex-wrap gap-2" aria-label="Stay actions" role="group">
          {can('CheckIn') && <Button onClick={() => setDialog('checkIn')}>Check in</Button>}
          {can('CheckOut') && <Button onClick={() => setDialog('checkOut')}>Check out</Button>}
          {can('Extend') && (
            <Button variant="secondary" onClick={() => setDialog('extend')}>
              Extend
            </Button>
          )}
          {can('Relocate') && (
            <Button variant="secondary" onClick={() => setDialog('relocate')}>
              Relocate
            </Button>
          )}
          {can('Cancel') && (
            <Button variant="secondary" onClick={() => setDialog('cancel')}>
              Cancel stay
            </Button>
          )}
          {can('RecordNoShow') && (
            <Button variant="secondary" onClick={() => setDialog('noShow')}>
              Record no-show
            </Button>
          )}
          {stay.canOverrideGate &&
            ['Enquiry', 'Reserved', 'Confirmed'].includes(stay.status) &&
            blocked.map((c) => (
              <Button
                key={c.catId}
                variant="secondary"
                onClick={() => setDialog({ override: c.catId })}
              >
                Override vaccination for {c.catName}
              </Button>
            ))}
        </div>
      </header>

      <div
        role="tablist"
        aria-label="Stay sections"
        className="flex flex-wrap gap-1 border-b border-border"
      >
        {TABS.map((t) => (
          <button
            key={t}
            role="tab"
            type="button"
            id={`stay-tab-${t}`}
            aria-selected={tab === t}
            aria-controls="stay-panel"
            onClick={() => url.set({ tab: t === 'overview' ? undefined : t })}
            className={`min-h-11 border-b-2 px-3 text-sm font-semibold ${tab === t ? 'border-accent text-ink' : 'border-transparent text-ink-muted'}`}
          >
            {TAB_LABEL[t]}
          </button>
        ))}
      </div>
      <div
        role="tabpanel"
        id="stay-panel"
        aria-labelledby={`stay-tab-${tab}`}
        className="flex flex-col gap-6"
      >
        {tab === 'overview' && (
          <>
            <DepositPanel stay={stay} />
            <dl className="grid gap-x-6 gap-y-1 text-sm sm:grid-cols-[9rem_1fr]">
              <dt className="font-semibold text-ink-muted">Channel</dt>
              <dd>{stay.channel === 'Online' ? 'Booked online' : 'Booked at reception'}</dd>
              <dt className="font-semibold text-ink-muted">Owner</dt>
              <dd>
                {stay.customerName}
                {stay.customerPhone ? ` · ${stay.customerPhone}` : ''}
              </dd>
            </dl>
          </>
        )}
        {tab === 'care' && (
          <>
            <CareProfileTabs
              cats={stay.cats}
              currencyCode={currency}
              readOnly={['CheckedOut', 'Cancelled', 'NoShow'].includes(stay.status)}
              requireAuthorityReason={stay.status !== 'Enquiry'}
              onSave={(catId, profile, extra) =>
                saveProfile.mutateAsync({ catId, profile, ...extra })
              }
            />
            <ReferralPanel stay={stay} />
          </>
        )}
        {tab === 'tasks' && <TasksTab />}
        {tab === 'diary' && <DiaryTab stay={stay} />}
        {tab === 'incidents' && <IncidentsTab stay={stay} />}
        {tab === 'isolation' && <IsolationPanel stay={stay} />}
        {tab === 'charges' && (
          <>
            <StayCharges
              charges={stay.charges}
              invoices={invoiceSource.data?.invoices ?? stay.invoices}
              depositReceived={depositReceivedOf(stay)}
              balanceDue={stay.balanceDue}
              invoiceHref={(id) => `/staff/billing/invoices/${encodeURIComponent(id)}`}
            />
            {stay.status === 'CheckedOut' && (
              <p className="text-sm text-ink-muted">
                The final invoice is issued by Billing shortly after check-out — if it is not here
                within a minute, the worker issues it on its next pass.
              </p>
            )}
            <HouseFoodPanel />
          </>
        )}
        {tab === 'history' && <HistoryTab stay={stay} />}
      </div>

      {dialog === 'checkIn' && (
        <div>
          <CheckInWizard stay={stay} onDone={() => setDialog(null)} />
        </div>
      )}
      {dialog === 'checkOut' && <CheckOutWizard stay={stay} onDone={() => setDialog(null)} />}
      {dialog === 'extend' && (
        <ExtendDialog open onOpenChange={(o) => !o && setDialog(null)} stay={stay} />
      )}
      {dialog === 'relocate' && (
        <RelocateDialog
          open
          onOpenChange={(o) => !o && setDialog(null)}
          stay={stay}
          onDone={() => setDialog(null)}
        />
      )}
      {dialog === 'cancel' && (
        <ReasonDialog
          open
          onOpenChange={(o) => !o && setDialog(null)}
          title={`Cancel the stay for ${stay.cats.map((c) => c.catName).join(' and ')}?`}
          consequence={`${formatStayDates(stay.arrivalOn, stay.departureOn, stay.nights)} is cancelled and the unit released. The owner is messaged.`}
          reasonLabel="Reason for cancelling"
          requiredMessage="Enter a reason for cancelling."
          confirmLabel="Cancel the stay"
          variant="destructive"
          onConfirm={(note) => cancel.mutateAsync(note)}
        />
      )}
      <ConfirmDialog
        open={dialog === 'noShow'}
        onOpenChange={(o) => !o && setDialog(null)}
        title="Record a no-show?"
        consequence={`${stay.cats.map((c) => c.catName).join(' and ')} did not arrive. The deposit is forfeited and the unit is released.`}
        confirmLabel="Record no-show"
        variant="destructive"
        onConfirm={async () => {
          try {
            await noShow.mutateAsync();
          } catch (e) {
            setError(describeBoardingError(e));
          }
          setDialog(null);
        }}
      />
      {typeof dialog === 'object' &&
        dialog &&
        'override' in dialog &&
        (() => {
          const target = stay.cats.find((c) => c.catId === dialog.override);
          return target ? (
            <ReasonDialog
              open
              onOpenChange={(o) => !o && setDialog(null)}
              title={`Override the vaccination block for ${target.catName}?`}
              consequence={`${target.catName} can be reserved for this stay although a vaccination is overdue. This will be recorded against you, with your reason. It does not admit the cat at check-in.`}
              reasonLabel="Reason (mandatory)"
              requiredMessage="Enter the reason for the override."
              confirmLabel="Override and record"
              variant="destructive"
              onConfirm={(reason) => override.mutateAsync({ catId: target.catId, reason })}
            />
          ) : null;
        })()}
    </div>
  );
}

/** `components/boarding/stay/StayWorkspace.tsx` — ⭐ HO-BRD-002 §11.8, US-BRD-06…18: the staff stay
 *  page — full cat headers, status, gate (with who overrode it and why), the attention banner, actions
 *  **only from `allowedTransitions[]`** (ADR-FE-107) and the tabs Overview · Care · Tasks · Diary ·
 *  Incidents · Isolation · Charges · History (`?tab=`). */
export function StayWorkspace({ stayId }: { stayId: string }) {
  const query = useStaffStay(stayId);
  return (
    <QueryGate
      query={query}
      loadingLabel="Loading the stay…"
      notFoundTitle="We can’t find that stay."
    >
      {(stay) => <Body stay={stay} />}
    </QueryGate>
  );
}
