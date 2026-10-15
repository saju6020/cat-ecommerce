'use client';

import { useRef, useState } from 'react';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { Button } from '@/components/ui/button';
import { FormFieldRow } from '@/components/cat/FormFieldRow';
import { CatHeaderCompact } from '@/components/boarding/shared/CatHeaderCompact';
import { FieldError, FormErrorAlert } from '@/components/boarding/shared/FormBits';
import { BELONGING_KIND_LABEL } from '@/components/boarding/shared/labels';
import { SignaturePad } from '@/components/signature/SignaturePad';
import { ApiValidationError } from '@/lib/api/client';
import { describeBoardingError, parseChecklistProperty } from '@/lib/api/errors';
import type { StaffStayDetail } from '@/lib/api/types';
import { useCheckOutPreview, useCheckOutStay } from '@/lib/hooks/useStaffBoarding';
import { EarlyCheckOutPreview } from './EarlyCheckOutPreview';
import {
  EMPTY_SIGNATURE,
  checkOutCatErrors,
  draftToSignature,
  type CheckOutCatDraft,
  type ChecklistErrors,
} from '@/lib/validation/boarding';
import { StayCharges } from './StayCharges';

function Wizard({ stay, onDone }: { stay: StaffStayDetail; onDone: () => void }) {
  const checkOut = useCheckOutStay(stay.id);
  const [drafts, setDrafts] = useState<CheckOutCatDraft[]>(() =>
    stay.cats.map((c) => ({
      weightKg: '',
      returns: Object.fromEntries(
        (c.belongings ?? []).map((b) => [b.id, { returned: true, note: '' }]),
      ),
      signature: { ...EMPTY_SIGNATURE },
    })),
  );
  const [step, setStep] = useState(0);
  const [errors, setErrors] = useState<ChecklistErrors[]>(() => stay.cats.map(() => ({})));
  const [serverError, setServerError] = useState<string | null>(null);
  const [result, setResult] = useState<StaffStayDetail | null>(null);
  const [dirty, setDirty] = useState(false);
  const submitting = useRef(false);
  const summaryStep = stay.cats.length;
  // Early or late is the SERVER's call (its clock and the branch's day), not the browser's.
  const preview = useCheckOutPreview(stay.id);
  const early = preview.data?.isEarly ?? false;
  const late = preview.data?.isLate ?? false;
  const [wasEarly, setWasEarly] = useState(false);

  function patch(i: number, next: Partial<CheckOutCatDraft>) {
    setDirty(true);
    setDrafts((prev) => prev.map((d, di) => (di === i ? { ...d, ...next } : d)));
  }

  async function submit() {
    if (submitting.current) return;
    const all = drafts.map((d) => checkOutCatErrors(d));
    setErrors(all);
    const bad = all.findIndex((e) => Object.keys(e).length > 0);
    if (bad >= 0) {
      setStep(bad);
      return;
    }
    submitting.current = true;
    setServerError(null);
    try {
      const response = await checkOut.mutateAsync(
        stay.cats.map((c, i) => {
          const d = drafts[i]!;
          return {
            catId: c.catId,
            weightKg: Number(d.weightKg),
            belongings: Object.entries(d.returns).map(([belongingId, r]) => ({
              belongingId,
              returned: r.returned,
              note: r.returned ? null : r.note.trim(),
            })),
            ownerSignature: draftToSignature(d.signature),
          };
        }),
      );
      setWasEarly(early);
      setDirty(false);
      // the owner's signature is not kept in memory once the check-out is recorded
      setDrafts((prev) => prev.map((d) => ({ ...d, signature: { ...EMPTY_SIGNATURE } })));
      checkOut.reset();
      setResult(response.stay);
    } catch (e) {
      if (e instanceof ApiValidationError) {
        const mapped: ChecklistErrors[] = stay.cats.map(() => ({}));
        let first = -1;
        for (const v of e.response.errors) {
          const parsed = parseChecklistProperty(v.propertyName);
          if (parsed && mapped[parsed.catIndex]) {
            mapped[parsed.catIndex]![
              parsed.field === 'ownerSignature' ? 'signature' : parsed.field || 'general'
            ] = v.errorMessage;
            if (first < 0 || parsed.catIndex < first) first = parsed.catIndex;
          }
        }
        setErrors(mapped);
        if (first >= 0) setStep(first);
      }
      setServerError(describeBoardingError(e));
    } finally {
      submitting.current = false;
    }
  }

  if (result) {
    return (
      <section
        aria-labelledby="checkout-done"
        className="flex flex-col gap-4 rounded-[var(--radius-md)] border border-border bg-surface p-5"
      >
        <h2 id="checkout-done" className="font-display text-lg font-semibold text-ink">
          Checked out
        </h2>
        <p role="status" className="text-sm text-ink">
          {wasEarly
            ? 'This was an early check-out. The server recalculated the nights and the charges below show the result, including any credit.'
            : 'The stay is checked out. The charges below are final for the nights stayed.'}
        </p>
        <StayCharges
          charges={result.charges}
          invoices={result.invoices}
          depositReceived={result.deposit.accepted}
          balanceDue={result.balanceDue}
        />
        <div>
          <Button onClick={onDone}>Done</Button>
        </div>
      </section>
    );
  }

  const cat = stay.cats[step];
  const draft = drafts[step];
  const belongings = cat?.belongings ?? [];
  const err = errors[step] ?? {};

  return (
    <section
      aria-labelledby="checkout-heading"
      className="flex flex-col gap-4 rounded-[var(--radius-md)] border border-border bg-surface p-5"
    >
      <UnsavedChangesGuard when={dirty} />
      <h2 id="checkout-heading" className="font-display text-lg font-semibold text-ink">
        Check out
      </h2>
      {(early || late) && (
        <p
          role="note"
          className="rounded-[var(--radius-sm)] bg-warning-bg px-3 py-2 text-sm font-semibold text-warning-ink"
        >
          {early
            ? 'This is an early check-out. The nights used and the basis (for example the long-stay discount) are recalculated by the server when you check out, and the new lines and any refund or credit are shown straight after.'
            : 'This stay is past its booked departure. The server charges the extra nights when you check out.'}
        </p>
      )}
      <FormErrorAlert message={serverError} />
      {cat && draft && (
        <div className="flex flex-col gap-4">
          <CatHeaderCompact
            name={cat.catName}
            isHighRisk={cat.catIsHighRisk}
            handlingNote={cat.handlingNote}
          />
          <div className="flex items-end gap-2">
            <FormFieldRow
              id={`co-weight-${step}`}
              label="Weight"
              inputMode="decimal"
              required
              className="max-w-40"
              value={draft.weightKg}
              error={err.weightKg}
              onChange={(e) => patch(step, { weightKg: e.target.value })}
            />
            <span className="pb-2 text-sm font-semibold text-ink-muted">kg</span>
          </div>
          <fieldset className="flex flex-col gap-3">
            <legend className="text-sm font-semibold text-ink">Belongings returned</legend>
            {belongings.length === 0 && (
              <p className="text-sm text-ink-muted">No belongings were handed in.</p>
            )}
            {belongings.map((b) => {
              const r = draft.returns[b.id] ?? { returned: true, note: '' };
              return (
                <div
                  key={b.id}
                  className="flex flex-col gap-2 rounded-[var(--radius-sm)] border border-border p-3"
                >
                  <label className="flex items-center gap-2 text-sm text-ink">
                    <input
                      type="checkbox"
                      checked={r.returned}
                      onChange={(e) =>
                        patch(step, {
                          returns: {
                            ...draft.returns,
                            [b.id]: { ...r, returned: e.target.checked },
                          },
                        })
                      }
                    />
                    {BELONGING_KIND_LABEL[b.kind]}: {b.description} — returned
                  </label>
                  {!r.returned && (
                    <FormFieldRow
                      id={`co-note-${b.id}`}
                      label="Why was it not returned?"
                      required
                      value={r.note}
                      error={err[`belonging:${b.id}`]}
                      onChange={(e) =>
                        patch(step, {
                          returns: { ...draft.returns, [b.id]: { ...r, note: e.target.value } },
                        })
                      }
                    />
                  )}
                </div>
              );
            })}
            <FieldError id="co-bel" message={err.belongings} />
          </fieldset>
          <SignaturePad
            idPrefix={`co-sign-${step}`}
            value={draft.signature}
            onChange={(signature) => patch(step, { signature })}
            error={err.signature}
            statement="I confirm I have collected my cat and belongings."
          />
          <div className="flex gap-3">
            {step > 0 && (
              <Button variant="secondary" onClick={() => setStep(step - 1)}>
                Back
              </Button>
            )}
            <Button
              onClick={() => {
                const found = checkOutCatErrors(draft);
                setErrors((prev) => prev.map((e, i) => (i === step ? found : e)));
                if (Object.keys(found).length === 0) setStep(step + 1);
              }}
            >
              {step === stay.cats.length - 1 ? 'Review' : 'Next cat'}
            </Button>
          </div>
        </div>
      )}
      {step === summaryStep && (
        <div className="flex flex-col gap-3">
          <EarlyCheckOutPreview
            query={preview}
            zone={stay.branchTimeZoneId}
            onRetry={() => void preview.refetch()}
          />
          <ul aria-label="Check-out summary" className="text-sm">
            {stay.cats.map((c, i) => (
              <li key={c.catId}>
                <strong>{c.catName}</strong> · {drafts[i]!.weightKg} kg · signed by{' '}
                {drafts[i]!.signature.signerName}
              </li>
            ))}
          </ul>
          <div className="flex gap-3">
            <Button variant="secondary" onClick={() => setStep(summaryStep - 1)}>
              Back
            </Button>
            <Button
              onClick={() => void submit()}
              disabled={checkOut.isPending}
              aria-busy={checkOut.isPending}
            >
              {checkOut.isPending ? 'Checking out…' : 'Check out'}
            </Button>
          </div>
        </div>
      )}
    </section>
  );
}

/** `components/boarding/stay/CheckOutWizard.tsx` — ⭐ HO-BRD-002 §11.8, US-BRD-16/17, ADR-FE-108:
 *  weight, belongings returned (a note when not), signature per cat, one submit. The early check-out
 *  recalculation is the server's, shown before the submit from `GET …/check-out-preview` and again on the
 *  result (ADR-FE-105). */
export function CheckOutWizard({ stay, onDone }: { stay: StaffStayDetail; onDone: () => void }) {
  // The belongings come with the stay view (`cats[].belongings`, from `checkIn.belongings`).
  return <Wizard stay={stay} onDone={onDone} />;
}
