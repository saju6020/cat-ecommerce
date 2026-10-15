'use client';

import { Plus, Trash2 } from 'lucide-react';
import type { FieldErrors, UseFormRegister } from 'react-hook-form';
import { Button } from '@/components/ui/button';
import { SelectField, TextAreaField, TextField } from '@/components/inventory/fields';
import { SEVERITY_ORDER } from '@/lib/format/pharmacy';
import { FormularyPicker } from './FormularyPicker';

/**
 * `InteractionRows` — HO-PHM-002 §11.7. A repeatable row group: each row names ANOTHER medicine (picked
 * from the formulary) **or** a free term, a severity and a note. The server reads these rows when it
 * checks a new prescription against the cat's active ones; this only collects them. Severities are
 * written as words and the picked medicine is shown by name.
 */
export interface InteractionRowValue {
  kind: 'medicine' | 'term';
  medicineId: string;
  medicineName: string;
  term: string;
  severity: string;
  note: string;
}

type RowErrors = Array<
  Partial<Record<'medicineId' | 'term' | 'severity' | 'note', { message?: string }>> | undefined
>;

export function InteractionRows({
  rows,
  keys,
  selfId,
  errors,
  register,
  onAdd,
  onRemove,
  onPatch,
}: {
  rows: InteractionRowValue[];
  keys: string[];
  selfId?: string;
  errors?: FieldErrors | RowErrors;
  register: UseFormRegister<{ interactions: InteractionRowValue[] }>;
  onAdd: () => void;
  onRemove: (index: number) => void;
  onPatch: (index: number, patch: Partial<InteractionRowValue>) => void;
}) {
  const rowErrors = (errors ?? []) as RowErrors;
  return (
    <div className="flex flex-col gap-3">
      {rows.length === 0 && (
        <p className="text-sm text-ink-muted">
          No interactions listed. Add one if this medicine should warn alongside another.
        </p>
      )}
      {rows.map((row, index) => {
        const error = rowErrors[index];
        return (
          <fieldset
            key={keys[index] ?? index}
            className="flex flex-col gap-3 rounded-[var(--radius-md)] border border-border p-3"
          >
            <legend className="px-1 text-sm font-semibold text-ink">Interaction {index + 1}</legend>
            <div className="grid gap-3 sm:grid-cols-2">
              <SelectField
                id={`fm-int-${index}-kind`}
                label="Interacts with"
                value={row.kind}
                onChange={(event) =>
                  onPatch(index, {
                    kind: event.target.value as 'medicine' | 'term',
                    medicineId: '',
                    medicineName: '',
                    term: '',
                  })
                }
              >
                <option value="medicine">Another medicine</option>
                <option value="term">A term (for example a drug class)</option>
              </SelectField>
              {row.kind === 'medicine' ? (
                <div className="flex flex-col gap-1">
                  <FormularyPicker
                    id={`fm-int-${index}-medicine`}
                    label="Other medicine"
                    exclude={selfId}
                    clearOnSelect={false}
                    error={error?.medicineId?.message}
                    onSelect={(picked) =>
                      onPatch(index, { medicineId: picked.id, medicineName: picked.name })
                    }
                  />
                  <p className="text-xs text-ink-muted">
                    {row.medicineName ? `Chosen: ${row.medicineName}` : 'Nothing chosen yet.'}
                  </p>
                </div>
              ) : (
                <TextField
                  id={`fm-int-${index}-term`}
                  label="Term"
                  error={error?.term?.message}
                  {...register(`interactions.${index}.term`)}
                />
              )}
              <SelectField
                id={`fm-int-${index}-severity`}
                label="Severity"
                required
                error={error?.severity?.message}
                {...register(`interactions.${index}.severity`)}
              >
                <option value="">Choose severity</option>
                {[...SEVERITY_ORDER].reverse().map((severity) => (
                  <option key={severity} value={severity}>
                    {severity}
                  </option>
                ))}
              </SelectField>
            </div>
            <TextAreaField
              id={`fm-int-${index}-note`}
              label="Note"
              required
              rows={2}
              hint="Shown to the vet with the warning."
              error={error?.note?.message}
              {...register(`interactions.${index}.note`)}
            />
            <div>
              <Button variant="ghost" size="sm" onClick={() => onRemove(index)}>
                <Trash2 size={14} aria-hidden="true" />
                Remove interaction {index + 1}
              </Button>
            </div>
          </fieldset>
        );
      })}
      <div>
        <Button variant="secondary" onClick={onAdd}>
          <Plus size={14} aria-hidden="true" />
          Add interaction
        </Button>
      </div>
    </div>
  );
}
