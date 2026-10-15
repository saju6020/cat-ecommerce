'use client';

import * as React from 'react';
import { Controller, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { ListboxPicker } from '@/components/formulary/ListboxPicker';
import { ActionDialog } from '@/components/inventory/ReasonDialog';
import {
  Banner,
  DefinitionList,
  Panel,
  TextAreaField,
  TextField,
} from '@/components/inventory/fields';
import { Quantity, QuantityInput, SignedQuantity } from '@/components/common/Quantity';
import { WitnessDialog, type WitnessCredentials } from '@/components/pharmacy/WitnessDialog';
import type { CatListItem } from '@/lib/api/cats';
import type { CorrectionInput, RegisterEntry } from '@/lib/api/controlled-drugs';
import { describePharmacyError, pharmacyCode, pharmacyViolations } from '@/lib/api/errors';
import { formatInstantInZone } from '@/lib/format/pharmacy';
import { ApiValidationError } from '@/lib/api/client';
import { applyValidationErrors, PHARMACY_CODE_MESSAGES } from '@/lib/api/errors';

/**
 * ⭐ `CorrectionForm` — HO-PHM-002 §11.6 (FR-PHM-7, C-2). Adds a NEW entry that corrects an existing one;
 * the original is shown read-only above and never changes. Fields: the corrected dose text, the
 * corrected cat, a quantity change (it may be 0; *"−1 means one fewer unit in stock than recorded"*),
 * notes and a required **reason**. A non-zero quantity says it also adjusts stock by the same amount.
 * Submit asks for confirmation (*"This adds entry #1206 correcting #1200; the original stays as it is"*),
 * then a witness — a vet or a nurse, signing in with their own details — before anything is sent.
 * Vet only (owner decision 2026-10-10): the server refuses anyone else.
 */
const schema = z.object({
  correctedDoseText: z.string().trim().max(200, 'Keep the dose text to 200 characters or fewer.'),
  correctedCatId: z.string(),
  quantityChange: z
    .string()
    .trim()
    .regex(/^-?\d+(\.\d{1,4})?$/, 'Enter a number, for example 0, 1 or -1.'),
  notes: z.string().trim().max(500, 'Keep the notes to 500 characters or fewer.'),
  reason: z
    .string()
    .trim()
    .min(1, 'Give the reason for the correction.')
    .max(500, 'Keep the reason to 500 characters or fewer.'),
});
type Values = z.infer<typeof schema>;

const FIELD_MAP = {
  CorrectedDoseText: 'correctedDoseText',
  CorrectedCatId: 'correctedCatId',
  QuantityDelta: 'quantityChange',
  Notes: 'notes',
  Reason: 'reason',
} as const;

export function CorrectionForm({
  original,
  zone,
  nextEntryNumber,
  cats,
  onSearchCats,
  onSubmit,
  onDone,
}: {
  original: RegisterEntry;
  zone: string;
  /** The number the new entry is expected to get (the newest entry's plus one). */
  nextEntryNumber: number | null;
  cats: CatListItem[];
  onSearchCats: (term: string) => void;
  onSubmit: (input: CorrectionInput) => Promise<{ entryNumber: number }>;
  onDone: (entryNumber: number) => void;
}) {
  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: {
      correctedDoseText: '',
      correctedCatId: '',
      quantityChange: '0',
      notes: '',
      reason: '',
    },
  });
  const { control, register, handleSubmit, setError, watch, setValue, formState } = form;
  const [draft, setDraft] = React.useState<Values | null>(null);
  const [confirmOpen, setConfirmOpen] = React.useState(false);
  const [witnessOpen, setWitnessOpen] = React.useState(false);
  const [witnessError, setWitnessError] = React.useState<string | null>(null);
  const [busy, setBusy] = React.useState(false);
  const [leaving, setLeaving] = React.useState(false);
  const [pickedCat, setPickedCat] = React.useState<string | null>(null);
  const change = watch('quantityChange');
  const nonZero = /^-?\d+(\.\d{1,4})?$/.test(change) && Number(change) !== 0;
  const unit = original.quantityDelta.unit;

  async function send(witness: WitnessCredentials) {
    if (!draft) return;
    setBusy(true);
    setWitnessError(null);
    try {
      const created = await onSubmit({
        correctedDoseText: draft.correctedDoseText || null,
        correctedCatId: draft.correctedCatId || null,
        quantityDelta: Number(draft.quantityChange),
        notes: draft.notes || null,
        reason: draft.reason,
        witnessIdentifier: witness.identifier,
        witnessPassword: witness.password,
      });
      setWitnessOpen(false);
      setLeaving(true); // stand the unsaved-changes guard down BEFORE the caller navigates
      onDone(created.entryNumber);
    } catch (error) {
      if (pharmacyCode(error, 'WitnessNotVerified', 'WitnessRequired')) {
        setWitnessError(pharmacyViolations(error)[0]?.message ?? describePharmacyError(error));
      } else {
        setWitnessOpen(false);
        if (error instanceof ApiValidationError) {
          applyValidationErrors(error, setError, {
            fieldMap: FIELD_MAP,
            knownFields: Object.values(FIELD_MAP),
            codeMessages: PHARMACY_CODE_MESSAGES,
          });
        } else {
          setError('root', { type: 'server', message: describePharmacyError(error) });
        }
      }
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="flex flex-col gap-4">
      {!leaving && <UnsavedChangesGuard when={formState.isDirty} />}
      <Panel title={`The entry being corrected — #${original.entryNumber}`} headingId="cf-original">
        <DefinitionList
          items={[
            { label: 'Recorded', value: formatInstantInZone(original.recordedOn, zone) },
            {
              label: 'Medicine',
              value: `${original.medicine.name}, ${original.lotNumber ? `lot ${original.lotNumber}` : 'no lot'}`,
            },
            { label: 'Quantity', value: <SignedQuantity value={original.quantityDelta} /> },
            { label: 'Running balance', value: <Quantity value={original.runningBalance} /> },
            { label: 'Dose', value: original.doseText ?? 'None recorded' },
            { label: 'Cat', value: original.catName ?? 'Not linked to a cat' },
          ]}
        />
        <p className="mt-2 text-xs text-ink-muted">
          This entry is read-only and stays exactly as it is.
        </p>
      </Panel>
      <form
        noValidate
        aria-label="Add a correction"
        onSubmit={handleSubmit((values) => {
          setDraft(values);
          setConfirmOpen(true);
        })}
        className="flex max-w-2xl flex-col gap-4"
      >
        {formState.errors.root?.message && (
          <p role="alert" className="text-sm font-semibold text-danger-ink">
            {formState.errors.root.message}
          </p>
        )}
        <TextField
          id="cf-dose"
          label="Corrected dose text"
          hint="Leave empty to keep the recorded dose."
          error={formState.errors.correctedDoseText?.message}
          {...register('correctedDoseText')}
        />
        <div className="flex flex-col gap-1">
          <ListboxPicker<CatListItem>
            id="cf-cat"
            label="Corrected cat"
            items={cats}
            getKey={(cat) => cat.id}
            getLabel={(cat) => cat.name}
            renderDetail={(cat) => <span>{cat.breed ?? cat.species}</span>}
            onSearch={onSearchCats}
            onSelect={(cat) => {
              setValue('correctedCatId', cat.id, { shouldDirty: true });
              setPickedCat(cat.name);
            }}
            minLength={1}
            noun="cats"
            hint="Leave empty to keep the recorded cat. Cats are listed from the first hundred on file."
          />
          {pickedCat && <p className="text-xs text-ink-muted">Chosen: {pickedCat}</p>}
        </div>
        <Controller
          control={control}
          name="quantityChange"
          render={({ field }) => (
            <QuantityInput
              id="cf-quantity"
              label="Quantity change"
              required
              amount={field.value}
              unit={unit}
              fixedUnit={unit}
              onChange={(next) => field.onChange(next.amount)}
              hint="−1 means one fewer unit in stock than recorded. It may be 0 (for a wording-only correction)."
              error={formState.errors.quantityChange?.message}
            />
          )}
        />
        {nonZero && <Banner tone="warning">This also adjusts stock by the same amount.</Banner>}
        <TextAreaField
          id="cf-notes"
          label="Notes"
          rows={2}
          error={formState.errors.notes?.message}
          {...register('notes')}
        />
        <TextAreaField
          id="cf-reason"
          label="Reason for the correction"
          required
          rows={3}
          error={formState.errors.reason?.message}
          {...register('reason')}
        />
        <div>
          <Button type="submit" disabled={formState.isSubmitting || busy}>
            Review and add correction
          </Button>
        </div>
      </form>
      <ActionDialog
        open={confirmOpen}
        onOpenChange={setConfirmOpen}
        title="Add this correction?"
        consequence={`This adds ${nextEntryNumber ? `entry #${nextEntryNumber}` : 'a new entry'} correcting #${original.entryNumber}; the original stays as it is.${nonZero ? ' It also adjusts stock by the same amount.' : ''} A witness will be asked to sign in next.`}
        confirmLabel="Continue to witness"
        onConfirm={async () => {
          setWitnessError(null);
          setWitnessOpen(true);
        }}
      />
      <WitnessDialog
        open={witnessOpen}
        witnessRoles={['Vet', 'Nurse']}
        busy={busy}
        error={witnessError}
        confirmLabel="Confirm and add correction"
        onCancel={() => {
          setWitnessOpen(false);
          setWitnessError(null);
        }}
        onSubmit={(credentials) => void send(credentials)}
      />
    </div>
  );
}
