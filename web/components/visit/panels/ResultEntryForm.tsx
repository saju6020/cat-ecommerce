'use client';

import { useEffect, useRef, useState } from 'react';
import { Plus } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { ResultEntryRow, type ResultRowState } from './ResultEntryRow';
import { mapClinicalFormErrors } from '@/lib/api/errors';
import { useLabAnalytes } from '@/lib/hooks/useClinicalCatalogue';
import { formatDateOnly, todayInZone } from '@/lib/format/time';
import { MAX_RESULT_NOTE } from '@/lib/validation/clinical';
import type { LabAnalyte } from '@/lib/api/clinical-catalogue';
import type {
  DiagnosticOrder,
  RecordResultsRequest,
  RecordedResultValue,
} from '@/lib/api/diagnostics';

/**
 * ⭐ `ResultEntryForm` — HO-EMR-002 §11.3 (US-CLN-14 AC2). Rows (an analyte pre-fills name, unit and
 * range, all editable; numeric or text results) with a live PREVIEW flag per row, a reported-on date
 * (not in the future), a note and — when earlier sets exist — a *Supersedes* selector so a correction
 * stays append-only. The server's flag, returned by the save, is what the record shows afterwards.
 */
let rowCounter = 0;
const nextKey = () => `row-${(rowCounter += 1)}`;

function rowFromAnalyte(analyte: LabAnalyte): ResultRowState {
  return {
    key: nextKey(),
    analyteId: analyte.id,
    name: analyte.name,
    valueKind: analyte.valueKind,
    value: '',
    unit: analyte.unit ?? '',
    low: analyte.referenceLow ?? '',
    high: analyte.referenceHigh ?? '',
    normalText: analyte.normalText ?? '',
  };
}

const blankRow = (): ResultRowState => ({
  key: nextKey(),
  name: '',
  valueKind: 'Numeric',
  value: '',
  unit: '',
  low: '',
  high: '',
  normalText: '',
});

