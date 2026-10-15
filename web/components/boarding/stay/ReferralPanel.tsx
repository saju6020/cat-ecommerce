'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { FormFieldRow } from '@/components/cat/FormFieldRow';
import { CatHeaderCompact } from '@/components/boarding/shared/CatHeaderCompact';
import { FormErrorAlert, SelectRow, TextAreaRow } from '@/components/boarding/shared/FormBits';
import { ReasonDialog } from '@/components/boarding/shared/ReasonDialog';
import { describeBoardingError } from '@/lib/api/errors';
import type { StaffStayDetail, TriagePriority } from '@/lib/api/types';
import { formatBranchInstant } from '@/lib/format/boarding';
import { formatMoney } from '@/lib/format/money';
import { toMoneyText } from '@/lib/validation/boarding';
import {
  useReferToClinic,
  useReturnFromReferral,
  useRequestVetApproval,
  useStaffApprovalAnswer,
  useVetHeadroom,
} from '@/lib/hooks/useStaffBoarding';

function Headroom({ stayId, catId }: { stayId: string; catId: string }) {
  const h = useVetHeadroom(stayId, catId);
  if (!h.data) return null;
  return (
    <p className="text-sm text-ink">
      Vet limit {formatMoney(h.data.limit)}, committed {formatMoney(h.data.committed)}, headroom{' '}
      <strong>{formatMoney(h.data.headroom)}</strong>
      {!h.data.vetVisitAuthorised && ' — the owner has not authorised treatment'}
    </p>
  );
}

/**
 * `components/boarding/stay/ReferralPanel.tsx` — HO-BRD-002 §11.8, US-BRD-14: **Refer to the clinic**
 * (reason, urgency) and the created referral (its visit link appears when HO-EMR-001 opens the visit),
 * the **vet headroom** as text (limit, committed, headroom, and *“the owner has not authorised
 * treatment”*) and **Request approval** (amount, description) for a cost above it — plus the
 * approvals with their state and a staff-recorded phone answer. Amounts are typed as text and sent
 * as text; the headroom is the server's (ADR-FE-105).
 */
