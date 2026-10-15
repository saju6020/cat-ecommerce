'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { FormFieldRow } from '@/components/cat/FormFieldRow';
import { FormErrorAlert, SelectRow } from '@/components/boarding/shared/FormBits';
import { QueryGate } from '@/components/boarding/shared/QueryGate';
import { UNIT_TYPE_LABEL } from '@/components/boarding/shared/labels';
import { ApiValidationError } from '@/lib/api/client';
import {
  applyValidationErrors,
  BOARDING_VIOLATION_MESSAGES,
  describeBoardingError,
} from '@/lib/api/errors';
import type { BoardingUnit } from '@/lib/api/types';
import { formatDateOnly } from '@/lib/format/time';
import { formatMoney } from '@/lib/format/money';
import {
  useBoardingUnits,
  useCancelMaintenanceBlock,
  useCreateBoardingUnit,
  useCreateMaintenanceBlock,
  useMaintenanceBlocks,
  useSetUnitActivation,
  useUpdateBoardingUnit,
} from '@/lib/hooks/useStaffBoarding';
import { STAFF_ADMIN_BOARDING_RATE_CARDS } from '@/lib/routing/boarding-links';
import { maintenanceSchema, unitSchema, type UnitFormValues } from '@/lib/validation/boarding';

const FIELDS = {
  Code: 'code',
  Name: 'name',
  CapacityCats: 'capacityCats',
  Block: 'block',
  SortOrder: 'sortOrder',
  UnitType: 'unitType',
} as const;

/** `UnitForm` — HO-BRD-002 §11.11, US-BRD-01: code, name, type, capacity, block, sort order. The type's
 *  current nightly rate is shown read-only with a link to the rate card (ADR-221). */
