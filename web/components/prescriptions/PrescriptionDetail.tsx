'use client';

import * as React from 'react';
import Link from 'next/link';
import { Ban, Pill, RefreshCw, ShieldCheck } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { buttonVariants, Button } from '@/components/ui/button';
import { ClinicalText, Quantity } from '@/components/common/Quantity';
import { ReasonDialog } from '@/components/inventory/ReasonDialog';
import { DefinitionList, Panel } from '@/components/inventory/fields';
import { describePharmacyError } from '@/lib/api/errors';
import type { PrescriptionDetail as PrescriptionDetailData } from '@/lib/api/prescriptions';
import { formatDay, formatInstantInZone, routeLabel } from '@/lib/format/pharmacy';
import { formatDoseRange, formatRemaining } from '@/lib/format/units';
import { PrescriptionStatusBadge } from './PrescriptionStatusBadge';

/**
 * ⭐ `PrescriptionDetail` — HO-PHM-002 §11.3 (US-PHM-01, 06). Everything the detail payload carries:
 * the directions, the instructions (plain text — printed on the label), the **safety record** as it
 * was when the prescription was issued, the warnings as they were shown, every dispensing (each links
 * to its own page), the owner's repeat requests and the renewal links.
 *
 * ADR-FE-065: an issued prescription has NO edit control. The buttons come ONLY from the API's
 * `allowedActions` — Dispense, Renew (a new prescription; every check runs again), Cancel (a reason,
 * and an open repeat request is declined) and Decline on a repeat request (a reason).
 */
const OUTCOME_LABEL: Record<string, string> = {
  WithinRange: 'within range',
  BelowRange: 'below range',
  AboveRange: 'above range',
  NotCheckable: 'not checked against a range',
};

