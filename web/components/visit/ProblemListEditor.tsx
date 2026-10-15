'use client';

import { useEffect, useId, useState } from 'react';
import { Plus, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { useDebouncedValue } from '@/lib/hooks/useDebouncedValue';
import { useDiagnosisCode, useDiagnosisCodes } from '@/lib/hooks/useClinicalCatalogue';
import { cn } from '@/lib/utils';
import { MAX_PROBLEM_NOTE, MAX_PROBLEMS, type ProblemRow } from '@/lib/validation/clinical';
import type { ProblemCertainty } from '@/lib/api/visits';

/**
 * ⭐ `ProblemListEditor` — HO-EMR-002 §11.1 (US-CLN-12). A diagnosis-code COMBOBOX
 * (`GET /api/staff/diagnosis-codes?search&activeOnly=true`, debounced, keyboard-navigable listbox:
 * ↑/↓/Home/End move, Enter picks, Esc closes), a certainty radio per problem (*Provisional* /
 * *Confirmed*) and an optional note; up to 20. A duplicate is disabled in place with its reason and
 * `DuplicateProblem` is also mapped from the server. A retired code is never offered (`activeOnly`).
 */
export function ProblemListEditor({
  problems,
  onChange,
  disabled,
}: {
  problems: ProblemRow[];
  /** Receives an UPDATER over the latest rows, so two edits landing together (two restored-draft
   *  lookups resolving at once) compose instead of overwriting each other from a stale closure. */
  onChange: (update: (current: ProblemRow[]) => ProblemRow[]) => void;
  disabled?: boolean;
}) {
  const atLimit = problems.length >= MAX_PROBLEMS;

  return (
    <section
      aria-labelledby="problems-heading"
      className="flex flex-col gap-3 rounded-[var(--radius-md)] border border-border bg-surface p-4"
    >
      <h2
        id="problems-heading"
        className="text-sm font-bold uppercase tracking-wide text-ink-muted"
      >
        Problems{' '}
        <span className="font-semibold normal-case text-ink-muted">
          · {problems.length} of {MAX_PROBLEMS}
        </span>
      </h2>

      {problems.length === 0 ? (
        <p className="text-sm text-ink-muted">
          No problems yet. Add at least one diagnosis before finalising.
        </p>
      ) : (
        <ul className="m-0 flex list-none flex-col divide-y divide-border p-0">
          {problems.map((row) => (
            <ProblemItem
              key={row.key}
              row={row}
              disabled={disabled}
              onChange={(patch) =>
                onChange((current) =>
                  current.map((p) => (p.key === row.key ? { ...p, ...patch } : p)),
                )
              }
              onRemove={() => onChange((current) => current.filter((p) => p.key !== row.key))}
            />
          ))}
        </ul>
      )}

      {atLimit ? (
        <p role="status" className="text-sm font-semibold text-warning-ink">
          A visit holds at most {MAX_PROBLEMS} problems. Remove one to add another.
        </p>
      ) : (
        <ProblemCombobox
          disabled={disabled}
          existingCodeIds={problems.map((p) => p.diagnosisCodeId)}
          onPick={(code) =>
            onChange((current) => [
              ...current,
              {
                key: `new-${code.id}-${current.length}`,
                diagnosisCodeId: code.id,
                code: code.code,
                name: code.name,
                certainty: 'Provisional',
                note: '',
              },
            ])
          }
        />
      )}
    </section>
  );
}

function ProblemItem({
  row,
  disabled,
  onChange,
  onRemove,
}: {
  row: ProblemRow;
  disabled?: boolean;
  onChange: (patch: Partial<ProblemRow>) => void;
  onRemove: () => void;
}) {
  // A restored draft carries code ids only — look the name up so the row is readable.
  const lookup = useDiagnosisCode(row.name ? '' : row.diagnosisCodeId);
  const resolved = lookup.data;
  useEffect(() => {
    if (!row.name && resolved) onChange({ code: resolved.code, name: resolved.name });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [resolved, row.name]);

  const group = useId();
  const label = row.name ? `${row.code} — ${row.name}` : 'Loading diagnosis…';
  return (
    <li className="flex flex-col gap-2 py-3 first:pt-0">
      <div className="flex items-start justify-between gap-2">
        <p className="text-sm font-semibold text-ink">{label}</p>
        <Button
          variant="ghost"
          size="sm"
          disabled={disabled}
          aria-label={`Remove ${row.name || 'this problem'}`}
          onClick={onRemove}
        >
          <X size={14} aria-hidden="true" /> Remove
        </Button>
      </div>
      <fieldset
        className="m-0 flex flex-wrap items-center gap-x-4 gap-y-1 border-0 p-0"
        disabled={disabled}
      >
        <legend className="sr-only">Certainty of {row.name || 'this problem'}</legend>
        {(['Provisional', 'Confirmed'] as ProblemCertainty[]).map((value) => (
          <label key={value} className="inline-flex items-center gap-1.5 text-sm text-ink">
            <input
              type="radio"
              name={`${group}-certainty`}
              checked={row.certainty === value}
              onChange={() => onChange({ certainty: value })}
            />
            {value}
          </label>
        ))}
      </fieldset>
      <div className="flex flex-col gap-1">
        <label htmlFor={`${group}-note`} className="text-xs font-semibold text-ink-muted">
          Note <span className="font-normal">(optional)</span>
        </label>
        <input
          id={`${group}-note`}
          type="text"
          maxLength={MAX_PROBLEM_NOTE}
          disabled={disabled}
          value={row.note}
          onChange={(event) => onChange({ note: event.target.value })}
          className="w-full rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-1.5 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
        />
      </div>
    </li>
  );
}

function ProblemCombobox({
  existingCodeIds,
  onPick,
  disabled,
}: {
  existingCodeIds: string[];
  onPick: (code: { id: string; code: string; name: string }) => void;
  disabled?: boolean;
}) {
  const listId = useId();
  const [text, setText] = useState('');
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(0);
  const search = useDebouncedValue(text.trim(), 250);
  const results = useDiagnosisCodes({ search, activeOnly: true, pageSize: 10 }, open);
  // The list belongs to the DEBOUNCED search: while the typed text is ahead of it (or a request is in
  // flight) it is not offered, so Enter can never pick from the previous search's results.
  const searching = text.trim() !== search || results.isFetching;
  const items = searching ? [] : (results.data?.items ?? []);
  const optionId = (index: number) => `${listId}-opt-${index}`;
  const isTaken = (id: string) => existingCodeIds.includes(id);

  function pick(index: number) {
    const item = items[index];
    if (!item || isTaken(item.id)) return;
    onPick({ id: item.id, code: item.code, name: item.name });
    setText('');
    setOpen(false);
  }

  function onKeyDown(event: React.KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      setOpen(true);
      setActive((i) => Math.min(items.length - 1, i + 1));
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      setActive((i) => Math.max(0, i - 1));
    } else if (event.key === 'Home' && open) {
      event.preventDefault();
      setActive(0);
    } else if (event.key === 'End' && open) {
      event.preventDefault();
      setActive(Math.max(0, items.length - 1));
    } else if (event.key === 'Enter' && open) {
      event.preventDefault();
      pick(active);
    } else if (event.key === 'Escape' && open) {
      event.stopPropagation();
      setOpen(false);
    }
  }

  return (
    <div className="relative flex flex-col gap-1.5">
      <label
        htmlFor="problem-search"
        className="flex items-center gap-1 text-sm font-semibold text-ink"
      >
        <Plus size={14} aria-hidden="true" /> Add a diagnosis
      </label>
      <input
        id="problem-search"
        type="text"
        role="combobox"
        aria-expanded={open}
        aria-controls={listId}
        aria-autocomplete="list"
        aria-activedescendant={open && items.length > 0 ? optionId(active) : undefined}
        autoComplete="off"
        disabled={disabled}
        placeholder="Search by code or name"
        value={text}
        onChange={(event) => {
          setText(event.target.value);
          setOpen(true);
          setActive(0);
        }}
        onFocus={() => text && setOpen(true)}
        onBlur={() => setOpen(false)}
        onKeyDown={onKeyDown}
        className="w-full rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
      />
      <ul
        id={listId}
        role="listbox"
        aria-label="Matching diagnoses"
        hidden={!open}
        className="absolute left-0 right-0 top-full z-20 m-0 mt-1 max-h-64 list-none overflow-y-auto rounded-[var(--radius-sm)] border border-border bg-surface p-0 shadow-modal"
      >
        {searching && (
          <li role="presentation" className="px-3 py-2 text-sm text-ink-muted">
            Searching…
          </li>
        )}
        {results.isError && (
          <li role="presentation" className="px-3 py-2 text-sm text-danger-ink">
            The diagnosis list couldn’t be loaded. Try again.
          </li>
        )}
        {!searching && !results.isError && items.length === 0 && (
          <li role="presentation" className="px-3 py-2 text-sm text-ink-muted">
            No diagnosis matches “{search}”.
          </li>
        )}
        {items.map((item, index) => {
          const taken = isTaken(item.id);
          return (
            <li
              key={item.id}
              id={optionId(index)}
              role="option"
              aria-selected={index === active}
              aria-disabled={taken || undefined}
              // mousedown (not click) so the input's blur does not close the list first.
              onMouseDown={(event) => {
                event.preventDefault();
                pick(index);
              }}
              className={cn(
                'cursor-pointer px-3 py-2 text-sm',
                index === active && 'bg-accent-soft',
                taken ? 'cursor-not-allowed text-ink-muted' : 'text-ink',
              )}
            >
              <span className="font-semibold">{item.code}</span> — {item.name}
              {taken && <span className="ml-2 text-xs">(already on the list)</span>}
            </li>
          );
        })}
      </ul>
    </div>
  );
}
