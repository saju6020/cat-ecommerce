'use client';

import * as React from 'react';
import Link from 'next/link';
import { OctagonX } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ClinicalText, Quantity } from '@/components/common/Quantity';
import { DefinitionList, Panel, TextAreaField } from '@/components/inventory/fields';
import { VisitCatHeader } from '@/components/visit/VisitCatHeader';
import { ApiConflictError, ApiValidationError } from '@/lib/api/client';
import { describePharmacyError, pharmacyCode, pharmacyViolations } from '@/lib/api/errors';
import type {
  DispensePlan,
  DispenseRequest,
  DispenseResult as DispenseResultData,
} from '@/lib/api/pharmacy';
import {
  formatDay,
  formatInstantInZone,
  fromScaled,
  sameAmount,
  toScaled,
} from '@/lib/format/pharmacy';
import { unitLabel } from '@/lib/format/units';
import { BatchPlanTable } from './BatchPlanTable';
import { BatchScanField } from './BatchScanField';
import { DispenseResult } from './DispenseResult';
import { WitnessDialog, type WitnessCredentials } from './WitnessDialog';

/**
 * ⭐ `DispenseScreen` — HO-PHM-002 §11.4, ADR-FE-062/063 (US-PHM-05, US-PHM-04 AC2, US-PHM-06 AC1).
 * It FOLLOWS THE SERVER'S PLAN: the suggested batches are preselected, expired and non-dispensable
 * ones are listed disabled with their reason, choosing away from the suggestion asks for a reason, and
 * **when the plan says `canDispense: false` (only expired stock, an ended validity, no supplies left)
 * there is no Dispense button in the DOM at all** — there is no override (BR-3). The client adds up the
 * quantities chosen ("8 of 10 ml selected") but never picks a batch or compares an expiry date.
 * A controlled medicine opens the witness dialog BEFORE any request is sent.
 */
interface Chosen {
  [batchId: string]: string;
}

function fromSuggestion(plan: DispensePlan): Chosen {
  return Object.fromEntries(
    plan.suggestedLines.map((line) => [line.batchId, line.quantity.amount]),
  );
}

