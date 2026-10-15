'use client';

import { ExportButton } from '@/components/inventory/ExportButton';
import { SelectField, TextField } from '@/components/inventory/fields';
import {
  REGISTER_ENTRY_KINDS,
  registerExportPath,
  type RegisterFilters,
} from '@/lib/api/controlled-drugs';

/**
 * `RegisterToolbar` — HO-PHM-002 §11.6, UI-4. Filters (medicine, lot, kind, date range) are URL
 * parameters owned by the page; **Export CSV** is the SERVER'S file for the same filters the export
 * endpoint understands (medicine and dates). It is fetched with the branch header — a plain link
 * could not carry it and would export the wrong branch's register (HO-PHM-002 §1 Amended).
 */
export function RegisterToolbar({
  filters,
  medicines,
  lots,
  onChange,
}: {
  filters: RegisterFilters;
  medicines: Array<{ id: string; name: string }>;
  lots: Array<{ batchId: string; label: string }>;
  onChange: (next: Partial<RegisterFilters>) => void;
}) {
  return (
    <div className="flex flex-col gap-3">
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
        <SelectField
          id="rt-medicine"
          label="Medicine"
          value={filters.medicineId ?? ''}
          onChange={(event) => onChange({ medicineId: event.target.value || undefined })}
        >
          <option value="">All controlled medicines</option>
          {medicines.map((medicine) => (
            <option key={medicine.id} value={medicine.id}>
              {medicine.name}
            </option>
          ))}
        </SelectField>
        <SelectField
          id="rt-lot"
          label="Lot"
          value={filters.batchId ?? ''}
          onChange={(event) => onChange({ batchId: event.target.value || undefined })}
        >
          <option value="">All lots</option>
          {lots.map((lot) => (
            <option key={lot.batchId} value={lot.batchId}>
              {lot.label}
            </option>
          ))}
        </SelectField>
        <SelectField
          id="rt-kind"
          label="Kind"
          value={filters.kind ?? ''}
          onChange={(event) => onChange({ kind: event.target.value || undefined })}
        >
          <option value="">Every kind</option>
          {REGISTER_ENTRY_KINDS.map((kind) => (
            <option key={kind} value={kind}>
              {kind.replace(/([a-z])([A-Z])/g, '$1 $2')}
            </option>
          ))}
        </SelectField>
        <TextField
          id="rt-from"
          type="date"
          label="From"
          value={filters.from ?? ''}
          onChange={(event) => onChange({ from: event.target.value || undefined })}
        />
        <TextField
          id="rt-to"
          type="date"
          label="To"
          value={filters.to ?? ''}
          onChange={(event) => onChange({ to: event.target.value || undefined })}
        />
      </div>
      <div className="flex flex-wrap items-center gap-3">
        <ExportButton
          path={registerExportPath(filters)}
          fallbackName="controlled-drug-register.csv"
        />
        <span className="text-xs text-ink-muted">
          The export follows the medicine and date filters. Every export is recorded.
        </span>
      </div>
    </div>
  );
}
