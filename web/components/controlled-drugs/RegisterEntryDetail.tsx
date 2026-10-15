'use client';

import Link from 'next/link';
import { PencilLine } from 'lucide-react';
import { buttonVariants } from '@/components/ui/button';
import { Quantity, SignedQuantity } from '@/components/common/Quantity';
import { DefinitionList, Panel } from '@/components/inventory/fields';
import type { RegisterEntry, RegisterEntryDetail as DetailData } from '@/lib/api/controlled-drugs';
import { formatDay, formatInstantInZone } from '@/lib/format/pharmacy';
import { SealBadge, sourceHref } from './RegisterTable';

/**
 * `RegisterEntryDetail` — HO-PHM-002 §11.6 (FR-PHM-7, C-2, ADR-FE-065). The entry exactly as recorded,
 * its seal (chain position and hash, in a monospace block), and the **correction chain beneath the
 * original**, each correction with its reason. The original stays visible and unchanged: **there is no
 * edit or delete control**. *Add correction* appears for vets only (owner decision 2026-10-10: an
 * administrator may reverse a dispensing but cannot correct the register) and creates a NEW linked entry.
 */
function EntryFacts({ entry, zone }: { entry: RegisterEntry; zone: string }) {
  const href = sourceHref(entry);
  return (
    <DefinitionList
      items={[
        { label: 'Kind', value: entry.kind.replace(/([a-z])([A-Z])/g, '$1 $2') },
        { label: 'Recorded', value: formatInstantInZone(entry.recordedOn, zone) },
        { label: 'Happened', value: formatInstantInZone(entry.occurredOn, zone) },
        { label: 'Medicine', value: `${entry.medicine.name} (${entry.medicine.strengthText})` },
        {
          label: 'Lot',
          value: entry.lotNumber
            ? `${entry.lotNumber}${entry.batchExpiresOn ? `, expires ${formatDay(entry.batchExpiresOn)}` : ''}`
            : 'No lot',
        },
        { label: 'Quantity', value: <SignedQuantity value={entry.quantityDelta} /> },
        { label: 'Running balance', value: <Quantity value={entry.runningBalance} /> },
        ...(entry.doseText
          ? [{ label: 'Dose', value: <span data-clinical-value>{entry.doseText}</span> }]
          : []),
        ...(entry.wastage ? [{ label: 'Wastage', value: <Quantity value={entry.wastage} /> }] : []),
        {
          label: 'Cat',
          value: entry.catName
            ? `${entry.catName}${entry.customerName ? `, owner ${entry.customerName}` : ''}`
            : 'Not linked to a cat',
        },
        { label: 'Recorded by', value: entry.actorName },
        { label: 'Witness', value: entry.witnessName ?? 'No witness' },
        {
          label: 'Source',
          value: href ? (
            <Link href={href} className="font-semibold text-accent-ink underline">
              {entry.source.label}
            </Link>
          ) : (
            entry.source.label
          ),
        },
        ...(entry.notes
          ? [{ label: 'Notes', value: <span className="whitespace-pre-wrap">{entry.notes}</span> }]
          : []),
        { label: 'Seal', value: <SealBadge sealed={entry.isSealed} /> },
      ]}
    />
  );
}

export function RegisterEntryDetail({
  detail,
  zone,
  canCorrect,
}: {
  detail: DetailData;
  zone: string;
  canCorrect: boolean;
}) {
  const { entry, corrections, seal } = detail;
  return (
    <article aria-label={`Register entry ${entry.entryNumber}`} className="flex flex-col gap-4">
      <header className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="font-display text-xl font-semibold text-ink">
            Register entry #{entry.entryNumber}
          </h1>
          <p className="mt-1 max-w-2xl text-sm text-ink-muted">
            A register entry can never be changed or deleted. If something was recorded wrongly, add
            a correction: a new entry linked to this one. This entry stays exactly as it is.
          </p>
        </div>
        {canCorrect && (
          <Link
            href={`/staff/pharmacy/controlled-drugs/${entry.id}/correct`}
            className={buttonVariants({ variant: 'secondary', size: 'md' })}
          >
            <PencilLine size={14} aria-hidden="true" />
            Add correction
          </Link>
        )}
      </header>
      <Panel title="The entry" headingId="re-entry">
        <EntryFacts entry={entry} zone={zone} />
      </Panel>
      <Panel title="Seal" headingId="re-seal">
        {seal ? (
          <div className="flex flex-col gap-2 text-sm">
            <p>
              Chain position <b>{seal.chainPosition}</b>, sealed{' '}
              {formatInstantInZone(seal.sealedOn, zone)}.
            </p>
            <pre className="m-0 overflow-x-auto whitespace-pre-wrap break-all rounded-[var(--radius-sm)] bg-surface-sunken p-2 font-mono text-xs text-ink">
              {seal.entryHash}
            </pre>
          </div>
        ) : (
          <p className="text-sm text-ink-muted">
            This entry is awaiting its seal. It is added to the chain within a few minutes.
          </p>
        )}
      </Panel>
      <Panel title={`Corrections (${corrections.length})`} headingId="re-corrections">
        {corrections.length === 0 ? (
          <p className="text-sm text-ink-muted">Nothing has corrected this entry.</p>
        ) : (
          <ol className="m-0 flex list-none flex-col gap-3 p-0">
            {corrections.map((correction) => (
              <li
                key={correction.id}
                className="rounded-[var(--radius-sm)] border border-border p-3 text-sm"
              >
                <p className="mb-1 font-semibold text-ink">
                  <Link
                    href={`/staff/pharmacy/controlled-drugs/${correction.id}`}
                    className="text-accent-ink underline"
                  >
                    Entry #{correction.entryNumber}
                  </Link>{' '}
                  corrects #{entry.entryNumber}, {formatInstantInZone(correction.recordedOn, zone)},
                  by {correction.actorName}
                </p>
                <p className="whitespace-pre-wrap break-words">
                  <b>Reason:</b> {correction.correctionReason ?? 'No reason recorded'}
                </p>
                <p className="mt-1">
                  Quantity change <SignedQuantity value={correction.quantityDelta} />
                  {correction.doseText && (
                    <>
                      , dose now <span data-clinical-value>{correction.doseText}</span>
                    </>
                  )}
                </p>
              </li>
            ))}
          </ol>
        )}
      </Panel>
      {entry.correctsEntryNumber !== null && (
        <p className="text-sm text-ink-muted">This entry corrects #{entry.correctsEntryNumber}.</p>
      )}
    </article>
  );
}
