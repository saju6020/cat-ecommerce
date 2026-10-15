'use client';

import { useCallback, useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { Banner } from '@/components/inventory/fields';
import { toast } from '@/components/ui/use-toast';
import { ApiValidationError } from '@/lib/api/client';
import { describeSurgeryError, surgeryViolations } from '@/lib/api/errors';
import { timeOnlyInZone } from '@/lib/format/time';
import { useReportAutosave } from '@/lib/hooks/useReportAutosave';
import { useFinaliseReport } from '@/lib/hooks/useSurgicalReport';
import {
  REPORT_FIELD_LABELS,
  SURGERY_LIMITS,
  parseMissingReportFields,
  type ReportField,
} from '@/lib/validation/surgery';
import type { SurgicalReport } from '@/lib/api/surgical-reports';
import { errorClass, hintClass, inputClass, labelClass } from '@/components/surgery/styles';

interface Values {
  procedurePerformed: string;
  findings: string;
  technique: string;
  sutureMaterial: string;
  hadComplications: boolean;
  complications: string;
}

const TEXT_FIELDS: Array<{ name: Exclude<ReportField, 'complications'>; rows: number }> = [
  { name: 'procedurePerformed', rows: 2 },
  { name: 'findings', rows: 4 },
  { name: 'technique', rows: 4 },
  { name: 'sutureMaterial', rows: 2 },
];

/**
 * ⭐ `SurgicalReportEditor` — HO-SRG-002 §11.5, ADR-FE-074, US-SRG-05. The draft: procedure performed
 * (prefilled with the procedure name), findings, technique, suture material, and complications (the
 * checkbox reveals the text, which is then required). It AUTOSAVES 5 s after the last change with the
 * draft's `expectedVersion` (*"Draft saved 10:42"*); a `409` shows a reload prompt that keeps the local
 * text for copying. **Finalise** (vets) is separate and confirmed; `ReportIncomplete` marks every missing
 * part and focuses the first. The surgeon and assistant are the surgery's own and shown as text. The
 * unsaved text survives a poll (nothing here is reset by a refetch) and navigation (`UnsavedChangesGuard`).
 */
export function SurgicalReportEditor({
  surgeryId,
  zone,
  report,
  procedureName,
  surgeonId,
  assistantId,
  surgeonName,
  assistantName,
  canFinalise,
  onReload,
}: {
  surgeryId: string;
  zone: string;
  report: SurgicalReport | null;
  procedureName: string;
  surgeonId: string;
  assistantId: string;
  surgeonName: string;
  assistantName: string;
  canFinalise: boolean;
  onReload: () => void;
}) {
  const form = useForm<Values>({
    defaultValues: {
      procedurePerformed: report?.procedurePerformed || procedureName,
      findings: report?.findings ?? '',
      technique: report?.technique ?? '',
      sutureMaterial: report?.sutureMaterial ?? '',
      hadComplications: report?.hadComplications ?? false,
      complications: report?.complications ?? '',
    },
  });
  const { register, watch, setFocus, getValues } = form;
  const autosave = useReportAutosave(surgeryId, report?.version);
  const finalise = useFinaliseReport(surgeryId);
  const [confirming, setConfirming] = useState(false);
  const [missing, setMissing] = useState<ReportField[]>([]);
  const [finaliseError, setFinaliseError] = useState<string | null>(null);
  const hadComplications = watch('hadComplications');
  const values = watch();

  const read = useCallback(() => {
    const v = getValues();
    return {
      procedurePerformed: v.procedurePerformed,
      findings: v.findings,
      technique: v.technique,
      sutureMaterial: v.sutureMaterial,
      hadComplications: v.hadComplications,
      complications: v.hadComplications ? v.complications : undefined,
      surgeonId,
      assistantId,
    };
  }, [getValues, surgeonId, assistantId]);

  const { schedule } = autosave;
  useEffect(() => {
    const subscription = watch(() => {
      setMissing([]);
      schedule(read);
    });
    return () => subscription.unsubscribe();
  }, [watch, schedule, read]);

  // After the confirm dialog has closed and handed focus back to its button, take it for the first gap.
  useEffect(() => {
    const first = missing[0];
    if (!first) return;
    const timer = setTimeout(() => setFocus(first), 150);
    return () => clearTimeout(timer);
  }, [missing, setFocus]);

  async function doFinalise() {
    setFinaliseError(null);
    // Make sure the server has the text on screen, then finalise THAT version.
    schedule(read);
    const saved = await autosave.saveNow();
    const version = autosave.currentVersion();
    if (!saved || version === undefined) {
      setConfirming(false);
      return;
    }
    try {
      await finalise.mutateAsync(version);
      toast({ title: 'Report finalised' });
      onReload();
    } catch (error) {
      if (error instanceof ApiValidationError) {
        const incomplete = surgeryViolations(error).find((v) => v.errorCode === 'ReportIncomplete');
        if (incomplete) {
          const fields = parseMissingReportFields(incomplete.message);
          setMissing(fields);
          setFinaliseError(incomplete.message);
          setConfirming(false);
          return;
        }
      }
      setFinaliseError(describeSurgeryError(error));
    } finally {
      setConfirming(false);
    }
  }

  const dirty = autosave.pending || autosave.status === 'error' || autosave.status === 'conflict';
  const stopped = autosave.status === 'conflict' || autosave.status === 'finalised';

  return (
    <form
      noValidate
      aria-label="Surgical report"
      className="flex max-w-3xl flex-col gap-4"
      onSubmit={(event) => {
        event.preventDefault();
        setConfirming(true);
      }}
    >
      <UnsavedChangesGuard when={dirty} />
      {stopped && (
        <div className="flex flex-col gap-2">
          <Banner tone="danger">
            {autosave.status === 'finalised'
              ? 'This report was finalised by someone else, so your changes were not saved.'
              : 'Someone else changed this report, so your changes were not saved.'}{' '}
            Your text is kept below — copy it before you reload.
          </Banner>
          <label htmlFor="report-local-copy" className={labelClass}>
            Your text, for copying
          </label>
          <textarea
            id="report-local-copy"
            readOnly
            rows={8}
            className={inputClass}
            value={[
              `Procedure performed: ${values.procedurePerformed}`,
              `Findings: ${values.findings}`,
              `Technique: ${values.technique}`,
              `Suture material: ${values.sutureMaterial}`,
              values.hadComplications ? `Complications: ${values.complications}` : '',
            ]
              .filter(Boolean)
              .join('\n\n')}
          />
          <div>
            <Button variant="secondary" onClick={onReload}>
              Reload the latest report
            </Button>
          </div>
        </div>
      )}
      {missing.length > 0 && (
        <Banner tone="danger">
          The report is not complete. Fill in:{' '}
          {missing.map((field) => REPORT_FIELD_LABELS[field]).join(', ')}.
        </Banner>
      )}
      {missing.length === 0 && finaliseError && (
        <p role="alert" className={errorClass}>
          {finaliseError}
        </p>
      )}

      {TEXT_FIELDS.map(({ name, rows }) => {
        const isMissing = missing.includes(name);
        return (
          <div key={name} className="flex flex-col gap-1.5">
            <label htmlFor={`report-${name}`} className={labelClass}>
              {REPORT_FIELD_LABELS[name]}
            </label>
            <textarea
              id={`report-${name}`}
              rows={rows}
              maxLength={name === 'sutureMaterial' ? 500 : SURGERY_LIMITS.reportTextMax}
              disabled={stopped}
              aria-invalid={isMissing ? true : undefined}
              className={inputClass}
              {...register(name)}
            />
            {isMissing && (
              <p className={errorClass}>
                {REPORT_FIELD_LABELS[name]} is required before finalising.
              </p>
            )}
          </div>
        );
      })}

      <div className="flex flex-col gap-2">
        <label className="flex items-center gap-2 text-sm font-semibold text-ink">
          <input
            type="checkbox"
            disabled={stopped}
            className="h-4 w-4"
            {...register('hadComplications')}
          />
          Complications occurred
        </label>
        {hadComplications && (
          <div className="flex flex-col gap-1.5">
            <label htmlFor="report-complications" className={labelClass}>
              {REPORT_FIELD_LABELS.complications}
            </label>
            <textarea
              id="report-complications"
              rows={3}
              maxLength={SURGERY_LIMITS.reportTextMax}
              disabled={stopped}
              aria-invalid={missing.includes('complications') ? true : undefined}
              className={inputClass}
              {...register('complications')}
            />
            {missing.includes('complications') && (
              <p className={errorClass}>Describe the complications.</p>
            )}
          </div>
        )}
      </div>

      <p className={hintClass}>
        Surgeon {surgeonName} · Assistant {assistantName}
      </p>

      <div className="flex flex-wrap items-center gap-3">
        <p role="status" aria-live="polite" className="text-sm text-ink-muted">
          {autosave.status === 'saving' && 'Saving…'}
          {autosave.status === 'saved' &&
            autosave.savedAt &&
            `Draft saved ${timeOnlyInZone(autosave.savedAt, zone)}`}
          {autosave.status === 'error' && (autosave.message ?? 'The draft could not be saved.')}
          {autosave.status === 'idle' && (report ? 'Draft' : 'Not saved yet')}
        </p>
        <Button variant="secondary" disabled={stopped} onClick={() => void autosave.saveNow()}>
          Save draft now
        </Button>
        {canFinalise && (
          <Button type="submit" disabled={stopped || finalise.isPending}>
            Finalise report
          </Button>
        )}
      </div>

      <ConfirmDialog
        open={confirming}
        onOpenChange={setConfirming}
        title="Finalise this report?"
        consequence="After finalising, the report can only be changed by addendum."
        confirmLabel="Finalise report"
        pendingLabel="Finalising…"
        onConfirm={doFinalise}
      />
    </form>
  );
}