export function ResultEntryForm({
  order,
  zone,
  save,
  onSaved,
}: {
  order: DiagnosticOrder;
  zone: string;
  save: (request: RecordResultsRequest) => Promise<{ values: RecordedResultValue[] }>;
  onSaved?: () => void;
}) {
  const analytes = useLabAnalytes({ activeOnly: true, pageSize: 100 });
  const catalogue = analytes.data?.items ?? [];
  const [rows, setRows] = useState<ResultRowState[]>([]);
  const [reportedOn, setReportedOn] = useState(() => todayInZone(zone));
  const [note, setNote] = useState('');
  const [supersedes, setSupersedes] = useState('');
  const [errors, setErrors] = useState<{ fields: Record<string, string>; root: string | null }>({
    fields: {},
    root: null,
  });
  const [pending, setPending] = useState(false);
  const [rowErrors, setRowErrors] = useState<Record<string, string>>({});
  const seeded = useRef(false);

  // Pre-fill the rows for the analytes requested on the order, once the catalogue has loaded.
  useEffect(() => {
    if (seeded.current || !analytes.data) return;
    seeded.current = true;
    const requested = order.requestedAnalytes
      .map((r) => analytes.data!.items.find((a) => a.id === r.analyteId))
      .filter((a): a is LabAnalyte => !!a);
    setRows(requested.length > 0 ? requested.map(rowFromAnalyte) : [blankRow()]);
  }, [analytes.data, order.requestedAnalytes]);

  const supersededIds = new Set(
    order.resultSets.map((s) => s.supersedesResultSetId).filter(Boolean),
  );
  const replaceable = order.resultSets.filter((s) => !supersededIds.has(s.id));
  const patchRow = (key: string, patch: Partial<ResultRowState>) =>
    setRows((current) => current.map((r) => (r.key === key ? { ...r, ...patch } : r)));
  const unusedAnalytes = catalogue.filter((a) => !rows.some((r) => r.analyteId === a.id));

  async function submit() {
    setErrors({ fields: {}, root: null });
    if (rows.length === 0) {
      setErrors({ fields: {}, root: 'Add at least one result value.' });
      return;
    }
    // A blank value is NEVER sent: Number('') is 0, a real-looking result. Check every row first.
    const problems: Record<string, string> = {};
    for (const row of rows) {
      const index = rows.indexOf(row) + 1;
      if (!row.name.trim()) problems[row.key] = `Enter a name for value ${index}.`;
      else if (row.valueKind === 'Numeric') {
        if (row.value.trim() === '' || row.value === '.')
          problems[row.key] = `Enter the result for ${row.name.trim()}.`;
        else if (!row.unit.trim()) problems[row.key] = `Enter the unit for ${row.name.trim()}.`;
      } else if (!row.value.trim()) problems[row.key] = `Enter the result for ${row.name.trim()}.`;
    }
    setRowErrors(problems);
    if (Object.keys(problems).length > 0) return;
    setPending(true);
    try {
      await save({
        reportedOn,
        ...(note.trim() ? { note: note.trim() } : {}),
        ...(supersedes ? { supersedesResultSetId: supersedes } : {}),
        values: rows.map((row) =>
          row.valueKind === 'Numeric'
            ? {
                ...(row.analyteId ? { analyteId: row.analyteId } : {}),
                name: row.name.trim(),
                valueKind: 'Numeric' as const,
                numericValue: Number(row.value),
                unit: row.unit.trim(),
                ...(row.low ? { referenceLow: Number(row.low) } : {}),
                ...(row.high ? { referenceHigh: Number(row.high) } : {}),
              }
            : {
                ...(row.analyteId ? { analyteId: row.analyteId } : {}),
                name: row.name.trim(),
                valueKind: 'Qualitative' as const,
                textValue: row.value.trim(),
                ...(row.normalText.trim() ? { normalText: row.normalText.trim() } : {}),
              },
        ),
      });
      seeded.current = false;
      setRows([]);
      setNote('');
      setSupersedes('');
      onSaved?.();
    } catch (error) {
      setErrors(mapClinicalFormErrors(error, { ReportedOn: 'reportedOn', Note: 'note' }));
    } finally {
      setPending(false);
    }
  }

  return (
    <form
      noValidate
      aria-label="Enter results"
      className="flex flex-col gap-4"
      onSubmit={(event) => {
        event.preventDefault();
        void submit();
      }}
    >
      {analytes.isLoading && (
        <p role="status" className="text-sm text-ink-muted">
          Loading the analyte list…
        </p>
      )}
      {analytes.isError && (
        <p role="status" className="text-sm text-ink-muted">
          The analyte list couldn’t be loaded. You can still enter values by hand.
        </p>
      )}
      {rows.map((row, index) => (
        <ResultEntryRow
          key={row.key}
          row={row}
          index={index}
          error={rowErrors[row.key]}
          onChange={(patch) => patchRow(row.key, patch)}
          onRemove={() => setRows((current) => current.filter((r) => r.key !== row.key))}
        />
      ))}
      <div className="flex flex-wrap items-end gap-3">
        <div className="flex min-w-48 flex-col gap-1.5">
          <label htmlFor="result-add-analyte" className="text-sm font-semibold text-ink">
            Add a test from the list
          </label>
          <select
            id="result-add-analyte"
            value=""
            onChange={(event) => {
              const analyte = catalogue.find((a) => a.id === event.target.value);
              if (analyte) setRows((current) => [...current, rowFromAnalyte(analyte)]);
            }}
            className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          >
            <option value="">Choose a test…</option>
            {unusedAnalytes.map((a) => (
              <option key={a.id} value={a.id}>
                {a.name}
              </option>
            ))}
          </select>
        </div>
        <Button
          variant="secondary"
          size="sm"
          onClick={() => setRows((current) => [...current, blankRow()])}
        >
          <Plus size={14} aria-hidden="true" /> Add another value
        </Button>
      </div>

      <div className="grid gap-3 min-[460px]:grid-cols-2">
        <div className="flex flex-col gap-1.5">
          <label htmlFor="result-reported-on" className="text-sm font-semibold text-ink">
            Reported on
          </label>
          <input
            id="result-reported-on"
            type="date"
            max={todayInZone(zone)}
            value={reportedOn}
            onChange={(event) => setReportedOn(event.target.value)}
            aria-invalid={errors.fields.reportedOn ? true : undefined}
            className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          />
          {errors.fields.reportedOn && (
            <p role="alert" className="text-xs font-semibold text-danger-ink">
              {errors.fields.reportedOn}
            </p>
          )}
        </div>
        {replaceable.length > 0 && (
          <div className="flex flex-col gap-1.5">
            <label htmlFor="result-supersedes" className="text-sm font-semibold text-ink">
              Replaces an earlier set
            </label>
            <select
              id="result-supersedes"
              value={supersedes}
              onChange={(event) => setSupersedes(event.target.value)}
              className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
            >
              <option value="">Doesn’t replace anything</option>
              {replaceable.map((s) => (
                <option key={s.id} value={s.id}>
                  Set reported {formatDateOnly(s.reportedOn)}
                </option>
              ))}
            </select>
          </div>
        )}
      </div>
      <div className="flex flex-col gap-1.5">
        <label htmlFor="result-note" className="text-sm font-semibold text-ink">
          Note <span className="font-normal text-ink-muted">(optional)</span>
        </label>
        <input
          id="result-note"
          type="text"
          maxLength={MAX_RESULT_NOTE}
          value={note}
          onChange={(event) => setNote(event.target.value)}
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
        />
      </div>
      {errors.root && (
        <p role="alert" className="text-sm font-semibold text-danger-ink">
          {errors.root}
        </p>
      )}
      <div>
        <Button type="submit" disabled={pending} aria-busy={pending}>
          {pending ? 'Saving…' : 'Save results'}
        </Button>
      </div>
    </form>
  );
}
