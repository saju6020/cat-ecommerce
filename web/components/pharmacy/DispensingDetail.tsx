'use client';

import Link from 'next/link';
import { Printer } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { buttonVariants } from '@/components/ui/button';
import { Quantity } from '@/components/common/Quantity';
import { DefinitionList, Panel } from '@/components/inventory/fields';
import type { Dispensing } from '@/lib/api/pharmacy';
import { formatDay, formatInstantInZone } from '@/lib/format/pharmacy';
import { MedicineLabel } from './MedicineLabel';
import { ReverseDispensing } from './ReverseDispensing';
import type { WitnessCredentials } from './WitnessDialog';

/**
 * `DispensingDetail` — HO-PHM-002 §11.4 (US-PHM-05). One supply: its lines with lot and expiry, who
 * dispensed it and who witnessed, the reason for any non-FEFO batch, the status, the frozen label and
 * how many times it was printed. ADR-FE-065: **no edit control** — the buttons come ONLY from
 * `allowedActions` (*Reprint*, *Reverse*), and a reversal is its own confirmed action.
 */
export function DispensingDetail({
  dispensing,
  zone,
  catId,
  onReverse,
}: {
  dispensing: Dispensing;
  zone: string;
  catId: string;
  onReverse: (reason: string, witness?: WitnessCredentials) => Promise<unknown>;
}) {
  const can = (action: string) => (dispensing.allowedActions as string[]).includes(action);
  const reversed = dispensing.status === 'Reversed';
  return (
    <article
      aria-label={`Supply ${dispensing.supplyNumber} of ${dispensing.prescriptionNumber}`}
      className="flex flex-col gap-4"
    >
      <header className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="font-display text-xl font-semibold text-ink">
            Supply {dispensing.supplyNumber} of {dispensing.prescriptionNumber}
          </h1>
          <p className="mt-1 flex flex-wrap items-center gap-2 text-sm text-ink-muted">
            <Badge variant={reversed ? 'danger' : 'success'}>
              {reversed ? 'Reversed' : 'Dispensed'}
            </Badge>
            {dispensing.isRepeat && <Badge variant="neutral">Repeat</Badge>}
            <Link
              href={`/staff/cats/${catId}/prescriptions/${dispensing.prescriptionId}`}
              className="font-semibold text-accent-ink underline"
            >
              View the prescription
            </Link>
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          {can('Reprint') && !reversed && (
            <Link
              href={`/staff/pharmacy/dispensings/${dispensing.id}/label`}
              className={buttonVariants({ variant: 'secondary', size: 'md' })}
            >
              <Printer size={14} aria-hidden="true" />
              Reprint label
            </Link>
          )}
          {can('Reverse') && (
            <ReverseDispensing
              dispensing={dispensing}
              isControlled={dispensing.label.controlledNotice !== null}
              onReverse={onReverse}
            />
          )}
        </div>
      </header>

      <Panel title="Supply" headingId="dd-supply">
        <DefinitionList
          items={[
            { label: 'Dispensed', value: formatInstantInZone(dispensing.dispensedOn, zone) },
            { label: 'Dispensed by', value: dispensing.dispensedByName },
            { label: 'Witness', value: dispensing.witnessName ?? 'No witness needed' },
            {
              label: 'Batch choice',
              value: dispensing.fefoOverrideReason
                ? `Not the earliest-expiring batch: ${dispensing.fefoOverrideReason}`
                : 'The earliest-expiring batch, as suggested',
            },
            {
              label: 'Label printed',
              value: `${dispensing.labelPrintCount} ${dispensing.labelPrintCount === 1 ? 'time' : 'times'}`,
            },
            ...(reversed
              ? [
                  { label: 'Reversed', value: formatInstantInZone(dispensing.reversedOn, zone) },
                  { label: 'Reason', value: dispensing.reversalReason ?? '—' },
                ]
              : []),
          ]}
        />
        <h2 className="mb-1 mt-4 text-xs font-bold uppercase tracking-wide text-ink-muted">
          Batches
        </h2>
        <ul className="m-0 flex list-none flex-col gap-1 p-0 text-sm">
          {dispensing.lines.map((line) => (
            <li key={line.lineId} className="flex flex-wrap items-center gap-x-3">
              <b>Lot {line.lotNumber}</b>
              <span>expires {formatDay(line.expiresOn)}</span>
              <Quantity value={line.quantity} />
            </li>
          ))}
        </ul>
      </Panel>

      <Panel title="Label (as dispensed)" headingId="dd-label">
        <p className="mb-3 text-sm text-ink-muted">
          This is the label frozen when the supply was dispensed. A reprint is always this same
          label.
        </p>
        <MedicineLabel label={dispensing.label} />
      </Panel>
    </article>
  );
}