function UnitForm({ unit, onClose }: { unit?: BoardingUnit; onClose: () => void }) {
  const create = useCreateBoardingUnit();
  const update = useUpdateBoardingUnit(unit?.id ?? '');
  const form = useForm<UnitFormValues>({
    resolver: zodResolver(unitSchema),
    defaultValues: {
      code: unit?.code ?? '',
      name: unit?.name ?? '',
      unitType: unit?.unitType ?? 'Standard',
      capacityCats: unit?.capacityCats ?? 1,
      block: unit?.block ?? '',
      sortOrder: unit?.sortOrder ?? 0,
    },
  });
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = form;

  async function submit(values: UnitFormValues) {
    const parsed = unitSchema.parse(values);
    const input = { ...parsed, block: parsed.block.trim() || null };
    try {
      if (unit) await update.mutateAsync(input);
      else await create.mutateAsync(input);
      onClose();
    } catch (e) {
      if (e instanceof ApiValidationError) {
        applyValidationErrors(e, setError, {
          fieldMap: FIELDS,
          knownFields: Object.values(FIELDS),
          codeMessages: BOARDING_VIOLATION_MESSAGES,
        });
      } else {
        setError('root', { message: describeBoardingError(e) });
      }
    }
  }

  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent hideClose>
        <DialogHeader>
          <DialogTitle>{unit ? `Edit ${unit.name}` : 'Add a unit'}</DialogTitle>
          <DialogDescription>
            {unit?.nightlyRate ? (
              <>
                Nightly rate {formatMoney(unit.nightlyRate)}, from the{' '}
                {UNIT_TYPE_LABEL[unit.unitType]} rate card —{' '}
                <Link href={STAFF_ADMIN_BOARDING_RATE_CARDS} className="font-semibold underline">
                  change it on the rate card
                </Link>
                .
              </>
            ) : (
              'The nightly rate comes from the unit type’s rate card.'
            )}
          </DialogDescription>
        </DialogHeader>
        <form
          onSubmit={handleSubmit(submit)}
          noValidate
          className="grid gap-3 sm:grid-cols-2"
          aria-label="Unit"
        >
          <FormErrorAlert message={errors.root?.message} />
          <FormFieldRow
            id="unit-code"
            label="Code"
            required
            error={errors.code?.message}
            {...register('code')}
          />
          <FormFieldRow
            id="unit-name"
            label="Name"
            required
            error={errors.name?.message}
            {...register('name')}
          />
          <SelectRow
            id="unit-type"
            label="Type"
            error={errors.unitType?.message}
            {...register('unitType')}
          >
            {Object.entries(UNIT_TYPE_LABEL).map(([k, v]) => (
              <option key={k} value={k}>
                {v}
              </option>
            ))}
          </SelectRow>
          <FormFieldRow
            id="unit-cap"
            label="Capacity (cats)"
            type="number"
            error={errors.capacityCats?.message}
            {...register('capacityCats')}
          />
          <FormFieldRow
            id="unit-block"
            label="Block"
            error={errors.block?.message}
            {...register('block')}
          />
          <FormFieldRow
            id="unit-sort"
            label="Sort order"
            type="number"
            error={errors.sortOrder?.message}
            {...register('sortOrder')}
          />
          <DialogFooter className="sm:col-span-2">
            <Button variant="secondary" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? 'Saving…' : 'Save unit'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

/** `MaintenanceBlockDialog` — dates and reason; `MaintenanceOverlapsStays` is shown with the affected
 *  stays' dates exactly as the server wrote them (AC2). */
function MaintenanceDialog({ unit, onClose }: { unit: BoardingUnit; onClose: () => void }) {
  const blocks = useMaintenanceBlocks(unit.id);
  const create = useCreateMaintenanceBlock(unit.id);
  const cancel = useCancelMaintenanceBlock(unit.id);
  const [values, setValues] = useState({ startsOn: '', endsOn: '', reason: '' });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [serverError, setServerError] = useState<string | null>(null);

  async function add() {
    const parsed = maintenanceSchema.safeParse(values);
    if (!parsed.success) {
      const next: Record<string, string> = {};
      for (const i of parsed.error.issues) next[String(i.path[0])] ??= i.message;
      setErrors(next);
      return;
    }
    setErrors({});
    setServerError(null);
    try {
      await create.mutateAsync(parsed.data);
      setValues({ startsOn: '', endsOn: '', reason: '' });
    } catch (e) {
      setServerError(describeBoardingError(e));
    }
  }

  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent hideClose>
        <DialogHeader>
          <DialogTitle>Maintenance — {unit.name}</DialogTitle>
          <DialogDescription>
            A unit on maintenance is not offered for those days.
          </DialogDescription>
        </DialogHeader>
        <QueryGate query={blocks} loadingLabel="Loading…">
          {(list) =>
            list.length === 0 ? (
              <p className="text-sm text-ink-muted">No maintenance planned.</p>
            ) : (
              <ul className="flex flex-col gap-1 text-sm">
                {list.map((b) => (
                  <li key={b.id} className="flex items-center justify-between gap-2">
                    <span>
                      {formatDateOnly(b.startsOn)} – {formatDateOnly(b.endsOn)} · {b.reason}
                    </span>
                    <Button
                      size="sm"
                      variant="secondary"
                      onClick={() => void cancel.mutateAsync(b.id)}
                    >
                      Cancel block
                    </Button>
                  </li>
                ))}
              </ul>
            )
          }
        </QueryGate>
        <div className="grid gap-3 sm:grid-cols-2">
          <FormFieldRow
            id="mb-from"
            label="First day"
            type="date"
            value={values.startsOn}
            error={errors.startsOn}
            onChange={(e) => setValues({ ...values, startsOn: e.target.value })}
          />
          <FormFieldRow
            id="mb-to"
            label="Last day"
            type="date"
            value={values.endsOn}
            error={errors.endsOn}
            onChange={(e) => setValues({ ...values, endsOn: e.target.value })}
          />
          <FormFieldRow
            id="mb-reason"
            label="Reason"
            className="sm:col-span-2"
            value={values.reason}
            error={errors.reason}
            onChange={(e) => setValues({ ...values, reason: e.target.value })}
          />
        </div>
        <FormErrorAlert message={serverError} />
        <DialogFooter>
          <Button variant="secondary" onClick={onClose}>
            Close
          </Button>
          <Button onClick={() => void add()} disabled={create.isPending}>
            Add maintenance
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/** `UnitTable` — HO-BRD-002 §11.11, US-BRD-01. */
export function UnitsAdmin() {
  const units = useBoardingUnits();
  const [editing, setEditing] = useState<BoardingUnit | 'new' | null>(null);
  const [maintaining, setMaintaining] = useState<BoardingUnit | null>(null);
  return (
    <div className="flex flex-col gap-4">
      <div>
        <Button onClick={() => setEditing('new')}>Add a unit</Button>
      </div>
      <QueryGate query={units} loadingLabel="Loading units…">
        {(list) => (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[40rem] border-collapse text-sm">
              <caption className="sr-only">Boarding units</caption>
              <thead>
                <tr className="bg-surface-sunken text-left text-xs uppercase text-ink-muted">
                  <th scope="col" className="p-2">
                    Unit
                  </th>
                  <th scope="col" className="p-2">
                    Type
                  </th>
                  <th scope="col" className="p-2">
                    Cats
                  </th>
                  <th scope="col" className="p-2">
                    Block
                  </th>
                  <th scope="col" className="p-2">
                    Nightly rate
                  </th>
                  <th scope="col" className="p-2">
                    Status
                  </th>
                  <th scope="col" className="p-2">
                    <span className="sr-only">Actions</span>
                  </th>
                </tr>
              </thead>
              <tbody>
                {list.map((u) => (
                  <tr key={u.id} className="border-b border-border">
                    <th scope="row" className="p-2 text-left">
                      {u.code} · {u.name}
                    </th>
                    <td className="p-2">{UNIT_TYPE_LABEL[u.unitType]}</td>
                    <td className="p-2">{u.capacityCats}</td>
                    <td className="p-2">{u.block ?? '—'}</td>
                    <td className="p-2">
                      {u.nightlyRate ? formatMoney(u.nightlyRate) : 'Not priced'}{' '}
                      <Link
                        href={STAFF_ADMIN_BOARDING_RATE_CARDS}
                        className="text-xs font-semibold underline"
                      >
                        rate card
                      </Link>
                    </td>
                    <td className="p-2">
                      <Badge variant={u.isActive ? 'success' : 'neutral'}>
                        {u.isActive ? 'Active' : 'Switched off'}
                      </Badge>
                    </td>
                    <td className="flex gap-2 p-2">
                      <Button size="sm" variant="secondary" onClick={() => setEditing(u)}>
                        Edit
                      </Button>
                      <Button size="sm" variant="secondary" onClick={() => setMaintaining(u)}>
                        Maintenance
                      </Button>
                      <ActivationButton unit={u} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </QueryGate>
      {editing && (
        <UnitForm unit={editing === 'new' ? undefined : editing} onClose={() => setEditing(null)} />
      )}
      {maintaining && <MaintenanceDialog unit={maintaining} onClose={() => setMaintaining(null)} />}
    </div>
  );
}

function ActivationButton({ unit }: { unit: BoardingUnit }) {
  const toggle = useSetUnitActivation(unit.id);
  const [error, setError] = useState<string | null>(null);
  return (
    <>
      <Button
        size="sm"
        variant="secondary"
        onClick={async () => {
          setError(null);
          try {
            await toggle.mutateAsync(!unit.isActive);
          } catch (e) {
            setError(describeBoardingError(e));
          }
        }}
      >
        {unit.isActive ? 'Switch off' : 'Switch on'}
      </Button>
      {error && (
        <span role="alert" className="text-xs font-semibold text-danger-ink">
          {error}
        </span>
      )}
    </>
  );
}
