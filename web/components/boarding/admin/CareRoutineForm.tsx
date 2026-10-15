'use client';

import { useState } from 'react';
import { Plus, Trash2 } from 'lucide-react';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { Button } from '@/components/ui/button';
import { FormErrorAlert } from '@/components/boarding/shared/FormBits';
import { QueryGate } from '@/components/boarding/shared/QueryGate';
import { TASK_KIND_LABEL } from '@/components/boarding/shared/labels';
import { describeBoardingError } from '@/lib/api/errors';
import type { CareRoutine, RoutineTaskKind } from '@/lib/api/types';
import { useCareRoutine, useSetCareRoutine } from '@/lib/hooks/useStaffBoarding';

const KINDS: RoutineTaskKind[] = ['LitterChange', 'Enrichment', 'ObservationCheck'];
const TIME = /^([01]\d|2[0-3]):[0-5]\d$/;

function Form({ routine }: { routine: CareRoutine }) {
  const save = useSetCareRoutine();
  const [times, setTimes] = useState(routine.times);
  const [dirty, setDirty] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit() {
    const bad = times.find((t) => !TIME.test(t.localTime));
    if (bad) {
      setError('Write each time as 09:30.');
      return;
    }
    setError(null);
    try {
      await save.mutateAsync(times);
      setDirty(false);
      setSaved(true);
    } catch (e) {
      setError(describeBoardingError(e));
    }
  }

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        void submit();
      }}
      className="flex max-w-xl flex-col gap-5"
      aria-label="Care routine"
    >
      <UnsavedChangesGuard when={dirty} />
      {routine.isDefault && (
        <p className="text-sm text-ink-muted">
          These are the defaults — saving makes them this branch’s own.
        </p>
      )}
      {KINDS.map((kind) => (
        <fieldset key={kind} className="flex flex-col gap-2">
          <legend className="text-sm font-semibold text-ink">{TASK_KIND_LABEL[kind]} times</legend>
          {times
            .filter((t) => t.kind === kind)
            .map((t) => {
              const index = times.indexOf(t);
              return (
                <div key={`${kind}-${index}`} className="flex items-center gap-2">
                  <input
                    type="time"
                    aria-label={`${TASK_KIND_LABEL[kind]} time ${index + 1}`}
                    value={t.localTime}
                    onChange={(e) => {
                      setDirty(true);
                      setSaved(false);
                      setTimes(
                        times.map((x, i) =>
                          i === index ? { ...x, localTime: e.target.value } : x,
                        ),
                      );
                    }}
                    className="min-h-10 rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 text-sm"
                  />
                  <Button
                    variant="ghost"
                    size="sm"
                    aria-label={`Remove ${TASK_KIND_LABEL[kind]} time ${t.localTime}`}
                    onClick={() => {
                      setDirty(true);
                      setTimes(times.filter((_, i) => i !== index));
                    }}
                  >
                    <Trash2 size={14} aria-hidden="true" />
                  </Button>
                </div>
              );
            })}
          <div>
            <Button
              variant="secondary"
              size="sm"
              disabled={times.filter((t) => t.kind === kind).length >= 8}
              onClick={() => {
                setDirty(true);
                setTimes([...times, { kind, localTime: '09:00' }]);
              }}
            >
              <Plus size={14} aria-hidden="true" /> Add a time
            </Button>
          </div>
        </fieldset>
      ))}
      <FormErrorAlert message={error} />
      {saved && (
        <p role="status" className="text-sm font-semibold text-accent-ink">
          Care routine saved.
        </p>
      )}
      <div>
        <Button type="submit" disabled={save.isPending}>
          {save.isPending ? 'Saving…' : 'Save routine'}
        </Button>
      </div>
    </form>
  );
}

/** `CareRoutineForm` — HO-BRD-002 §11.11, US-BRD-09: the litter, enrichment and observation times per
 *  branch that the server turns into daily tasks (up to 8 of each). */
export function CareRoutineForm() {
  const query = useCareRoutine();
  return (
    <QueryGate query={query} loadingLabel="Loading the routine…">
      {(routine) => <Form routine={routine} />}
    </QueryGate>
  );
}
