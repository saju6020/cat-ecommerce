'use client';

import { use, useCallback, useState } from 'react';
import { DeceasedBanner } from '@/components/cat/DeceasedBanner';
import { FileThumb } from '@/components/files/FileThumb';
import { FileUploadField } from '@/components/files/FileUploadField';
import { Button } from '@/components/ui/button';
import { toast } from '@/components/ui/use-toast';
import { describeApiError } from '@/lib/api/errors';
import type { CatPhoto } from '@/lib/api/cats';
import type { StoredFileViewModel } from '@/lib/api/files';
import { CAT_PHOTO_RULES } from '@/lib/catPhotoRules';
import { useCat } from '@/lib/hooks/useCat';
import { useAddCatPhoto } from '@/lib/hooks/useCatMutations';

function toFileViewModel(photo: CatPhoto): StoredFileViewModel {
  return {
    fileId: photo.fileId,
    url: photo.url,
    contentType: photo.contentType,
    sizeInBytes: photo.sizeInBytes,
    originalFileName: photo.originalFileName,
    urlExpiresOn: photo.urlExpiresOn,
  };
}

/**
 * Cat photos — US-CAT-01 AC3 (HO-CAT-002 §8.3, §1 Amended 2026-09-27 by HO-FIL-001, and the
 * 2026-10-09 follow-up). Upload-then-attach: `<FileUploadField purpose="CatProfilePhoto">` sends
 * the bytes to `POST /api/staff/files` (type/size pre-check, scan and refusals are the uploader's,
 * HO-FIL-002) and hands back a server-issued file id; "Add photo" then posts only
 * `{ fileId, isPrimary }` to `POST /api/cats/{id}/photos`. Photos render from the signed `url` in
 * the cat profile read (never cached past `urlExpiresOn`; `FileThumb` re-reads once on failure).
 */
export default function PhotosPage({ params }: { params: Promise<{ catId: string }> }) {
  const { catId } = use(params);
  const catQuery = useCat(catId);
  const addPhoto = useAddCatPhoto(catId);
  const [uploaded, setUploaded] = useState<StoredFileViewModel | null>(null);
  const [serverError, setServerError] = useState<string | undefined>();

  const refetch = catQuery.refetch;
  const refreshPhoto = useCallback(
    (photoId: string) => async () => {
      const result = await refetch();
      const fresh = result.data?.photos.find((photo) => photo.id === photoId);
      return fresh ? toFileViewModel(fresh) : null;
    },
    [refetch],
  );

  if (!catQuery.data) return null;
  const cat = catQuery.data;
  const isDeceased = cat.lifeStatus === 'Deceased';
  const atLimit = cat.photos.length >= CAT_PHOTO_RULES.maxPhotosPerCat;

  async function handleAttach() {
    if (!uploaded) return;
    setServerError(undefined);
    try {
      await addPhoto.mutateAsync({ fileId: uploaded.fileId, isPrimary: cat.photos.length === 0 });
      toast({ title: 'Photo added' });
      setUploaded(null);
    } catch (error) {
      setServerError(describeApiError(error));
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <h2 className="text-lg font-bold text-ink">Photos</h2>
      {isDeceased && <DeceasedBanner deceasedOn={cat.deceasedOn} deceasedNote={cat.deceasedNote} />}

      {cat.photos.length === 0 ? (
        <p className="text-sm text-ink-muted">No photos on file yet.</p>
      ) : (
        <ul className="grid grid-cols-2 gap-4 sm:grid-cols-4">
          {cat.photos.map((photo) => (
            <li
              key={photo.id}
              className="flex min-w-0 flex-col items-center gap-2 rounded-[var(--radius-md)] border border-border bg-surface p-4 text-center"
            >
              <FileThumb
                file={toFileViewModel(photo)}
                alt={`Photo of ${cat.name}: ${photo.originalFileName}`}
                onRefresh={refreshPhoto(photo.id)}
              />
              <span className="max-w-full break-words text-xs font-semibold text-ink">
                {photo.originalFileName}
              </span>
              {photo.isPrimary && <span className="text-xs text-ink-muted">Primary</span>}
            </li>
          ))}
        </ul>
      )}

      {!isDeceased &&
        (atLimit ? (
          <p className="text-sm text-ink-muted">
            This cat already has the maximum of {CAT_PHOTO_RULES.maxPhotosPerCat} photos.
          </p>
        ) : (
          <div className="flex max-w-md flex-col gap-3">
            <FileUploadField
              id="new-photo"
              label="Photo"
              purpose="CatProfilePhoto"
              value={uploaded}
              onChange={(file) => {
                setUploaded(file);
                setServerError(undefined);
              }}
              disabled={addPhoto.isPending}
            />
            {serverError && (
              <p role="alert" className="text-sm font-semibold text-danger-ink">
                {serverError}
              </p>
            )}
            <div>
              <Button onClick={handleAttach} disabled={!uploaded || addPhoto.isPending}>
                {addPhoto.isPending ? 'Saving…' : 'Add photo'}
              </Button>
            </div>
          </div>
        ))}
    </div>
  );
}