export function ReferralPanel({ stay }: { stay: StaffStayDetail }) {
  const refer = useReferToClinic(stay.id);
  const request = useRequestVetApproval(stay.id);
  const answer = useStaffApprovalAnswer(stay.id);
  const returned = useReturnFromReferral(stay.id);
  const [catId, setCatId] = useState(stay.cats[0]?.catId ?? '');
  const [urgency, setUrgency] = useState<TriagePriority>('Routine');
  const [reason, setReason] = useState('');
  const [amount, setAmount] = useState('');
  const [description, setDescription] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [answering, setAnswering] = useState<{ id: string; answer: 'Approve' | 'Decline' } | null>(
    null,
  );
  const currency = stay.deposit.required.currencyCode;
  const inHouse = stay.status === 'CheckedIn';

  async function run(fn: () => Promise<unknown>, after?: () => void) {
    setError(null);
    try {
      await fn();
      after?.();
    } catch (e) {
      setError(describeBoardingError(e));
    }
  }

  return (
    <section aria-labelledby="referral-heading" className="flex flex-col gap-4">
      <h2 id="referral-heading" className="font-display text-lg font-semibold text-ink">
        Vet referral and approvals
      </h2>
      <FormErrorAlert message={error} />
      {inHouse && (
        <div className="grid gap-3 rounded-[var(--radius-md)] border border-border bg-surface p-4 sm:grid-cols-2">
          <SelectRow
            id="ref-cat"
            label="Cat"
            value={catId}
            onChange={(e) => setCatId(e.target.value)}
          >
            {stay.cats.map((c) => (
              <option key={c.catId} value={c.catId}>
                {c.catName}
              </option>
            ))}
          </SelectRow>
          <SelectRow
            id="ref-urgency"
            label="Urgency"
            value={urgency}
            onChange={(e) => setUrgency(e.target.value as TriagePriority)}
          >
            <option value="Routine">Routine</option>
            <option value="Urgent">Urgent</option>
            <option value="Emergency">Emergency</option>
          </SelectRow>
          <TextAreaRow
            id="ref-reason"
            label="Reason for the referral"
            className="sm:col-span-2"
            value={reason}
            onChange={(e) => setReason(e.target.value)}
          />
          <div className="sm:col-span-2">
            <Button
              disabled={!reason.trim() || refer.isPending}
              onClick={() =>
                void run(
                  () => refer.mutateAsync({ catId, reason: reason.trim(), urgency }),
                  () => setReason(''),
                )
              }
            >
              Refer to the clinic
            </Button>
          </div>
          {catId && (
            <div className="sm:col-span-2">
              <Headroom stayId={stay.id} catId={catId} />
            </div>
          )}
          <FormFieldRow
            id="ap-amount"
            label={`Cost to approve (${currency})`}
            inputMode="decimal"
            value={amount}
            onChange={(e) => setAmount(e.target.value)}
          />
          <FormFieldRow
            id="ap-desc"
            label="What for"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
          />
          <div className="sm:col-span-2">
            <Button
              variant="secondary"
              disabled={!amount.trim() || !description.trim() || request.isPending}
              onClick={() =>
                void run(
                  () =>
                    request.mutateAsync({
                      catId,
                      referralId: stay.referrals.find((r) => r.catId === catId)?.id,
                      amount: { amount: toMoneyText(amount), currencyCode: currency },
                      description: description.trim(),
                    }),
                  () => {
                    setAmount('');
                    setDescription('');
                  },
                )
              }
            >
              Request approval
            </Button>
          </div>
        </div>
      )}

      <div>
        <h3 className="mb-1 font-display text-base font-semibold text-ink">Referrals</h3>
        {stay.referrals.length === 0 ? (
          <p className="text-sm text-ink-muted">No referrals.</p>
        ) : (
          <ul className="flex flex-col gap-2 text-sm">
            {stay.referrals.map((r) => (
              <li key={r.id} className="rounded-[var(--radius-sm)] border border-border p-3">
                <CatHeaderCompact name={r.catName} isHighRisk={false} />
                {r.urgency} — {r.reason} ·{' '}
                {formatBranchInstant(r.referredOn, stay.branchTimeZoneId)} ·{' '}
                {r.returnedOn
                  ? `back with us since ${formatBranchInstant(r.returnedOn, stay.branchTimeZoneId)}`
                  : r.visitId
                    ? 'visit opened'
                    : 'waiting for the clinic to open the visit'}
                {!r.returnedOn && inHouse && (
                  <Button
                    size="sm"
                    variant="secondary"
                    className="ml-2"
                    disabled={returned.isPending}
                    aria-label={`${r.catName} is back from the clinic`}
                    onClick={() => void run(() => returned.mutateAsync(r.id))}
                  >
                    Cat is back
                  </Button>
                )}
              </li>
            ))}
          </ul>
        )}
      </div>

      <div>
        <h3 className="mb-1 font-display text-base font-semibold text-ink">Vet-cost approvals</h3>
        {stay.approvals.length === 0 ? (
          <p className="text-sm text-ink-muted">No approvals requested.</p>
        ) : (
          <ul className="flex flex-col gap-2 text-sm">
            {stay.approvals.map((a) => (
              <li
                key={a.id}
                className="flex flex-wrap items-center justify-between gap-2 rounded-[var(--radius-sm)] border border-border p-3"
              >
                <span>
                  {a.catName}: {a.description} — <strong>{formatMoney(a.amount)}</strong> ·{' '}
                  {a.status === 'Pending'
                    ? 'waiting for the owner'
                    : `${a.status.toLowerCase()} ${a.decidedVia === 'Staff' ? 'by phone' : 'online'}`}
                </span>
                {a.status === 'Pending' && (
                  <span className="flex gap-2">
                    <Button
                      size="sm"
                      variant="secondary"
                      onClick={() => setAnswering({ id: a.id, answer: 'Approve' })}
                    >
                      Owner approved by phone
                    </Button>
                    <Button
                      size="sm"
                      variant="secondary"
                      onClick={() => setAnswering({ id: a.id, answer: 'Decline' })}
                    >
                      Owner declined by phone
                    </Button>
                  </span>
                )}
              </li>
            ))}
          </ul>
        )}
      </div>
      {answering && (
        <ReasonDialog
          open
          onOpenChange={(o) => !o && setAnswering(null)}
          title={
            answering.answer === 'Approve'
              ? 'Record that the owner approved?'
              : 'Record that the owner declined?'
          }
          consequence="This is recorded against you, with the note, as the owner’s answer."
          reasonLabel="Note (who you spoke to, when)"
          requiredMessage="A note is required for a staff-recorded answer."
          confirmLabel="Record the answer"
          onConfirm={(note) =>
            answer.mutateAsync({ approvalId: answering.id, answer: answering.answer, note })
          }
        />
      )}
    </section>
  );
}