export function DispenseScreen({
  plan,
  repeatRequest,
  zone,
  onDispense,
  onPlanChanged,
}: {
  plan: DispensePlan;
  /** The owner's repeat request this supply answers, when staff came from the queue's second section. */
  repeatRequest: { id: string; note: string | null; requestedOn: string } | null;
  zone: string;
  onDispense: (request: DispenseRequest) => Promise<DispenseResultData>;
  /** After a `400`/`409`: refetch the plan — the stock may have moved on. */
  onPlanChanged: () => void;
}) {
  const [chosen, setChosen] = React.useState<Chosen>(() => fromSuggestion(plan));
  const [reason, setReason] = React.useState('');
  const [highlight, setHighlight] = React.useState<string | null>(null);
  const [error, setError] = React.useState<string | null>(null);
  const [refreshed, setRefreshed] = React.useState(false);
  const [witnessOpen, setWitnessOpen] = React.useState(false);
  const [witnessError, setWitnessError] = React.useState<string | null>(null);
  const [busy, setBusy] = React.useState(false);
  const [result, setResult] = React.useState<DispenseResultData | null>(null);

  // A refetched plan (the stock moved on) starts again from the server's new suggestion.
  const signature = JSON.stringify([
    plan.suggestedLines,
    plan.batches.map((b) => [b.batchId, b.available.amount]),
  ]);
  const seen = React.useRef(signature);
  React.useEffect(() => {
    if (seen.current === signature) return;
    seen.current = signature;
    setChosen(fromSuggestion(plan));
    setRefreshed(true);
  }, [signature, plan]);

  const unit = plan.quantity.unit;
  const unitText = unitLabel(unit, '2');
  const selected = Object.entries(chosen).filter(([, text]) => text.trim() !== '');
  const total = fromScaled(
    selected.reduce(
      (sum, [, text]) => sum + (Number.isNaN(toScaled(text)) ? 0 : toScaled(text)),
      0,
    ),
  );
  const invalidQuantity = selected.some(
    ([, text]) => Number.isNaN(toScaled(text)) || toScaled(text) === 0,
  );
  const suggestedIds = new Set(plan.suggestedLines.map((line) => line.batchId));
  const departs =
    selected.length !== plan.suggestedLines.length ||
    selected.some(([batchId, text]) => {
      const suggested = plan.suggestedLines.find((line) => line.batchId === batchId);
      return !suggested || !sameAmount(suggested.quantity.amount, text);
    });
  const totalOk = sameAmount(total, plan.quantity.amount);
  const blocked = !totalOk
    ? `Choose batches that add up to ${plan.quantity.amount} ${unitText}.`
    : invalidQuantity
      ? 'Enter a quantity greater than zero for each chosen batch.'
      : departs && reason.trim() === ''
        ? 'Say why you are not using the earliest-expiring batch.'
        : null;

  function scan(code: string) {
    const needle = code.trim().toLowerCase();
    const batch = plan.batches.find(
      (b) => b.lotNumber.toLowerCase() === needle || (b.barcode ?? '').toLowerCase() === needle,
    );
    if (!batch)
      return { matched: false, message: `Lot ${code} is not in this prescription’s batch list.` };
    if (batch.isExpired)
      return {
        matched: false,
        message: `Lot ${batch.lotNumber} expired ${formatDay(batch.expiresOn)} and cannot be dispensed.`,
      };
    if (!batch.isDispensable)
      return {
        matched: false,
        message: `Lot ${batch.lotNumber} cannot be dispensed: ${batch.notDispensableReason ?? 'not dispensable'}`,
      };
    const take = Math.min(toScaled(batch.available.amount), toScaled(plan.quantity.amount));
    setChosen({ [batch.batchId]: fromScaled(take) });
    setHighlight(batch.batchId);
    return { matched: true, message: `Selected lot ${batch.lotNumber}.` };
  }

  async function run(witness?: WitnessCredentials) {
    setBusy(true);
    setError(null);
    setWitnessError(null);
    try {
      const response = await onDispense({
        lines: selected.map(([batchId, text]) => ({
          skuId: plan.batches.find((b) => b.batchId === batchId)!.skuId,
          batchId,
          quantity: Number(text),
        })),
        fefoOverrideReason: departs ? reason.trim() : null,
        repeatRequestId: repeatRequest?.id ?? null,
        ...(witness
          ? { witnessIdentifier: witness.identifier, witnessPassword: witness.password }
          : {}),
      });
      setWitnessOpen(false);
      setResult(response);
    } catch (failure) {
      if (pharmacyCode(failure, 'WitnessNotVerified', 'WitnessRequired')) {
        setWitnessError(pharmacyViolations(failure)[0]?.message ?? describePharmacyError(failure));
      } else {
        setWitnessOpen(false);
        setError(describePharmacyError(failure));
        if (failure instanceof ApiValidationError || failure instanceof ApiConflictError)
          onPlanChanged();
      }
    } finally {
      setBusy(false);
    }
  }

  const summary = plan.prescription;
  return (
    <div className="flex flex-col gap-4">
      <VisitCatHeader catId={plan.cat.id} />
      <Panel title="Prescription" headingId="ds-rx">
        <DefinitionList
          items={[
            { label: 'Number', value: summary.prescriptionNumber },
            {
              label: 'Medicine',
              value: (
                <>
                  {summary.medicine.name} <ClinicalText>{summary.medicine.strength}</ClinicalText>{' '}
                  {plan.isControlled && <Badge variant="warning">Controlled</Badge>}
                </>
              ),
            },
            { label: 'Dose', value: <Quantity value={summary.dose} /> },
            { label: 'Directions', value: `${summary.route}, ${summary.frequency}` },
            { label: 'Quantity this supply', value: <Quantity value={plan.quantity} /> },
            {
              label: 'Supply',
              value: `${summary.suppliesDispensed + 1} of ${summary.suppliesAuthorised}`,
            },
            { label: 'Valid until', value: formatDay(summary.validUntil) },
          ]}
        />
      </Panel>
      {repeatRequest && (
        <p
          role="status"
          className="rounded-[var(--radius-sm)] bg-accent-soft px-3 py-2 text-sm text-accent-ink"
        >
          <b>Repeat request from the owner</b>,{' '}
          {formatInstantInZone(repeatRequest.requestedOn, zone)}.
          {repeatRequest.note && (
            <span className="block whitespace-pre-wrap break-words">“{repeatRequest.note}”</span>
          )}
        </p>
      )}

      {result ? (
        <DispenseResult result={result} suppliesAuthorised={summary.suppliesAuthorised} />
      ) : !plan.canDispense ? (
        <section
          role="alert"
          aria-label="This cannot be dispensed"
          className="flex flex-col gap-3 rounded-[var(--radius-md)] border border-danger-ink bg-danger-bg p-4"
        >
          <p className="flex items-start gap-2 text-sm font-semibold text-danger-ink">
            <OctagonX size={18} className="mt-0.5 flex-none" aria-hidden="true" />
            <span>{plan.blockingReason ?? 'This prescription cannot be dispensed.'}</span>
          </p>
          <p className="text-sm text-ink">There is no way to override this here.</p>
          <div>
            <Link
              href="/staff/pharmacy"
              className="text-sm font-semibold text-accent-ink underline"
            >
              Back to queue
            </Link>
          </div>
        </section>
      ) : (
        <form
          noValidate
          aria-label="Dispense"
          onSubmit={(event) => {
            event.preventDefault();
            if (blocked || busy) return;
            if (plan.isControlled) {
              setWitnessError(null);
              setWitnessOpen(true);
            } else void run();
          }}
          className="flex flex-col gap-4"
        >
          {refreshed && (
            <p role="status" className="text-sm font-semibold text-warning-ink">
              The batch list was refreshed because stock changed. Check the batches before
              dispensing.
            </p>
          )}
          <BatchScanField id="ds-scan" onScan={scan} />
          <BatchPlanTable
            batches={plan.batches}
            suggestedIds={suggestedIds}
            selection={chosen}
            unit={unit}
            needed={plan.quantity.amount}
            highlightId={highlight}
            onChange={(batchId, text) => {
              setChosen({ ...chosen, [batchId]: text });
              setHighlight(null);
            }}
          />
          <p aria-live="polite" className="text-sm font-semibold text-ink">
            <span data-clinical-value>
              {total} of {plan.quantity.amount} {unitText}
            </span>{' '}
            selected
          </p>
          {departs && selected.length > 0 && (
            <TextAreaField
              id="ds-fefo-reason"
              label="Reason for not using the earliest-expiring batch"
              required
              rows={2}
              maxLength={500}
              value={reason}
              hint="Recorded with this supply."
              onChange={(event) => setReason(event.target.value)}
            />
          )}
          {error && (
            <p role="alert" className="text-sm font-semibold text-danger-ink">
              {error}
            </p>
          )}
          <div className="flex flex-wrap items-center gap-3">
            <Button
              type="submit"
              disabled={Boolean(blocked) || busy}
              aria-busy={busy}
              aria-describedby="ds-blocked"
            >
              {plan.isControlled ? 'Dispense (witness needed)' : 'Dispense'}
            </Button>
            <span id="ds-blocked" className="text-sm text-ink-muted">
              {blocked ?? (plan.isControlled ? 'A witness will be asked to sign in next.' : '')}
            </span>
          </div>
        </form>
      )}
      <WitnessDialog
        open={witnessOpen}
        witnessRoles={plan.witnessRoles}
        busy={busy}
        error={witnessError}
        onCancel={() => {
          setWitnessOpen(false);
          setWitnessError(null);
        }}
        onSubmit={(credentials) => void run(credentials)}
      />
    </div>
  );
}
