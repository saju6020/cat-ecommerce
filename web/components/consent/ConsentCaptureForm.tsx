'use client';

import { useEffect, useRef, useState } from 'react';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { LimitedMarkup } from '@/components/content/LimitedMarkup';
import { FileUploadField } from '@/components/files/FileUploadField';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { VisitCatHeader } from '@/components/visit/VisitCatHeader';
import {
  MIN_TOTAL_POINTS,
  SignaturePad,
  type SignaturePadHandle,
  type SignatureSummary,
} from './SignaturePad';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';
import { mapClinicalFormErrors } from '@/lib/api/errors';
import { useConsentForm } from '@/lib/hooks/useConsents';
import { cn } from '@/lib/utils';
import type { ConsentKind, RecordConsentRequest, SignerRelationship } from '@/lib/api/consents';
import type { StoredFileViewModel } from '@/lib/api/files';

/**
 * ⭐ `ConsentCaptureForm` — HO-EMR-002 §11.6 (US-CLN-21), reached with `?catId&appointmentId&kind`. The
 * compact cat header (UI-7); the CURRENT consent-form text through `LimitedMarkup` (never HTML —
 * ADR-FE-058) with its version shown; the procedure; the signer (name pre-filled from the owner, and
 * Owner / Authorised agent); an optional witness; and two ways to sign — **Sign on screen** (the
 * `SignaturePad`) or **Upload signed form** (the non-drawing alternative, UI-6). Submit CONFIRMS
 * (UI-3) and records the exact form version shown. A form that is not published yet is a stated
 * `404` message and no submit button; a version that changed under the signer is
 * `ConsentFormVersionNotCurrent` and the wording is reloaded.
 */
const KINDS: ConsentKind[] = ['Surgery', 'Anaesthesia', 'Euthanasia'];
type Method = 'OnScreenSignature' | 'SignedUpload';

