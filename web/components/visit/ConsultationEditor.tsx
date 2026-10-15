'use client';

import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react';
import { useForm } from 'react-hook-form';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { toast } from '@/components/ui/use-toast';
import { VisitHeaderBar } from './VisitHeaderBar';
import { SoapEditor } from './SoapEditor';
import { ProblemListEditor } from './ProblemListEditor';
import { FinaliseDialog } from './FinaliseDialog';
import { RestoredDraftNotice, TemplatePrefillNotice } from './DraftNotices';
import { SaveConflictPanel } from './SaveConflictPanel';
import { ApiConflictError } from '@/lib/api/client';
import { mapClinicalFormErrors } from '@/lib/api/errors';
import { useConsultationAutosave } from '@/lib/hooks/useConsultationAutosave';
import { useFinaliseVisit, useSaveNote } from '@/lib/hooks/useVisit';
import {
  finaliseChecklist,
  initialNoteValues,
  isNoteDirty,
  toDraftRequest,
  toSaveNoteRequest,
  type NoteFormValues,
} from '@/lib/validation/clinical';
import type { VisitDetail } from '@/lib/api/visits';

/**
 * ⭐ `ConsultationEditor` — HO-EMR-002 §11.1 (US-CLN-12, 17, 20). The editable open visit: the SOAP
 * form, the coded problem list, auto-save to the server working copy (ADR-FE-050), explicit **Save
 * note**, `Ctrl/Cmd+S`, the finalise flow, and the `409` side-by-side. The form is initialised ONCE
 * per mount from (restored draft ‖ saved note ‖ template) — a visit refetch (a nurse's vitals, a
 * panel write) never re-initialises it, so nothing typed can be lost to a refresh (ADR-FE-049).
 * The history drawer is a sibling of this component, not a navigation, so it cannot cost the draft.
 */
