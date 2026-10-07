'use client';

import { use, useState } from 'react';
import { Image as ImageIcon } from 'lucide-react';
import { PhotoField } from '@/components/cat/PhotoField';
import { DeceasedBanner } from '@/components/cat/DeceasedBanner';
import { Button } from '@/components/ui/button';
import { useCat } from '@/lib/hooks/useCat';
import { useAddCatPhoto } from '@/lib/hooks/useCatMutations';
import { toast } from '@/components/ui/use-toast';
import { describeApiError } from '@/lib/api/errors';

/**
 * Photo references — US-CAT-01 AC3 (HO-CAT-002 §8.3, §11.6). No binary upload — see
 * `components/cat/PhotoField.tsx`'s doc comment. A validated file becomes a dev-only storage key
 * plus the derived metadata the real API takes.
 */
export default function PhotosPage({ params }: { params: Promise<{ catId: string }> }) {
  const { catId } = use(params);
  const catQuery = useCat(catId);
  const addPhoto = useAddCatPhoto(catId);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [serverError, setServerError] = useState<string | undefined>();

  if (!catQuery.data) return null;
  const cat = catQuery.data;
  const isDeceased = cat.lifeStatus === 'Deceased';

  async function handleUpload() {
    if (!selectedFile) return;
    setServerError(undefined);
    try {
      await addPhoto.mutateAsync({
        storageKey: `dev/${crypto.randomUUID()}-${selectedFile.name}`,
        contentType: selectedFile.type,
        sizeInBytes: selectedFile.size,
        fileName: selectedFile.name,
        isPrimary: cat.photos.length === 0,
      });
      toast({ title: 'Photo reference added' });
      setSelectedFile(null);
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
              className="flex flex-col items-center gap-2 rounded-[var(--radius-md)] border border-border bg-surface p-4 text-center"
            >
              <ImageIcon size={28} className="text-ink-faint" aria-hidden="true" />
              <span className="text-xs font-semibold text-ink">{photo.fileName}</span>
              {photo.isPrimary && <span className="text-xs text-ink-muted">Primary</span>}
            </li>
          ))}
        </ul>
      )}

      {!isDeceased && (
        <div className="flex max-w-md flex-col gap-3">
          <PhotoField id="new-photo" onFileValidated={setSelectedFile} serverError={serverError} />
          <div>
            <Button onClick={handleUpload} disabled={!selectedFile || addPhoto.isPending}>
              {addPhoto.isPending ? 'Saving…' : 'Add photo'}
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
