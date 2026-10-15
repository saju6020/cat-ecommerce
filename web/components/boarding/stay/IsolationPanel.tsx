'use client';

import { useState } from 'react';
import { ShieldAlert } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { CatHeaderCompact } from '@/components/boarding/shared/CatHeaderCompact';
import { FormErrorAlert, SelectRow } from '@/components/boarding/shared/FormBits';
import { ReasonDialog } from '@/components/boarding/shared/ReasonDialog';
import { describeBoardingError, getViolationCode } from '@/lib/api/errors';
import type { StaffStayDetail } from '@/lib/api/types';
import { formatBranchInstant } from '@/lib/format/boarding';
import { todayInZone } from '@/lib/format/time';
import {
  useClearInfectious,
  useFlagInfectious,
  useRecordIsolationEntry,
  useRecordIsolationExit,
  useStaffAvailability,
} from '@/lib/hooks/useStaffBoarding';

/**
 * `components/boarding/stay/IsolationPanel.tsx` — ⭐ HO-BRD-002 §11.8, US-BRD-18, ADR-227. Per cat:
 * **Flag suspected infectious** (a note and the isolation unit to move to — the confirm names the
 * move), **Clear flag** (the server decides who may), and the **isolation entry log** with **Record
 * entry** / **Record exit**. `NoIsolationUnitAvailable` is shown as the blocking problem it is.
 */
function FlagDialog({
  stay,
  catId,
  catName,
  onClose,
}: {
  stay: StaffStayDetail;
  catId: string;
  catName: string;
  onClose: () => void;
}) {
  const flag = useFlagInfectious(stay.id);
  const [target, setTarget] = useState('');
  const inIsolation = stay.allocations.some((a) => a.isActive && a.unitType === 'Isolation');
  const free = useStaffAvailability(
    inIsolation
      ? null
      : {
          arrivalOn: todayInZone(stay.branchTimeZoneId),
          departureOn: stay.departureOn,
          catCount: stay.cats.length,
          requiresIsolation: true,
          excludeStayId: stay.id,
        },
  );
  const units = (free.data?.unitTypes ?? []).flatMap((t) => t.freeUnits ?? []);
  const targetName = units.find((u) => u.unitId === target)?.name;
  const none = !inIsolation && free.data && units.length === 0;
  return (
    <ReasonDialog
      open
      onOpenChange={(o) => !o && onClose()}
      title={`Flag ${catName} as suspected infectious?`}
      consequence={
        inIsolation
          ? `${catName} is already in an isolation unit; the flag is recorded.`
          : `${catName} will be moved to ${targetName ?? 'the isolation unit you choose'}.`
      }
      reasonLabel="What are the signs?"
      requiredMessage="Add a note for the flag."
      confirmLabel="Flag and move"
      variant="destructive"
      submitDisabled={!!none}
      validate={() => (inIsolation || target ? null : 'Choose the isolation unit to move to.')}
      onConfirm={(note) =>
        flag.mutateAsync({ catId, note, targetIsolationUnitId: inIsolation ? undefined : target })
      }
    >
      {none && (
        <p
          role="alert"
          className="rounded-[var(--radius-sm)] bg-danger-bg px-3 py-2 text-sm font-semibold text-danger-ink"
        >
          No isolation unit is free, so {catName} can’t be moved. Free one up first — the stay is
          listed under Needs attention on the dashboard.
        </p>
      )}
      {!inIsolation && !none && (
        <SelectRow
          id="iso-unit"
          label="Isolation unit"
          value={target}
          onChange={(e) => setTarget(e.target.value)}
        >
          <option value="">Choose a unit…</option>
          {units.map((u) => (
            <option key={u.unitId} value={u.unitId}>
              {u.name}
            </option>
          ))}
        </SelectRow>
      )}
    </ReasonDialog>
  );
}

