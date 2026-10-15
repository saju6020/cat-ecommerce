'use client';

import { useState } from 'react';
import { Ban, CircleCheck, ExternalLink } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button, buttonVariants } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  useDialogReturnFocus,
} from '@/components/ui/dialog';
import { useStaffBlobUrl } from '@/lib/hooks/useStaffBlobUrl';
import { describeClinicalError } from '@/lib/api/errors';
import { consentKindLabel, consentMethodLabel, consentStatusLabel } from '@/lib/format/clinical';
import { formatInZone } from '@/lib/format/time';
import type { Consent } from '@/lib/api/consents';

/**
 * ⭐ `ConsentDetail` — HO-EMR-002 §11.6 (US-CLN-21). What was signed: kind, procedure, the form VERSION
 * signed, who signed (and as whom), who captured and witnessed it, the stored signature (the
 * server's polyline-only SVG, shown as an image — never inline markup) or the signed file (Open), the
 * status in words, and **Withdraw** with a required reason (UI-3 confirm). A withdrawn consent shows
 * when, by whom and why. Built on `ui/dialog.tsx`.
 */
/** The on-screen signature is a RELATIVE staff path; it is fetched as a blob so `X-Branch-Id` is sent. */
function signaturePath(consent: Consent): string | null {
  const url = consent.signatureUrl;
  return consent.method === 'OnScreenSignature' && url?.startsWith('/') ? url : null;
}

export function ConsentDetail({
  consent,
  canWithdraw,
  onWithdraw,
}: {
  consent: Consent;
  canWithdraw: boolean;
  onWithdraw: (reason: string) => Promise<void>;
}) {
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);
  const returnFocusRef = useDialogReturnFocus(open);
  const zone = consent.branchTimeZoneId;
  const withdrawn = consent.status === 'Withdrawn';
  const signature = useStaffBlobUrl(signaturePath(consent));

  async function submit() {
    if (!reason.trim()) {
      setError('Say why the consent is being withdrawn.');
      return;
    }
    setPending(true);
    setError(null);
    try {
      await onWithdraw(reason.trim());
      setOpen(false);
      setReason('');
    } catch (caught) {
      setError(describeClinicalError(caught));
    } finally {
      setPending(false);
    }
  }

  return (
    <article
      aria-label={`${consentKindLabel(consent.kind)} consent`}
      className="flex flex-col gap-5"
    >
      <div className="flex flex-wrap items-center gap-3">
        <h1 className="font-display text-2xl font-semibold text-ink">
          {consentKindLabel(consent.kind)} consent
        </h1>
        <Badge
          variant={withdrawn ? 'neutral' : 'success'}
          icon={
            withdrawn ? (
              <Ban size={12} aria-hidden="true" />
            ) : (
              <CircleCheck size={12} aria-hidden="true" />
            )
          }
        >
          {consentStatusLabel(consent.status)}
        </Badge>
      </div>
      <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2 rounded-[var(--radius-md)] border border-border bg-surface p-4 text-sm">
        <dt className="font-semibold text-ink">Procedure</dt>
        <dd className="m-0 whitespace-pre-wrap break-words text-ink-muted">
          {consent.procedureDescription}
        </dd>
        <dt className="font-semibold text-ink">Form signed</dt>
        <dd className="m-0 text-ink-muted">Version {consent.policyVersionNumber}</dd>
        <dt className="font-semibold text-ink">Signed by</dt>
        <dd className="m-0 text-ink-muted">
          {consent.signedByName} (
          {consent.signerRelationship === 'Owner' ? 'owner' : 'authorised agent'})
        </dd>
        <dt className="font-semibold text-ink">Signed on</dt>
        <dd className="m-0 text-ink-muted">{formatInZone(consent.signedOn, zone)}</dd>
        <dt className="font-semibold text-ink">How</dt>
        <dd className="m-0 text-ink-muted">{consentMethodLabel(consent.method)}</dd>
        <dt className="font-semibold text-ink">Recorded by</dt>
        <dd className="m-0 text-ink-muted">{consent.capturedBy}</dd>
        <dt className="font-semibold text-ink">Witness</dt>
        <dd className="m-0 text-ink-muted">{consent.witness ?? 'None recorded'}</dd>
        {withdrawn && (
          <>
            <dt className="font-semibold text-ink">Withdrawn</dt>
            <dd className="m-0 text-ink-muted">
              {consent.withdrawnOn ? formatInZone(consent.withdrawnOn, zone) : ''} by{' '}
              {consent.withdrawnBy}
              <span className="block whitespace-pre-wrap break-words">
                {consent.withdrawalReason}
              </span>
            </dd>
          </>
        )}
      </dl>

      <section aria-labelledby="consent-evidence" className="flex flex-col gap-2">
        <h2
          id="consent-evidence"
          className="text-sm font-bold uppercase tracking-wide text-ink-muted"
        >
          {consent.method === 'OnScreenSignature' ? 'Signature' : 'Signed form'}
        </h2>
        {consent.method === 'OnScreenSignature' && signature.loading ? (
          <p role="status" className="text-sm text-ink-muted">
            Loading the signature…
          </p>
        ) : consent.method === 'OnScreenSignature' && signature.error ? (
          <p role="alert" className="text-sm font-semibold text-danger-ink">
            The signature couldn’t be loaded. Reload the page to try again.
          </p>
        ) : consent.method === 'OnScreenSignature' && signature.url ? (
          // eslint-disable-next-line @next/next/no-img-element
          <img
            src={signature.url}
            alt={`Signature of ${consent.signedByName}`}
            className="h-auto max-w-full rounded-[var(--radius-sm)] border border-border bg-surface"
            width={300}
            height={120}
          />
        ) : consent.file?.url ? (
          <div>
            <a
              href={consent.file.url}
              target="_blank"
              rel="noopener noreferrer"
              className={buttonVariants({ variant: 'secondary', size: 'sm' })}
              aria-label={`Open ${consent.file.originalFileName} in a new tab`}
            >
              <ExternalLink size={14} aria-hidden="true" /> Open {consent.file.originalFileName}
            </a>
          </div>
        ) : (
          <p className="text-sm text-ink-muted">The signature is not available.</p>
        )}
      </section>

      {canWithdraw && !withdrawn && (
        <div>
          <Button variant="destructive" onClick={() => setOpen(true)}>
            Withdraw consent
          </Button>
        </div>
      )}

      <Dialog open={open} onOpenChange={(next) => !pending && setOpen(next)}>
        <DialogContent returnFocusRef={returnFocusRef}>
          <DialogHeader>
            <DialogTitle>Withdraw this consent?</DialogTitle>
            <DialogDescription>
              The {consentKindLabel(consent.kind).toLowerCase()} consent signed by{' '}
              {consent.signedByName} will no longer count as valid for the procedure. The record of
              it stays.
            </DialogDescription>
          </DialogHeader>
          <div className="flex flex-col gap-1.5">
            <label htmlFor="withdraw-reason" className="text-sm font-semibold text-ink">
              Reason
            </label>
            <textarea
              id="withdraw-reason"
              rows={3}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              aria-invalid={error ? true : undefined}
              className="resize-y rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
            />
            {error && (
              <p role="alert" className="text-xs font-semibold text-danger-ink">
                {error}
              </p>
            )}
          </div>
          <DialogFooter className="mt-4">
            <Button variant="secondary" onClick={() => setOpen(false)} disabled={pending}>
              Cancel
            </Button>
            <Button
              variant="destructive"
              onClick={() => void submit()}
              disabled={pending}
              aria-busy={pending}
            >
              {pending ? 'Withdrawing…' : 'Withdraw consent'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </article>
  );
}
