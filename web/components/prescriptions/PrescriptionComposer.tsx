'use client';

import * as React from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { FormularyPicker } from '@/components/formulary/FormularyPicker';
import { Banner } from '@/components/inventory/fields';
import { LoadingState } from '@/components/common/LoadingState';
import { ClinicalText } from '@/components/common/Quantity';
import { toast } from '@/components/ui/use-toast';
import { pharmacyCode } from '@/lib/api/errors';
import {
  assessmentFromError,
  type IssuePrescriptionRequest,
  type RenewPrescriptionRequest,
} from '@/lib/api/prescriptions';
import { formLabel } from '@/lib/format/pharmacy';
import { doseUnitsForForm } from '@/lib/format/units';
import { useCat } from '@/lib/hooks/useCat';
import { useRecordCatWeight } from '@/lib/hooks/useCatMutations';
import { useFormularyList, useFormularyMedicine } from '@/lib/hooks/useFormulary';
import {
  useIssuePrescription,
  usePrescription,
  useRenewPrescription,
  useSafetyCheck,
} from '@/lib/hooks/usePrescriptions';
import { useStaffRoles } from '@/lib/hooks/useStaffRoles';
import { useVisit } from '@/lib/hooks/useVisit';
import { submitPharmacyForm } from '@/lib/validation/pharmacy';
import { AcknowledgementBlock } from './AcknowledgementBlock';
import {
  COMPOSER_FIELD_MAP,
  COMPOSER_KNOWN_FIELDS,
  EMPTY_ACK,
  EMPTY_COMPOSER,
  ackProblems,
  composerSchema,
  hasProblems,
  toIssueRequest,
  toRequest,
  valuesFromPrescription,
  type AckProblems,
  type Acknowledgement,
  type ComposerValues,
} from './composerModel';
import { DoseFields } from './DoseFields';
import { RecordWeightDialog } from './RecordWeightDialog';
import { RepeatFields } from './RepeatFields';
import { SafetyPanel, describeChange } from './SafetyPanel';
import { NO_VISIT_MESSAGE, VisitContextBar } from './VisitContextBar';
import type { SafetyAssessment, SafetyCheckRequest } from '@/lib/api/prescriptions';

/**
 * ⭐ `PrescriptionComposer` — HO-PHM-002 §8.6 / §11.2 (US-PHM-01, 02, 03, 06 AC2), ADR-FE-059/060/061.
 * Exported for HO-EMR-002's consultation screen: it takes the cat, the open visit (or none, for a
 * renewal) and a callback, and never navigates on its own.
 *
 *  - The server is the only clinical authority. A debounced safety check runs on every relevant change;
 *    the panel renders the assessment; acknowledgements are stored AGAINST the fingerprint and cleared
 *    when it changes; submit sends that fingerprint; `409 PrescriptionWarningsChanged` replaces the
 *    panel with the new assessment (form values kept).
 *  - `renewFrom` prefills from the prescription being renewed and calls the renew endpoint. A renewal
 *    needs no open visit (remote renewal — owner decision 2026-10-10); every check still runs again.
 *  - Below 1024 px the panel sits above the submit button, never in a column that scrolls away; from
 *    1024 px it is a sticky second column.
 */
export interface PrescriptionComposerProps {
  catId: string;
  visitId?: string | null;
  /** The id of the prescription this one renews. */
  renewFrom?: string;
  onIssued: (prescriptionId: string, prescriptionNumber: string) => void;
}

function requestFrom(values: ComposerValues): SafetyCheckRequest | null {
  if (!values.medicineId || !values.doseUnit || !values.route || !values.frequency) return null;
  if (!/^\d+(\.\d{1,4})?$/.test(values.doseAmount) || Number(values.doseAmount) <= 0) return null;
  return {
    medicineId: values.medicineId,
    dose: { amount: Number(values.doseAmount), unit: values.doseUnit },
    route: values.route,
    frequency: values.frequency,
  };
}