export function IsolationPanel({ stay }: { stay: StaffStayDetail }) {
  const [flagging, setFlagging] = useState<{ catId: string; catName: string } | null>(null);
  const [clearing, setClearing] = useState<{ catId: string; catName: string } | null>(null);
  const [purposeDraft, setPurposeDraft] = useState('');
  const [error, setError] = useState<string | null>(null);
  const clear = useClearInfectious(stay.id);
  const entry = useRecordIsolationEntry(stay.id);
  const exit = useRecordIsolationExit(stay.id);
  const inIsolationUnit = stay.allocations.some((a) => a.isActive && a.unitType === 'Isolation');
  const zone = stay.branchTimeZoneId;

  async function guarded(fn: () => Promise<unknown>) {
    setError(null);
    try {
      await fn();
    } catch (e) {
      setError(
        getViolationCode(e) === 'NotInIsolation'
          ? 'This cat isn’t in an isolation unit.'
          : describeBoardingError(e),
      );
    }
  }

  return (
    <section aria-labelledby="isolation-heading" className="flex flex-col gap-4">
      <h2 id="isolation-heading" className="font-display text-lg font-semibold text-ink">
        Isolation
      </h2>
      <FormErrorAlert message={error} />
      <ul className="flex flex-col gap-3">
        {stay.cats.map((cat) => (
          <li
            key={cat.catId}
            className="flex flex-wrap items-center justify-between gap-3 rounded-[var(--radius-sm)] border border-border bg-surface p-3"
          >
            <div>
              <CatHeaderCompact
                name={cat.catName}
                isHighRisk={cat.catIsHighRisk}
                handlingNote={cat.handlingNote}
              />
              {cat.suspectedInfectious && (
                <p className="mt-1 flex items-center gap-1.5 text-sm font-semibold text-warning-ink">
                  <ShieldAlert size={14} aria-hidden="true" /> Suspected infectious
                </p>
              )}
            </div>
            {cat.suspectedInfectious ? (
              <Button
                variant="secondary"
                size="sm"
                onClick={() => setClearing({ catId: cat.catId, catName: cat.catName })}
              >
                Clear flag
              </Button>
            ) : (
              <Button
                variant="secondary"
                size="sm"
                onClick={() => setFlagging({ catId: cat.catId, catName: cat.catName })}
              >
                Flag suspected infectious
              </Button>
            )}
          </li>
        ))}
      </ul>

      <div className="flex flex-col gap-2">
        <h3 className="font-display text-base font-semibold text-ink">Isolation entry log</h3>
        {stay.isolationEntries.length === 0 ? (
          <p className="text-sm text-ink-muted">No entries yet.</p>
        ) : (
          <ul aria-label="Isolation entries" className="flex flex-col gap-1 text-sm">
            {stay.isolationEntries.map((e) => (
              <li key={e.id} className="flex flex-wrap items-center gap-2">
                <span>
                  {e.staffName} · in {formatBranchInstant(e.enteredOn, zone)}
                  {e.leftOn
                    ? ` · out ${formatBranchInstant(e.leftOn, zone)}`
                    : ' · still inside'} — {e.purpose}
                </span>
                {!e.leftOn && (
                  <Button
                    size="sm"
                    variant="secondary"
                    onClick={() => void guarded(() => exit.mutateAsync(e.id))}
                  >
                    Record exit
                  </Button>
                )}
              </li>
            ))}
          </ul>
        )}
        <div className="flex flex-wrap items-end gap-2">
          <label className="flex flex-col gap-1 text-sm font-semibold text-ink">
            Purpose of the visit
            <input
              value={purposeDraft}
              onChange={(e) => setPurposeDraft(e.target.value)}
              className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm font-normal"
            />
          </label>
          <Button
            size="sm"
            disabled={!inIsolationUnit}
            onClick={() =>
              void guarded(async () => {
                await entry.mutateAsync(purposeDraft || 'Care visit');
                setPurposeDraft('');
              })
            }
          >
            Record entry
          </Button>
          {!inIsolationUnit && (
            <p className="text-xs text-ink-muted">
              Entries are only recorded for a cat in an isolation unit.
            </p>
          )}
        </div>
      </div>

      {flagging && (
        <FlagDialog
          stay={stay}
          catId={flagging.catId}
          catName={flagging.catName}
          onClose={() => setFlagging(null)}
        />
      )}
      {clearing && (
        <ReasonDialog
          open
          onOpenChange={(o) => !o && setClearing(null)}
          title={`Clear the flag on ${clearing.catName}?`}
          consequence={`${clearing.catName} is no longer treated as suspected infectious. The cat stays where it is until you move it.`}
          reasonLabel="Why is it being cleared?"
          requiredMessage="Add a note for clearing the flag."
          confirmLabel="Clear flag"
          onConfirm={(note) => clear.mutateAsync({ catId: clearing.catId, note })}
        />
      )}
    </section>
  );
}