export function PrescriptionDetail({
  detail,
  zone,
  catId,
  onCancel,
  onDeclineRepeat,
}: {
  detail: PrescriptionDetailData;
  zone: string;
  catId: string;
  onCancel: (reason: string) => Promise<unknown>;
  onDeclineRepeat: (requestId: string, reason: string) => Promise<unknown>;
}) {
  const [cancelOpen, setCancelOpen] = React.useState(false);
  const [declining, setDeclining] = React.useState<string | null>(null);
  const can = (action: string) => (detail.allowedActions as string[]).includes(action);
  const record = detail.safetyRecord;

  return (
    <article
      aria-label={`Prescription ${detail.prescriptionNumber}`}
      className="flex flex-col gap-4"
    >
      <header className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <h2 className="font-display text-xl font-semibold text-ink">
            {detail.prescriptionNumber}
          </h2>
          <p className="mt-1 flex flex-wrap items-center gap-2 text-sm text-ink-muted">
            <PrescriptionStatusBadge status={detail.status} />
            {detail.medicine.isControlled && <Badge variant="warning">Controlled</Badge>}
            {detail.isRemoteRenewal && <Badge variant="neutral">Remote renewal</Badge>}
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          {can('Dispense') && (
            <Link
              href={`/staff/pharmacy/dispense/${detail.id}`}
              className={buttonVariants({ variant: 'primary', size: 'md' })}
            >
              <Pill size={14} aria-hidden="true" />
              Dispense
            </Link>
          )}
          {can('Renew') && (
            <Link
              href={`/staff/cats/${catId}/prescriptions/${detail.id}/renew`}
              className={buttonVariants({ variant: 'secondary', size: 'md' })}
            >
              <RefreshCw size={14} aria-hidden="true" />
              Renew
            </Link>
          )}
          {can('Cancel') && (
            <Button variant="destructive" onClick={() => setCancelOpen(true)}>
              <Ban size={14} aria-hidden="true" />
              Cancel prescription
            </Button>
          )}
        </div>
      </header>
      {can('Renew') && (
        <p className="text-sm text-ink-muted">
          Renewing writes a new prescription. All checks run again on today’s weight and records;
          this one stays as it is.
        </p>
      )}

      <Panel title="Directions" headingId="rx-directions">
        <DefinitionList
          items={[
            {
              label: 'Medicine',
              value: (
                <>
                  {detail.medicine.name} <ClinicalText>{detail.medicine.strength}</ClinicalText>
                </>
              ),
            },
            { label: 'Dose', value: <Quantity value={detail.dose} /> },
            { label: 'Route', value: routeLabel(detail.route) },
            { label: 'Frequency', value: detail.frequency },
            { label: 'Duration', value: <ClinicalText>{detail.durationDays} days</ClinicalText> },
            { label: 'Quantity per supply', value: <Quantity value={detail.quantity} /> },
            {
              label: 'Supplies',
              value: `${detail.suppliesDispensed} of ${detail.suppliesAuthorised}`,
            },
            { label: 'Remaining', value: formatRemaining(detail.remainingDays) },
            { label: 'Valid until', value: formatDay(detail.validUntil) },
            {
              label: 'Issued',
              value: `${formatInstantInZone(detail.issuedOn, zone)} by ${detail.prescriberName}`,
            },
            ...(detail.renewedFrom
              ? [
                  {
                    label: 'Renews',
                    value: (
                      <Link
                        href={`/staff/cats/${catId}/prescriptions/${detail.renewedFrom.id}`}
                        className="font-semibold text-accent-ink underline"
                      >
                        {detail.renewedFrom.prescriptionNumber}
                      </Link>
                    ),
                  },
                ]
              : []),
            ...(detail.renewedBy
              ? [
                  {
                    label: 'Renewed by',
                    value: (
                      <Link
                        href={`/staff/cats/${catId}/prescriptions/${detail.renewedBy.id}`}
                        className="font-semibold text-accent-ink underline"
                      >
                        {detail.renewedBy.prescriptionNumber}
                      </Link>
                    ),
                  },
                ]
              : []),
          ]}
        />
        <h3 className="mb-1 mt-4 text-xs font-bold uppercase tracking-wide text-ink-muted">
          Instructions (printed on the label)
        </h3>
        <p className="whitespace-pre-wrap break-words text-sm text-ink">{detail.instructions}</p>
      </Panel>

      <Panel title="Safety record" headingId="rx-safety">
        <ul className="m-0 flex list-none flex-col gap-1.5 p-0 text-sm text-ink">
          <li className="flex gap-2">
            <ShieldCheck size={14} className="mt-0.5 flex-none text-ink-muted" aria-hidden="true" />
            <span>
              {record.weightUsed ? (
                <>
                  Issued with <Quantity value={record.weightUsed} />
                  {record.weightRecordedOn ? ` (${formatDay(record.weightRecordedOn)})` : ''}
                </>
              ) : (
                'No weight was recorded with this prescription.'
              )}
              {'; '}
              {record.doseMgPerKg ? (
                <>
                  dose <ClinicalText>{record.doseMgPerKg}&nbsp;mg/kg</ClinicalText>,{' '}
                  {OUTCOME_LABEL[record.doseCheckOutcome] ?? record.doseCheckOutcome}
                  {record.expectedRange && (
                    <>
                      {' — expected '}
                      <ClinicalText>
                        {formatDoseRange(record.expectedRange, record.weightUsed)}
                      </ClinicalText>
                    </>
                  )}
                </>
              ) : (
                'the dose was not checked against a range'
              )}
            </span>
          </li>
          {record.acknowledgedByName && (
            <li>
              Warnings acknowledged by {record.acknowledgedByName}
              {record.acknowledgedOn
                ? ` on ${formatInstantInZone(record.acknowledgedOn, zone)}`
                : ''}
              .
            </li>
          )}
          {record.allergyAcknowledgementReason && (
            <li className="whitespace-pre-wrap break-words">
              Reason given despite the allergy: “{record.allergyAcknowledgementReason}”
            </li>
          )}
          {record.doseAcknowledgementNote && (
            <li className="whitespace-pre-wrap break-words">
              Dose note: “{record.doseAcknowledgementNote}”
            </li>
          )}
        </ul>
        {detail.warnings.length > 0 && (
          <>
            <h3 className="mb-1 mt-3 text-xs font-bold uppercase tracking-wide text-ink-muted">
              Warnings as they were shown
            </h3>
            <ul className="m-0 flex list-none flex-col gap-2 p-0 text-sm">
              {detail.warnings.map((warning, index) => (
                <li
                  key={`${warning.subject}-${index}`}
                  className="rounded-[var(--radius-sm)] bg-surface-sunken p-2"
                >
                  <b>
                    {warning.blocking
                      ? 'Blocking — '
                      : warning.severity
                        ? `${warning.severity} — `
                        : ''}
                    {warning.subject}
                  </b>
                  <span className="block whitespace-pre-wrap break-words text-ink-muted">
                    {warning.detail}
                  </span>
                </li>
              ))}
            </ul>
          </>
        )}
      </Panel>

      <Panel title="Dispensings" headingId="rx-dispensings">
        {detail.dispensings.length === 0 ? (
          <p className="text-sm text-ink-muted">Nothing has been dispensed yet.</p>
        ) : (
          <ul className="m-0 flex list-none flex-col gap-2 p-0 text-sm">
            {detail.dispensings.map((dispensing) => (
              <li key={dispensing.id} className="flex flex-wrap items-center gap-x-3 gap-y-1">
                <Link
                  href={`/staff/pharmacy/dispensings/${dispensing.id}`}
                  className="font-semibold text-accent-ink underline"
                >
                  Supply {dispensing.supplyNumber}
                  {dispensing.isRepeat ? ' (repeat)' : ''}
                </Link>
                <span>
                  {formatDay(dispensing.dispensedOn)} by {dispensing.dispensedByName}
                </span>
                <span className="text-ink-muted">
                  {dispensing.lines.map((line) => (
                    <span key={line.lineId} className="mr-2">
                      Lot {line.lotNumber}
                      {line.expiresOn ? ` (expires ${formatDay(line.expiresOn)})` : ''},{' '}
                      <Quantity value={line.quantity} />
                    </span>
                  ))}
                </span>
                <Badge variant={dispensing.status === 'Reversed' ? 'danger' : 'success'}>
                  {dispensing.status}
                </Badge>
              </li>
            ))}
          </ul>
        )}
      </Panel>

      {detail.repeatRequests.length > 0 && (
        <Panel title="Repeat requests" headingId="rx-repeats">
          <ul className="m-0 flex list-none flex-col gap-3 p-0 text-sm">
            {detail.repeatRequests.map((request) => (
              <li key={request.id} className="flex flex-wrap items-center justify-between gap-2">
                <span className="min-w-0">
                  Requested {formatInstantInZone(request.requestedOn, zone)} —{' '}
                  <b>{request.status}</b>
                  {request.note && (
                    <span className="block whitespace-pre-wrap break-words text-ink-muted">
                      “{request.note}”
                    </span>
                  )}
                  {request.declineReason && (
                    <span className="block whitespace-pre-wrap break-words text-ink-muted">
                      Declined: {request.declineReason}
                    </span>
                  )}
                </span>
                {request.status === 'Requested' && can('DeclineRepeat') && (
                  <Button variant="secondary" size="sm" onClick={() => setDeclining(request.id)}>
                    Decline request
                  </Button>
                )}
              </li>
            ))}
          </ul>
        </Panel>
      )}

      <ReasonDialog
        open={cancelOpen}
        onOpenChange={setCancelOpen}
        title={`Cancel ${detail.prescriptionNumber}?`}
        consequence={`Cancel ${detail.prescriptionNumber} for ${detail.catName}? Any open repeat request will be declined.`}
        reasonLabel="Reason for cancelling"
        confirmLabel="Cancel prescription"
        variant="destructive"
        describeError={describePharmacyError}
        onConfirm={onCancel}
      />
      <ReasonDialog
        open={declining !== null}
        onOpenChange={(open) => !open && setDeclining(null)}
        title="Decline this repeat request?"
        consequence="The owner is told the request was declined and why."
        reasonLabel="Reason for declining"
        confirmLabel="Decline request"
        variant="destructive"
        describeError={describePharmacyError}
        onConfirm={(reason) => onDeclineRepeat(declining as string, reason)}
      />
    </article>
  );
}