export function PrescriptionComposer({
  catId,
  visitId,
  renewFrom,
  onIssued,
}: PrescriptionComposerProps) {
  const roles = useStaffRoles();
  const cat = useCat(catId);
  const visit = useVisit(visitId ?? '');
  const source = usePrescription(renewFrom ?? '');
  const form = useForm<ComposerValues>({
    resolver: zodResolver(composerSchema),
    defaultValues: EMPTY_COMPOSER,
  });
  const values = form.watch();
  const medicine = useFormularyMedicine(values.medicineId || null).data;
  const request = React.useMemo(
    () => requestFrom(values),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [values.medicineId, values.doseAmount, values.doseUnit, values.route, values.frequency],
  );
  const safety = useSafetyCheck(catId, request);
  const issue = useIssuePrescription(catId);
  const renew = useRenewPrescription(renewFrom ?? '', catId);
  const recordWeight = useRecordCatWeight(catId);

  const [ack, setAck] = React.useState<Acknowledgement>(EMPTY_ACK);
  const [ackChanged, setAckChanged] = React.useState(false);
  const [attempted, setAttempted] = React.useState(false);
  const [problems, setProblems] = React.useState<AckProblems | null>(null);
  const [visitClosed, setVisitClosed] = React.useState(false);
  const [changedNotice, setChangedNotice] = React.useState(false);
  const [weightOpen, setWeightOpen] = React.useState(false);
  const [leaving, setLeaving] = React.useState(false);
  const [announcement, setAnnouncement] = React.useState('');
  const weightRef = React.useRef<HTMLHeadingElement>(null);
  const previous = React.useRef<SafetyAssessment | undefined>(undefined);

  const current = safety.assessment && !safety.isStale ? safety.assessment : undefined;

  // ADR-FE-060: ticks belong to the fingerprint they were given against — a new one clears them.
  if (current && ack.fingerprint !== current.fingerprint) {
    const hadTicks = Object.values(ack.ticks).some(Boolean);
    setAck({ ...ack, fingerprint: current.fingerprint, ticks: {} });
    setAckChanged(hadTicks);
    setProblems(null);
  }

  React.useEffect(() => {
    if (!current) return;
    const text = describeChange(previous.current, current);
    previous.current = current;
    if (text) setAnnouncement(text);
  }, [current]);

  // Renewal: prefill once the source and its formulary medicine are known.
  const sourceData = source.data;
  const matches = useFormularyList(
    { search: sourceData?.medicine.name, isActive: true, pageSize: 50 },
    Boolean(renewFrom && sourceData),
  );
  const prefilled = React.useRef(false);
  const matchedId = matches.data?.items.find(
    (item) =>
      item.name === sourceData?.medicine.name &&
      item.strengthText === sourceData?.medicine.strength,
  )?.id;
  React.useEffect(() => {
    if (!sourceData || !matches.data || prefilled.current) return;
    prefilled.current = true;
    form.reset(valuesFromPrescription(sourceData, matchedId ?? ''));
  }, [sourceData, matches.data, matchedId, form]);

  // A medicine's own routes and units: keep the choices valid when the medicine changes.
  React.useEffect(() => {
    if (!medicine) return;
    if (values.route && !medicine.allowedRoutes.includes(values.route as never))
      form.setValue('route', '');
    if (!values.route && medicine.allowedRoutes.includes(medicine.defaultRoute))
      form.setValue('route', medicine.defaultRoute);
    if (values.doseUnit && !doseUnitsForForm(medicine.form).includes(values.doseUnit))
      form.setValue('doseUnit', '');
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [medicine]);

  if (roles.ready && !roles.has('Vet')) return <PermissionDeniedState />;

  const isRenewal = Boolean(renewFrom);
  const remote = isRenewal && !visitId;
  if (!isRenewal && !visitId) {
    return (
      <Banner tone="warning" role="alert">
        {NO_VISIT_MESSAGE}
      </Banner>
    );
  }
  if (isRenewal && (source.isLoading || (matches.isLoading && !matches.data))) {
    return <LoadingState variant="card" label="Loading the prescription…" />;
  }
  const catName = cat.data?.name ?? 'this cat';

  function react(error: unknown) {
    const replaced = assessmentFromError(error);
    if (replaced) safety.setAssessment(replaced);
    if (pharmacyCode(error, 'VisitNotOpen')) setVisitClosed(true);
    if (pharmacyCode(error, 'RecentWeightRequired')) weightRef.current?.focus();
  }

  async function submit(submitted: ComposerValues) {
    setAttempted(true);
    if (!current) return;
    const missing = ackProblems(current, ack);
    if (hasProblems(missing)) {
      setProblems(missing);
      requestAnimationFrame(() =>
        document.querySelector<HTMLElement>('[aria-invalid="true"]')?.focus(),
      );
      return;
    }
    setProblems(null);
    setChangedNotice(false);
    const fields = toRequest(submitted, current, ack);
    await submitPharmacyForm(
      async () => {
        try {
          const result = isRenewal
            ? await renew.mutateAsync({
                ...fields,
                visitId: visitId ?? null,
              } as RenewPrescriptionRequest)
            : await issue.mutateAsync(
                toIssueRequest(visitId as string, fields) as IssuePrescriptionRequest,
              );
          setLeaving(true); // stand the unsaved-changes guard down BEFORE the caller navigates
          toast({ title: 'Prescription issued', description: result.prescriptionNumber });
          onIssued(result.prescriptionId, result.prescriptionNumber);
        } catch (error) {
          react(error);
          throw error;
        }
      },
      form.setError,
      {
        fieldMap: COMPOSER_FIELD_MAP,
        knownFields: COMPOSER_KNOWN_FIELDS,
        onConflict: () => setChangedNotice(true),
      },
    );
  }

  const blockReason = visitClosed
    ? 'This visit is closed.'
    : safety.failed
      ? 'The safety check could not run. You cannot issue until it does.'
      : !request
        ? 'Choose a medicine and enter the dose, route and how often.'
        : !current
          ? 'Waiting for the safety check…'
          : safety.isChecking
            ? 'The safety check is running — wait for it to finish.'
            : current.weight.requirement !== 'Satisfied'
              ? 'Record a recent weight first.'
              : null;
  const ackNeeded = current?.warnings.some((w) => w.requiresAcknowledgement) ?? false;
  const rootError = form.formState.errors.root?.message;

  return (
    <div className="flex flex-col gap-4">
      {!leaving && <UnsavedChangesGuard when={form.formState.isDirty} />}
      <VisitContextBar visit={visit.data} remoteRenewal={remote} closed={visitClosed} />
      {visit.data && visit.data.catId !== catId && (
        <Banner tone="danger">
          That visit is for a different cat. Choose one of {catName}’s visits.
        </Banner>
      )}
      {isRenewal && sourceData && !matchedId && (
        <Banner tone="warning">
          We could not match {sourceData.medicine.name} in the formulary, so choose the medicine
          again.
        </Banner>
      )}
      {isRenewal && (
        <Banner tone="info">All checks run again on today’s weight and records.</Banner>
      )}
      {!visitClosed && visit.data?.status !== 'Finalised' && (
        <form
          noValidate
          aria-label="Prescription"
          onSubmit={form.handleSubmit(submit)}
          className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_22rem] lg:items-start"
        >
          <section
            aria-label="Prescription details"
            className="flex min-w-0 flex-col gap-4 rounded-[var(--radius-md)] border border-border bg-surface p-4 lg:col-start-1"
          >
            {medicine && isRenewal ? (
              <MedicineLine medicine={medicine} />
            ) : medicine ? (
              <div className="flex flex-wrap items-start justify-between gap-2">
                <MedicineLine medicine={medicine} />
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => {
                    form.setValue('medicineId', '', { shouldDirty: true });
                    form.setValue('route', '');
                    form.setValue('doseUnit', '');
                  }}
                >
                  Change medicine
                </Button>
              </div>
            ) : (
              <FormularyPicker
                id="rx-medicine"
                label="Medicine"
                required
                clearOnSelect={false}
                error={form.formState.errors.medicineId?.message}
                onSelect={(picked) =>
                  form.setValue('medicineId', picked.id, {
                    shouldDirty: true,
                    shouldValidate: attempted,
                  })
                }
              />
            )}
            <DoseFields form={form} medicine={medicine} />
            <RepeatFields form={form} />
          </section>

          <div className="flex min-w-0 flex-col gap-4 lg:sticky lg:top-4 lg:col-start-2 lg:row-span-2 lg:row-start-1">
            <SafetyPanel
              assessment={safety.assessment}
              isChecking={safety.isChecking}
              failed={safety.failed}
              hasInput={Boolean(request)}
              announcement={announcement}
              weightRef={weightRef}
              onRecordWeight={() => setWeightOpen(true)}
            />
          </div>

          <div className="flex min-w-0 flex-col gap-4 lg:col-start-1">
            {current && (ackNeeded || ackChanged) && (
              <AcknowledgementBlock
                assessment={current}
                ack={ack}
                problems={problems}
                changed={ackChanged}
                onTick={(key, ticked) => {
                  setAckChanged(false);
                  setAck({ ...ack, ticks: { ...ack.ticks, [key]: ticked } });
                }}
                onReason={(value) => setAck({ ...ack, allergyReason: value })}
                onNote={(value) => setAck({ ...ack, doseNote: value })}
              />
            )}
            {changedNotice ? (
              <p role="alert" className="text-sm font-semibold text-warning-ink">
                Something changed — please review the warnings again.
              </p>
            ) : (
              rootError && (
                <p role="alert" className="text-sm font-semibold text-danger-ink">
                  {rootError}
                </p>
              )
            )}
            <div className="flex flex-wrap items-center gap-3">
              <Button
                type="submit"
                disabled={Boolean(blockReason) || form.formState.isSubmitting}
                aria-busy={form.formState.isSubmitting}
                aria-describedby={blockReason ? 'rx-block-reason' : undefined}
              >
                {form.formState.isSubmitting
                  ? 'Issuing…'
                  : isRenewal
                    ? 'Issue renewal'
                    : 'Issue prescription'}
              </Button>
              <span id="rx-block-reason" className="text-sm text-ink-muted">
                {blockReason ?? 'Nothing is issued until the server accepts it.'}
              </span>
            </div>
          </div>
        </form>
      )}
      <RecordWeightDialog
        open={weightOpen}
        onOpenChange={setWeightOpen}
        catName={catName}
        onRecord={async (weight) => {
          await recordWeight.mutateAsync({
            kilograms: weight.kilograms,
            recordedOn: weight.recordedOn,
            notes: weight.notes || null,
          });
          void safety.refetch();
        }}
      />
    </div>
  );
}

function MedicineLine({
  medicine,
}: {
  medicine: NonNullable<ReturnType<typeof useFormularyMedicine>['data']>;
}) {
  return (
    <div className="flex min-w-0 flex-col gap-1">
      <p className="text-sm font-semibold text-ink">
        {medicine.name} <ClinicalText>{medicine.strength.text}</ClinicalText>
      </p>
      <p className="flex flex-wrap items-center gap-2 text-xs text-ink-muted">
        <span>{formLabel(medicine.form)}</span>
        <span>{medicine.code}</span>
        {medicine.isControlled && <Badge variant="warning">Controlled</Badge>}
        {medicine.linkedSkus.length === 0 && (
          <Badge variant="neutral" className="whitespace-normal text-left">
            Not stocked here — it can be prescribed but not dispensed
          </Badge>
        )}
      </p>
    </div>
  );
}
