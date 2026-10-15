'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  useDialogReturnFocus,
} from '@/components/ui/dialog';
import { FormFieldRow } from '@/components/cat/FormFieldRow';
import { FormErrorAlert, SelectRow } from '@/components/boarding/shared/FormBits';
import { ReasonDialog } from '@/components/boarding/shared/ReasonDialog';
import { ApiValidationError } from '@/lib/api/client';
import {
  describeBoardingError,
  getRelocationOptions,
  getViolationCode,
  type RelocationOption,
} from '@/lib/api/errors';
import type { StaffStayDetail } from '@/lib/api/types';
import { formatDateOnly, todayInZone } from '@/lib/format/time';
import { useStaffAvailability, useExtendStay, useRelocateStay } from '@/lib/hooks/useStaffBoarding';

/**
 * `ExtendDialog` — HO-BRD-002 §11.8, US-BRD-15 AC1/AC2. A new departure date → `POST …/extend` with the
 * stay's `version` (a stale one is a `409`). When the unit is not free the server's
 * `relocationOptions[]` are listed with **Relocate and extend**; the reason is mandatory and the
 * confirm names both units. The dialog owns its values — a poll never clears them (ADR-FE-101).
 */
export function ExtendDialog({
  open,
  onOpenChange,
  stay,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  stay: StaffStayDetail;
}) {
  const extend = useExtendStay(stay.id);
  const [date, setDate] = useState(stay.departureOn);
  const [error, setError] = useState<string | null>(null);
  const [options, setOptions] = useState<RelocationOption[]>([]);
  const [relocateTo, setRelocateTo] = useState<RelocationOption | null>(null);
  const returnFocusRef = useDialogReturnFocus(open);

  async function submit() {
    setError(null);
    setOptions([]);
    if (date <= stay.departureOn) {
      setError('Choose a date after the current departure.');
      return;
    }
    try {
      await extend.mutateAsync({ newDepartureOn: date, expectedVersion: stay.version });
      onOpenChange(false);
    } catch (e) {
      if (e instanceof ApiValidationError && getViolationCode(e) === 'UnitNotFreeForExtension') {
        setOptions(getRelocationOptions(e));
      }
      setError(describeBoardingError(e));
    }
  }

  return (
    <>
      <Dialog open={open && !relocateTo} onOpenChange={onOpenChange}>
        <DialogContent hideClose returnFocusRef={returnFocusRef}>
          <DialogHeader>
            <DialogTitle>Extend the stay</DialogTitle>
            <DialogDescription>
              Currently departing {formatDateOnly(stay.departureOn)}. The price is recalculated by
              the server.
            </DialogDescription>
          </DialogHeader>
          <FormFieldRow
            id="extend-date"
            label="New departure date"
            type="date"
            min={stay.departureOn}
            value={date}
            onChange={(e) => setDate(e.target.value)}
          />
          <FormErrorAlert message={error} />
          {options.length > 0 && (
            <ul aria-label="Units that are free" className="flex flex-col gap-2">
              {options.map((o) => (
                <li
                  key={o.unitId}
                  className="flex items-center justify-between gap-2 rounded-[var(--radius-sm)] border border-border px-3 py-2 text-sm"
                >
                  <span>
                    {o.name} · {o.unitType}
                    {o.block ? ` · ${o.block}` : ''}
                  </span>
                  <Button size="sm" variant="secondary" onClick={() => setRelocateTo(o)}>
                    Relocate and extend
                  </Button>
                </li>
              ))}
            </ul>
          )}
          <DialogFooter>
            <Button variant="secondary" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button onClick={() => void submit()} disabled={extend.isPending}>
              {extend.isPending ? 'Extending…' : 'Extend'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
      {relocateTo && (
        <RelocateDialog
          open
          onOpenChange={(o) => {
            if (!o) setRelocateTo(null);
          }}
          stay={stay}
          targetUnit={relocateTo}
          newDepartureOn={date}
          onDone={() => {
            setRelocateTo(null);
            onOpenChange(false);
          }}
        />
      )}
    </>
  );
}

/**
 * `RelocateDialog` — HO-BRD-002 §11.8, US-BRD-15: a target unit (from the server — a given option, or
 * the free units of this stay's availability), the effective date, a **mandatory reason**, and a
 * confirm sentence that names **both units**.
 */
export function RelocateDialog({
  open,
  onOpenChange,
  stay,
  targetUnit,
  newDepartureOn,
  onDone,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  stay: StaffStayDetail;
  targetUnit?: RelocationOption;
  newDepartureOn?: string;
  onDone?: () => void;
}) {
  const relocate = useRelocateStay(stay.id);
  const today = todayInZone(stay.branchTimeZoneId);
  const [effective, setEffective] = useState(stay.status === 'CheckedIn' ? today : stay.arrivalOn);
  const [chosen, setChosen] = useState(targetUnit?.unitId ?? '');
  const free = useStaffAvailability(
    targetUnit
      ? null
      : {
          arrivalOn: effective,
          departureOn: stay.departureOn,
          catCount: stay.cats.length,
          excludeStayId: stay.id,
          requiresIsolation: stay.requiresIsolation,
        },
  );
  const choices = targetUnit
    ? [{ unitId: targetUnit.unitId, name: targetUnit.name }]
    : (free.data?.unitTypes ?? []).flatMap((t) =>
        (t.freeUnits ?? []).map((u) => ({ unitId: u.unitId, name: u.name })),
      );
  const targetName =
    choices.find((c) => c.unitId === chosen)?.name ?? targetUnit?.name ?? 'the chosen unit';

  return (
    <ReasonDialog
      open={open}
      onOpenChange={onOpenChange}
      title="Relocate the stay"
      consequence={`Move ${stay.cats.map((c) => c.catName).join(' and ')} from ${stay.unitName ?? 'the current unit'} to ${targetName} from ${formatDateOnly(effective)}${newDepartureOn && newDepartureOn > stay.departureOn ? `, and extend to ${formatDateOnly(newDepartureOn)}` : ''}. Care tasks continue in the new unit.`}
      reasonLabel="Reason for the move"
      requiredMessage="Enter the reason for moving the cat."
      confirmLabel="Relocate"
      pendingLabel="Relocating…"
      submitDisabled={!chosen && !targetUnit}
      validate={() => (chosen || targetUnit ? null : 'Choose the unit to move to.')}
      onConfirm={async (reason) => {
        await relocate.mutateAsync({
          targetUnitId: targetUnit?.unitId ?? chosen,
          effectiveFromLocalDate: effective,
          reason,
          newDepartureOn:
            newDepartureOn && newDepartureOn > stay.departureOn ? newDepartureOn : undefined,
          expectedVersion: stay.version,
        });
        onDone?.();
      }}
    >
      <FormFieldRow
        id="relocate-effective"
        label="From (date)"
        type="date"
        value={effective}
        onChange={(e) => setEffective(e.target.value)}
      />
      {!targetUnit && (
        <SelectRow
          id="relocate-unit"
          label="Move to"
          value={chosen}
          onChange={(e) => setChosen(e.target.value)}
        >
          <option value="">Choose a free unit…</option>
          {choices.map((c) => (
            <option key={c.unitId} value={c.unitId}>
              {c.name}
            </option>
          ))}
        </SelectRow>
      )}
    </ReasonDialog>
  );
}