export function ConsultationEditor({
  visit,
  catHeader,
  panels,
  onOpenHistory,
  onFinalised,
}: {
  visit: VisitDetail;
  /** The compact cat header (UI-7), shown under the heading. */
  catHeader: ReactNode;
  /** The right-hand column (vitals, diagnostics, photos, follow-up). */
  panels: ReactNode;
  onOpenHistory: () => void;
  onFinalised?: () => void;
}) {
  const [initial] = useState(() => initialNoteValues(visit));
  const form = useForm<NoteFormValues>({ defaultValues: initial.values });
  const values = form.watch();
  const [savedValues, setSavedValues] = useState(initial.saved);
  const [source, setSource] = useState(initial.source);
  const [finaliseOpen, setFinaliseOpen] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [conflict, setConflict] = useState<NoteFormValues | null>(null);
  const versionRef = useRef(visit.version);
  const failureRef = useRef<string>('');
  useEffect(() => {
    versionRef.current = Math.max(versionRef.current, visit.version);
  }, [visit.version]);

  const saveNote = useSaveNote(visit.id, visit.catId);
  const finalise = useFinaliseVisit(visit.id, visit.catId);
  const autosave = useConsultationAutosave({
    visitId: visit.id,
    snapshot: toDraftRequest(values),
    initialSnapshot: toDraftRequest(initial.values),
    enabled: visit.allowedActions.includes('editNote'),
  });
  const dirty = isNoteDirty(values, savedValues);

  /** Writes the record. Returns the new version, or `null` after showing why it failed. */
  const save = useCallback(async (): Promise<number | null> => {
    const current = form.getValues();
    // Keep a saved problem's id stable across saves (the form rows were built before the server minted it).
    const known = new Map(visit.problems.map((p) => [p.diagnosisCodeId, p.id]));
    const withIds = {
      ...current,
      problems: current.problems.map((p) => ({
        ...p,
        problemId: p.problemId ?? known.get(p.diagnosisCodeId),
      })),
    };
    setSaveError(null);
    setFieldErrors({});
    try {
      // Let a draft save already on its way land first, so it cannot land AFTER the note.
      await autosave.settle();
      const result = await saveNote.mutateAsync(
        toSaveNoteRequest(
          withIds,
          versionRef.current,
          visit.template && source === 'template'
            ? { id: visit.template.id, version: visit.template.version }
            : null,
        ),
      );
      versionRef.current = result.version;
      setSavedValues(current);
      setConflict(null);
      autosave.markSaved(toDraftRequest(current));
      return result.version;
    } catch (error) {
      if (error instanceof ApiConflictError) {
        failureRef.current =
          'Someone else saved this note since you opened it. Your text is kept — see the comparison above.';
        setConflict(current);
      } else {
        const mapped = mapClinicalFormErrors(error, {
          Subjective: 'subjective',
          Objective: 'objective',
          Assessment: 'assessment',
          Plan: 'plan',
          DischargeInstructions: 'dischargeInstructions',
        });
        setFieldErrors(mapped.fields);
        const message =
          mapped.root ??
          (Object.keys(mapped.fields).length ? 'Fix the fields marked below.' : null);
        failureRef.current = message ?? '';
        setSaveError(message);
      }
      return null;
    }
  }, [form, visit.problems, visit.template, source, saveNote, autosave]);

  // Ctrl/Cmd+S saves the note, never the browser page (OQ-2).
  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 's') {
        event.preventDefault();
        void save().then((version) => version !== null && toast({ title: 'Note saved' }));
      }
    }
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [save]);

  async function confirmFinalise() {
    let version = versionRef.current;
    if (dirty) {
      const saved = await save();
      if (saved === null) {
        throw Object.assign(new Error(failureRef.current || 'The note could not be saved.'), {
          name: 'FinaliseBlocked',
        });
      }
      version = saved;
    }
    await finalise.mutateAsync(version);
    toast({ title: `${visit.catName}’s visit was finalised` });
    onFinalised?.();
  }

  return (
    <div className="flex flex-col gap-5 pb-24 lg:pb-0">
      <UnsavedChangesGuard when={dirty} />
      <VisitHeaderBar
        visit={visit}
        autosave={autosave}
        saving={saveNote.isPending}
        onSave={() =>
          void save().then((version) => version !== null && toast({ title: 'Note saved' }))
        }
        onFinalise={() => setFinaliseOpen(true)}
        onOpenHistory={onOpenHistory}
      />
      {catHeader}
      {saveError && (
        <p role="alert" className="text-sm font-semibold text-danger-ink">
          {saveError}
        </p>
      )}
      {conflict && (
        <SaveConflictPanel
          visit={visit}
          mine={conflict}
          zone={visit.branchTimeZoneId}
          onDismiss={() => setConflict(null)}
        />
      )}
      {source === 'template' && visit.template && (
        <TemplatePrefillNotice templateName={visit.template.name} />
      )}
      {source === 'draft' && visit.draft && (
        <RestoredDraftNotice
          savedOn={visit.draft.savedOn}
          zone={visit.branchTimeZoneId}
          hasSavedNote={visit.note.savedOn !== null}
          onRevert={() => {
            form.reset(savedValues);
            setSource('saved');
          }}
        />
      )}
      <div className="grid items-start gap-5 lg:grid-cols-[minmax(0,1.5fr)_minmax(0,1fr)]">
        <div className="flex min-w-0 flex-col gap-5">
          <SoapEditor form={form} fieldErrors={fieldErrors} />
          <ProblemListEditor
            problems={values.problems}
            onChange={(update) =>
              form.setValue('problems', update(form.getValues('problems')), { shouldDirty: true })
            }
          />
        </div>
        <div className="flex min-w-0 flex-col gap-5">{panels}</div>
      </div>
      <FinaliseDialog
        open={finaliseOpen}
        onOpenChange={setFinaliseOpen}
        catName={visit.catName}
        checklist={finaliseChecklist(values)}
        pendingOrders={visit.diagnostics.filter((d) => d.status === 'Pending').length}
        hasUnsavedChanges={dirty}
        onConfirm={confirmFinalise}
      />
    </div>
  );
}
