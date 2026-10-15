'use client';

import * as React from 'react';
import { Undo2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Quantity } from '@/components/common/Quantity';
import { ReasonDialog } from '@/components/inventory/ReasonDialog';
import { describePharmacyError, pharmacyCode, pharmacyViolations } from '@/lib/api/errors';
import type { Dispensing } from '@/lib/api/pharmacy';
import { WitnessDialog, type WitnessCredentials } from './WitnessDialog';

/**
 * `ReverseDispensing` — HO-PHM-002 §11.4, UI-3, ADR-FE-065. A reversal is its OWN action that records a
 * new fact (the original dispensing stays visible, marked *Reversed*). The confirmation names exactly
 * what happens: the quantity and batch that go back, and the supply that is undone. Reversal returns
 * stock to the **quarantine** location until Inventory releases it (HO-PHM-001 §1 Amended) — the text
 * says so rather than promising the same shelf. For a controlled medicine the vet or administrator
 * then needs a witness, who signs in here with their own details (owner decision 2026-10-10: an
 * administrator may reverse).
 */
export function ReverseDispensing({
  dispensing,
  isControlled,
  onReverse,
}: {
  dispensing: Dispensing;
  /** The label carries a controlled-drug notice only for a controlled medicine. */
  isControlled: boolean;
  onReverse: (reason: string, witness?: WitnessCredentials) => Promise<unknown>;
}) {
  const [reasonOpen, setReasonOpen] = React.useState(false);
  const [reason, setReason] = React.useState('');
  const [witnessOpen, setWitnessOpen] = React.useState(false);
  const [witnessError, setWitnessError] = React.useState<string | null>(null);
  const [error, setError] = React.useState<string | null>(null);
  const [busy, setBusy] = React.useState(false);

  async function finish(witness?: WitnessCredentials) {
    setBusy(true);
    setWitnessError(null);
    try {
      await onReverse(reason, witness);
      setWitnessOpen(false);
    } catch (failure) {
      if (pharmacyCode(failure, 'WitnessNotVerified', 'WitnessRequired')) {
        setWitnessError(pharmacyViolations(failure)[0]?.message ?? describePharmacyError(failure));
      } else {
        setWitnessOpen(false);
        setError(describePharmacyError(failure));
      }
    } finally {
      setBusy(false);
    }
  }

  return (
    <>
      <Button variant="destructive" onClick={() => setReasonOpen(true)}>
        <Undo2 size={14} aria-hidden="true" />
        Reverse this supply
      </Button>
      {error && (
        <p role="alert" className="basis-full text-sm font-semibold text-danger-ink">
          {error}
        </p>
      )}
      <ReasonDialog
        open={reasonOpen}
        onOpenChange={setReasonOpen}
        title={`Reverse supply ${dispensing.supplyNumber} of ${dispensing.prescriptionNumber}?`}
        consequence={
          <>
            Return{' '}
            {dispensing.lines.map((line, index) => (
              <React.Fragment key={line.lineId}>
                {index > 0 && ' and '}
                <Quantity value={line.quantity} /> of batch {line.lotNumber}
              </React.Fragment>
            ))}{' '}
            to stock (held in quarantine until Inventory releases it) and undo supply{' '}
            {dispensing.supplyNumber} of {dispensing.prescriptionNumber}? The original dispensing
            stays on record, marked Reversed.
          </>
        }
        reasonLabel="Reason for the reversal"
        confirmLabel={isControlled ? 'Continue to witness' : 'Reverse supply'}
        variant="destructive"
        describeError={describePharmacyError}
        onConfirm={async (typed) => {
          setReason(typed);
          setError(null);
          if (isControlled) {
            setWitnessOpen(true);
            return;
          }
          await onReverse(typed);
        }}
      />
      <WitnessDialog
        open={witnessOpen}
        witnessRoles={['Vet', 'Nurse']}
        busy={busy}
        error={witnessError}
        confirmLabel="Confirm and reverse"
        title="Controlled drug — witness needed"
        onCancel={() => {
          setWitnessOpen(false);
          setWitnessError(null);
        }}
        onSubmit={(credentials) => void finish(credentials)}
      />
    </>
  );
}
