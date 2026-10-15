'use client';

import Link from 'next/link';
import { CheckCircle2, Hourglass } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Quantity, SignedQuantity } from '@/components/common/Quantity';
import { ResponsiveTable, type Column, type Pager } from '@/components/billing/ResponsiveTable';
import type { RegisterEntry } from '@/lib/api/controlled-drugs';
import { formatInstantInZone } from '@/lib/format/pharmacy';

/**
 * ⭐ `RegisterTable` — HO-PHM-002 §11.6 (FR-PHM-7, C-2, ADR-FE-065). The controlled-drug register,
 * newest first: entry number, when it was recorded, the kind in words, medicine and lot, the SIGNED
 * quantity with its unit, the RUNNING BALANCE as of that entry, dose/wastage, cat and owner, who did it
 * and who witnessed, the source (a link to the dispensing where there is one), the correction chain and
 * whether the entry is sealed (*Sealed* / *Awaiting seal*, an icon and a word).
 *
 * **There is no edit or delete control anywhere** — the register cannot be changed; a correction is a
 * new, linked entry (added from an entry's page, by a vet). From 768 px this is a table; below it each
 * entry is a card (UI-1).
 */
export function sourceHref(entry: RegisterEntry): string | null {
  const type = entry.source.type;
  return type === 'Dispensing' || type === 'DispensingReversal'
    ? `/staff/pharmacy/dispensings/${entry.source.id}`
    : null;
}

export function SealBadge({ sealed }: { sealed: boolean }) {
  return sealed ? (
    <Badge variant="success" icon={<CheckCircle2 size={12} aria-hidden="true" />}>
      Sealed
    </Badge>
  ) : (
    <Badge variant="warning" icon={<Hourglass size={12} aria-hidden="true" />}>
      Awaiting seal
    </Badge>
  );
}

export function RegisterTable({
  rows,
  zone,
  pager,
  isLoading,
  isError,
  onRetry,
}: {
  rows: RegisterEntry[] | undefined;
  zone: string;
  pager: Pager;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
}) {
  const columns: Array<Column<RegisterEntry>> = [
    {
      id: 'entry',
      header: 'Entry',
      primary: true,
      cell: (entry) => (
        <span className="flex flex-col">
          <span>#{entry.entryNumber}</span>
          <span className="text-xs font-normal text-ink-muted">
            {formatInstantInZone(entry.recordedOn, zone)}
          </span>
        </span>
      ),
    },
    { id: 'kind', header: 'Kind', cell: (entry) => entry.kind.replace(/([a-z])([A-Z])/g, '$1 $2') },
    {
      id: 'medicine',
      header: 'Medicine',
      cell: (entry) => (
        <span className="flex flex-col">
          <span>{entry.medicine.name}</span>
          <span className="text-xs text-ink-muted">
            {entry.lotNumber ? `Lot ${entry.lotNumber}` : 'No lot'}
          </span>
        </span>
      ),
    },
    {
      id: 'quantity',
      header: 'Quantity',
      cell: (entry) => <SignedQuantity value={entry.quantityDelta} />,
    },
    {
      id: 'balance',
      header: 'Running balance',
      cell: (entry) => <Quantity value={entry.runningBalance} />,
    },
    {
      id: 'details',
      header: 'Details',
      cell: (entry) => (
        <span className="flex flex-col gap-0.5 text-xs">
          {entry.doseText && <span data-clinical-value>Dose: {entry.doseText}</span>}
          {entry.wastage && (
            <span>
              Wastage: <Quantity value={entry.wastage} />
            </span>
          )}
          {entry.catName && (
            <span>
              Cat: {entry.catName}
              {entry.customerName ? ` (${entry.customerName})` : ''}
            </span>
          )}
          <span>By {entry.actorName}</span>
          <span>{entry.witnessName ? `Witness ${entry.witnessName}` : 'No witness'}</span>
        </span>
      ),
    },
    {
      id: 'source',
      header: 'Source',
      cell: (entry) => {
        const href = sourceHref(entry);
        return (
          <span className="flex flex-col gap-0.5 text-xs">
            {href ? (
              <Link href={href} className="font-semibold text-accent-ink underline">
                {entry.source.label}
              </Link>
            ) : (
              <span>{entry.source.label}</span>
            )}
            {entry.correctsEntryNumber !== null && (
              <span>Corrects #{entry.correctsEntryNumber}</span>
            )}
            {entry.correctedByEntryNumbers.length > 0 && (
              <span>
                Corrected by {entry.correctedByEntryNumbers.map((n) => `#${n}`).join(', ')}
              </span>
            )}
            {entry.correctionReason && (
              <span className="text-ink-muted">Reason: {entry.correctionReason}</span>
            )}
          </span>
        );
      },
    },
    { id: 'seal', header: 'Seal', cell: (entry) => <SealBadge sealed={entry.isSealed} /> },
  ];

  return (
    <ResponsiveTable
      caption="Controlled-drug register entries"
      columns={columns}
      rows={rows}
      getRowId={(entry) => entry.id}
      rowHref={(entry) => `/staff/pharmacy/controlled-drugs/${entry.id}`}
      isLoading={isLoading}
      isError={isError}
      onRetry={onRetry}
      pager={pager}
      dense
      emptyState={{
        title: 'No register entries match',
        description: 'Clear a filter to see more of the register.',
      }}
    />
  );
}