export function ConsentCaptureForm({
  catId,
  appointmentId,
  kind,
  onKindChange,
  ownerName,
  ownerCustomerId,
  witnesses,
  onSubmit,
}: {
  catId: string;
  appointmentId: string;
  kind: ConsentKind;
  onKindChange: (kind: ConsentKind) => void;
  ownerName: string;
  ownerCustomerId: string | null;
  witnesses: Array<{ id: string; name: string }>;
  onSubmit: (request: RecordConsentRequest) => Promise<void>;
}) {
  const form = useConsentForm(kind);
  const pad = useRef<SignaturePadHandle>(null);
  const [method, setMethod] = useState<Method>('OnScreenSignature');
  const [procedure, setProcedure] = useState('');
  const [signerName, setSignerName] = useState('');
  const [signerEdited, setSignerEdited] = useState(false);
  const [relationship, setRelationship] = useState<SignerRelationship>('Owner');
  const [witnessId, setWitnessId] = useState('');
  const [, setSignature] = useState<SignatureSummary>({
    strokes: 0,
    points: 0,
    isEnough: false,
  });
  const [file, setFile] = useState<StoredFileViewModel | null>(null);
  const [errors, setErrors] = useState<{ fields: Record<string, string>; root: string | null }>({
    fields: {},
    root: null,
  });
  const [confirmOpen, setConfirmOpen] = useState(false);
  // A different consent kind is a different signature: drop the old summary (the pad itself remounts).
  useEffect(() => {
    setSignature({ strokes: 0, points: 0, isEnough: false });
  }, [kind]);
  const shownSigner = signerEdited ? signerName : ownerName;

  if (form.isLoading)
    return <LoadingState variant="card" rows={6} label="Loading the consent wording…" />;
  if (form.error instanceof ApiForbiddenError) return <PermissionDeniedState />;
  if (form.error instanceof ApiNotFoundError) {
    return (
      <div className="flex flex-col gap-4">
        <VisitCatHeader catId={catId} />
        <KindSelect kind={kind} onChange={onKindChange} />
        <p
          role="alert"
          className="rounded-[var(--radius-md)] border border-border bg-warning-bg p-4 text-sm font-semibold text-warning-ink"
        >
          The {kind} consent form hasn’t been published yet. Ask an administrator.
        </p>
      </div>
    );
  }
  if (form.isError || !form.data) return <ErrorState onRetry={() => void form.refetch()} />;
  const wording = form.data;

  /** What is REALLY on the pad right now — never the cached summary, which can outlive a remount. */
  function liveStrokes() {
    return pad.current?.getStrokes() ?? [];
  }
  const liveSummary = () => {
    const strokes = liveStrokes();
    return { strokes: strokes.length, points: strokes.reduce((n, st) => n + st.length, 0) };
  };

  function validate(): boolean {
    const fields: Record<string, string> = {};
    if (!procedure.trim()) fields.procedure = 'Describe the procedure being consented to.';
    if (!shownSigner.trim()) fields.signer = 'Enter the name of the person signing.';
    const live = liveSummary();
    if (method === 'OnScreenSignature' && live.points < MIN_TOTAL_POINTS) {
      fields.signature =
        live.strokes === 0
          ? 'Sign in the box above, or upload a signed form.'
          : 'The signature is too short. Clear it and sign again.';
    }
    if (method === 'SignedUpload' && !file) fields.file = 'Upload the signed form first.';
    setErrors({ fields, root: null });
    return Object.keys(fields).length === 0;
  }

  async function record() {
    // Never submit an empty or too-short signature, whatever the cached summary says.
    if (method === 'OnScreenSignature' && liveSummary().points < MIN_TOTAL_POINTS) {
      setConfirmOpen(false);
      setErrors({
        fields: { signature: 'The signature is missing. Sign again, or upload a signed form.' },
        root: null,
      });
      return;
    }
    try {
      await onSubmit({
        catId,
        appointmentId,
        kind,
        procedureDescription: procedure.trim(),
        method,
        ...(method === 'OnScreenSignature'
          ? { signature: pad.current?.getStrokes() ?? [] }
          : { fileId: file!.fileId }),
        signedByName: shownSigner.trim(),
        signerRelationship: relationship,
        ...(ownerCustomerId && relationship === 'Owner'
          ? { signedByCustomerId: ownerCustomerId }
          : {}),
        policyVersionNumber: wording.versionNumber,
        ...(witnessId ? { witnessStaffId: witnessId } : {}),
      });
    } catch (error) {
      const mapped = mapClinicalFormErrors(error, {
        ProcedureDescription: 'procedure',
        SignedByName: 'signer',
        Signature: 'signature',
        FileId: 'file',
      });
      setErrors(mapped);
      void form.refetch();
    } finally {
      setConfirmOpen(false);
    }
  }

  const input =
    'w-full rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent';
  return (
    <form
      noValidate
      className="flex flex-col gap-5"
      onSubmit={(event) => {
        event.preventDefault();
        if (validate()) setConfirmOpen(true);
      }}
    >
      <VisitCatHeader catId={catId} />
      <KindSelect kind={kind} onChange={onKindChange} />

      <section
        aria-labelledby="consent-wording"
        className="rounded-[var(--radius-md)] border border-border bg-surface p-4"
      >
        <div className="mb-2 flex flex-wrap items-baseline justify-between gap-2">
          <h2 id="consent-wording" className="text-base font-semibold text-ink">
            {wording.title}
          </h2>
          <p className="text-xs text-ink-muted">Version {wording.versionNumber}</p>
        </div>
        <LimitedMarkup
          body={wording.body}
          className="space-y-2 text-sm text-ink [&_h2]:font-semibold [&_h3]:font-semibold [&_ul]:list-disc [&_ul]:pl-5"
        />
      </section>

      <div className="flex flex-col gap-1.5">
        <label htmlFor="consent-procedure" className="text-sm font-semibold text-ink">
          Procedure
        </label>
        <textarea
          id="consent-procedure"
          rows={2}
          value={procedure}
          onChange={(e) => setProcedure(e.target.value)}
          aria-invalid={errors.fields.procedure ? true : undefined}
          className={input}
        />
        {errors.fields.procedure && (
          <p role="alert" className="text-xs font-semibold text-danger-ink">
            {errors.fields.procedure}
          </p>
        )}
      </div>

      <div className="grid gap-4 min-[560px]:grid-cols-2">
        <div className="flex flex-col gap-1.5">
          <label htmlFor="consent-signer" className="text-sm font-semibold text-ink">
            Signer’s name
          </label>
          <input
            id="consent-signer"
            type="text"
            value={shownSigner}
            onChange={(e) => {
              setSignerEdited(true);
              setSignerName(e.target.value);
            }}
            aria-invalid={errors.fields.signer ? true : undefined}
            className={input}
          />
          {errors.fields.signer && (
            <p role="alert" className="text-xs font-semibold text-danger-ink">
              {errors.fields.signer}
            </p>
          )}
        </div>
        <fieldset className="m-0 flex flex-col gap-1.5 border-0 p-0">
          <legend className="text-sm font-semibold text-ink">Signing as</legend>
          <div className="flex flex-wrap gap-4">
            {(
              [
                ['Owner', 'Owner'],
                ['AuthorisedAgent', 'Authorised agent'],
              ] as const
            ).map(([value, label]) => (
              <label key={value} className="inline-flex items-center gap-1.5 text-sm text-ink">
                <input
                  type="radio"
                  name="consent-relationship"
                  checked={relationship === value}
                  onChange={() => setRelationship(value)}
                />{' '}
                {label}
              </label>
            ))}
          </div>
        </fieldset>
        {witnesses.length > 0 && (
          <div className="flex flex-col gap-1.5">
            <label htmlFor="consent-witness" className="text-sm font-semibold text-ink">
              Witness <span className="font-normal text-ink-muted">(optional)</span>
            </label>
            <select
              id="consent-witness"
              value={witnessId}
              onChange={(e) => setWitnessId(e.target.value)}
              className={input}
            >
              <option value="">No witness</option>
              {witnesses.map((w) => (
                <option key={w.id} value={w.id}>
                  {w.name}
                </option>
              ))}
            </select>
          </div>
        )}
      </div>

      <section aria-labelledby="consent-sign" className="flex flex-col gap-3">
        <h2 id="consent-sign" className="text-sm font-bold uppercase tracking-wide text-ink-muted">
          How is it signed?
        </h2>
        <div
          role="tablist"
          aria-label="Signing method"
          className="flex flex-wrap gap-1 border-b border-border"
        >
          {(
            [
              ['OnScreenSignature', 'Sign on screen'],
              ['SignedUpload', 'Upload signed form'],
            ] as const
          ).map(([value, label]) => (
            <button
              key={value}
              id={`consent-tab-${value}`}
              type="button"
              role="tab"
              aria-selected={method === value}
              aria-controls="consent-sign-panel"
              tabIndex={method === value ? 0 : -1}
              onClick={() => setMethod(value)}
              onKeyDown={(event) => {
                if (event.key === 'ArrowRight' || event.key === 'ArrowLeft') {
                  const next = value === 'OnScreenSignature' ? 'SignedUpload' : 'OnScreenSignature';
                  setMethod(next);
                  requestAnimationFrame(() =>
                    document.getElementById(`consent-tab-${next}`)?.focus(),
                  );
                }
              }}
              className={cn(
                '-mb-px border-b-2 px-4 py-2 text-sm font-semibold focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent',
                method === value
                  ? 'border-accent text-accent-ink'
                  : 'border-transparent text-ink-muted',
              )}
            >
              {label}
            </button>
          ))}
        </div>
        <div id="consent-sign-panel" role="tabpanel" aria-labelledby={`consent-tab-${method}`}>
          <>
            <div hidden={method !== 'OnScreenSignature'} className="flex flex-col gap-2">
              <p id="consent-pad-help" className="text-xs text-ink-muted">
                Draw with a finger, pen or mouse. If drawing is difficult, use{' '}
                <strong>Upload signed form</strong> instead.
              </p>
              <SignaturePad ref={pad} describedBy="consent-pad-help" onChange={setSignature} />
              {errors.fields.signature && (
                <p role="alert" className="text-xs font-semibold text-danger-ink">
                  {errors.fields.signature}
                </p>
              )}
            </div>
            <div hidden={method !== 'SignedUpload'} className="flex flex-col gap-2">
              <FileUploadField
                id="consent-file"
                label="Upload the signed paper form"
                purpose="ConsentForm"
                value={file}
                onChange={setFile}
              />
              {errors.fields.file && (
                <p role="alert" className="text-xs font-semibold text-danger-ink">
                  {errors.fields.file}
                </p>
              )}
            </div>
          </>
        </div>
      </section>

      {errors.root && (
        <p role="alert" className="text-sm font-semibold text-danger-ink">
          {errors.root}
        </p>
      )}
      <div>
        <Button type="submit">Record consent</Button>
      </div>

      <ConfirmDialog
        open={confirmOpen}
        onOpenChange={setConfirmOpen}
        title="Record this consent?"
        consequence={`${shownSigner.trim()} will be recorded as having given ${kind.toLowerCase()} consent (form version ${wording.versionNumber}) for: ${procedure.trim()}. It becomes part of the record; it can later be withdrawn but not edited.`}
        confirmLabel="Record consent"
        pendingLabel="Recording…"
        onConfirm={record}
      />
    </form>
  );
}

function KindSelect({
  kind,
  onChange,
}: {
  kind: ConsentKind;
  onChange: (kind: ConsentKind) => void;
}) {
  return (
    <div className="flex max-w-xs flex-col gap-1.5">
      <label htmlFor="consent-kind" className="text-sm font-semibold text-ink">
        Consent for
      </label>
      <select
        id="consent-kind"
        value={kind}
        onChange={(event) => onChange(event.target.value as ConsentKind)}
        className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
      >
        {KINDS.map((value) => (
          <option key={value} value={value}>
            {value}
          </option>
        ))}
      </select>
    </div>
  );
}
