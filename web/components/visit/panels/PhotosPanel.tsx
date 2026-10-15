'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { MediaViewer } from '@/components/files/MediaViewer';
import { FileUploadField } from '@/components/files/FileUploadField';
import { PhotoThumb, photoAsFile, photoCaption } from '../PhotoThumb';
import { mapClinicalFormErrors } from '@/lib/api/errors';
import { useAttachPhoto, useVisit } from '@/lib/hooks/useVisit';
import { todayInZone } from '@/lib/format/time';
import { MAX_PHOTO_NOTE } from '@/lib/validation/clinical';
import type { StoredFileViewModel } from '@/lib/api/files';
import type { VisitDetail, VisitPhoto } from '@/lib/api/visits';

/**
 * `PhotosPanel` — HO-EMR-002 §11.3 (US-CLN-19). Thumbnails from the visit's signed URLs with the date
 * taken and a note; **Add photo** (only with `attachPhoto` in `allowedActions`) uploads a
 * `ClinicalPhoto` through `FileUploadField`, then posts the attach with `fileId`, a taken-on date (not
 * in the future) and a note. A thumbnail whose URL expired is refreshed ONCE from the visit (U-X4);
 * a tap opens the larger image in the accessible `MediaViewer`.
 */
export function PhotosPanel({ visit }: { visit: VisitDetail }) {
  const [adding, setAdding] = useState(false);
  const [viewing, setViewing] = useState<VisitPhoto | null>(null);
  const reload = useVisit(visit.id);
  const canAdd = visit.allowedActions.includes('attachPhoto');
  const refreshPhoto = (id: string) => async () => {
    const fresh = (await reload.refetch()).data;
    return fresh?.photos.find((p) => p.id === id);
  };
  const viewingFile = viewing ? photoAsFile(viewing) : null;

  return (
    <section
      aria-labelledby="photos-heading"
      className="rounded-[var(--radius-md)] border border-border bg-surface"
    >
      <div className="flex items-center justify-between gap-2 border-b border-border px-4 py-3">
        <h2 id="photos-heading" className="text-sm font-bold text-ink">
          Photos
        </h2>
        {canAdd && !adding && (
          <Button variant="secondary" size="sm" onClick={() => setAdding(true)}>
            Add photo
          </Button>
        )}
      </div>
      <div className="flex flex-col gap-4 p-4">
        {adding && canAdd && <AddPhotoForm visit={visit} onDone={() => setAdding(false)} />}
        {visit.photos.length === 0 ? (
          <p className="text-sm text-ink-muted">No photos on this visit.</p>
        ) : (
          <ul className="m-0 flex list-none flex-wrap gap-3 p-0">
            {visit.photos.map((photo) => (
              <li key={photo.id}>
                <PhotoThumb photo={photo} onOpen={setViewing} onRefresh={refreshPhoto(photo.id)} />
              </li>
            ))}
          </ul>
        )}
      </div>
      <MediaViewer
        open={viewing !== null}
        onOpenChange={(next) => !next && setViewing(null)}
        file={viewingFile}
        caption={viewing ? photoCaption(viewing) : ''}
        onRefresh={
          viewing
            ? async () => {
                const fresh = await refreshPhoto(viewing.id)();
                return fresh ? photoAsFile(fresh) : null;
              }
            : undefined
        }
      />
    </section>
  );
}

function AddPhotoForm({ visit, onDone }: { visit: VisitDetail; onDone: () => void }) {
  const attach = useAttachPhoto(visit.id, visit.catId);
  const zone = visit.branchTimeZoneId;
  const [file, setFile] = useState<StoredFileViewModel | null>(null);
  const [takenOn, setTakenOn] = useState(() => todayInZone(zone));
  const [note, setNote] = useState('');
  const [errors, setErrors] = useState<{ fields: Record<string, string>; root: string | null }>({
    fields: {},
    root: null,
  });
  const [pending, setPending] = useState(false);

  async function submit() {
    if (!file) {
      setErrors({ fields: {}, root: 'Choose a photo to add first.' });
      return;
    }
    setPending(true);
    setErrors({ fields: {}, root: null });
    try {
      await attach.mutateAsync({
        fileId: file.fileId,
        takenOn,
        ...(note.trim() ? { note: note.trim() } : {}),
      });
      onDone();
    } catch (caught) {
      setErrors(mapClinicalFormErrors(caught, { TakenOn: 'takenOn', Note: 'note' }));
    } finally {
      setPending(false);
    }
  }

  return (
    <form
      noValidate
      aria-label="Add a photo"
      className="flex flex-col gap-3 rounded-[var(--radius-sm)] border border-border p-3"
      onSubmit={(event) => {
        event.preventDefault();
        void submit();
      }}
    >
      <FileUploadField
        id="photo-upload"
        label="Choose a photo"
        purpose="ClinicalPhoto"
        value={file}
        onChange={setFile}
      />
      <div className="flex flex-col gap-1.5">
        <label htmlFor="photo-taken-on" className="text-sm font-semibold text-ink">
          Taken on
        </label>
        <input
          id="photo-taken-on"
          type="date"
          max={todayInZone(zone)}
          value={takenOn}
          onChange={(event) => setTakenOn(event.target.value)}
          className="w-fit rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
        />
        {errors.fields.takenOn && (
          <p role="alert" className="text-xs font-semibold text-danger-ink">
            {errors.fields.takenOn}
          </p>
        )}
      </div>
      <div className="flex flex-col gap-1.5">
        <label htmlFor="photo-note" className="text-sm font-semibold text-ink">
          Note <span className="font-normal text-ink-muted">(optional)</span>
        </label>
        <input
          id="photo-note"
          type="text"
          maxLength={MAX_PHOTO_NOTE}
          value={note}
          onChange={(event) => setNote(event.target.value)}
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
        />
      </div>
      {errors.root && (
        <p role="alert" className="text-sm font-semibold text-danger-ink">
          {errors.root}
        </p>
      )}
      <div className="flex gap-2">
        <Button type="submit" disabled={pending} aria-busy={pending}>
          {pending ? 'Adding…' : 'Add photo'}
        </Button>
        <Button variant="ghost" onClick={onDone} disabled={pending}>
          Cancel
        </Button>
      </div>
    </form>
  );
}
