import { Suspense } from 'react';
import { LoadingState } from '@/components/common/LoadingState';
import { NoteTemplateManager } from '@/components/clinical-catalogue/NoteTemplateManager';

/** `/staff/admin/clinical/note-templates` — HO-EMR-002 §8.1/§11.8: the clinical catalogue of note templates. */
export default function NoteTemplateManagerPage() {
  return (
    <Suspense fallback={<LoadingState variant="table" rows={5} label="Loading the catalogue…" />}>
      <NoteTemplateManager />
    </Suspense>
  );
}
