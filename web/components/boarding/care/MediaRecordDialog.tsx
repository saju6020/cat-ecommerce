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
import { FileUploader } from '@/components/files/FileUploader';
import { FormErrorAlert } from '@/components/boarding/shared/FormBits';
import { describeBoardingError } from '@/lib/api/errors';
import { useRecordStayMedia } from '@/lib/hooks/useBoardingCare';
import { todayInZone } from '@/lib/format/time';

/** `components/boarding/care/MediaRecordDialog.tsx` — HO-BRD-002 §11.7: a diary photo uploaded through
 *  HO-FIL-002's uploader (purpose `BoardingMedia`), with a caption and the date it shows on. The upload is an
 *  extension of the owner's 2026-10-14 uploader decision (belongings) and is recorded in HO-BRD-002 §1 Amended. */
export function MediaRecordDialog({
  open,
  onOpenChange,
  stayId,
  catId,
  catName,
  zone,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  stayId: string;
  catId: string | null;
  catName: string;
  zone: string;
}) {
  const record = useRecordStayMedia(stayId);
  const [caption, setCaption] = useState('');
  const [date, setDate] = useState(todayInZone(zone));
  const [file, setFile] = useState<{ fileId: string; name: string } | null>(null);
  const [error, setError] = useState<string | null>(null);
  const returnFocusRef = useDialogReturnFocus(open);

  async function submit() {
    if (!file) {
      setError('Add a photo first.');
      return;
    }
    setError(null);
    try {
      await record.mutateAsync({
        fileId: file.fileId,
        catId,
        incidentId: null,
        caption: caption.trim() || null,
        capturedOnLocalDate: date,
      });
      setCaption('');
      setFile(null);
      onOpenChange(false);
    } catch (e) {
      setError(describeBoardingError(e));
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent hideClose returnFocusRef={returnFocusRef}>
        <DialogHeader>
          <DialogTitle>Add a diary photo for {catName}</DialogTitle>
          <DialogDescription>
            It appears in the owner’s diary on the date you choose.
          </DialogDescription>
        </DialogHeader>
        <div className="flex flex-col gap-3">
          <FormFieldRow
            id="media-caption"
            label="Caption"
            value={caption}
            onChange={(e) => setCaption(e.target.value)}
          />
          <FormFieldRow
            id="media-date"
            label="Date"
            type="date"
            value={date}
            onChange={(e) => setDate(e.target.value)}
          />
          <FileUploader
            id="media-photo"
            label="Photo"
            purpose="BoardingMedia"
            audience="staff"
            onUploaded={(uploaded) =>
              setFile({ fileId: uploaded.fileId, name: uploaded.originalFileName })
            }
          />
          {file && <p className="text-sm text-ink-muted">Ready to add: {file.name}</p>}
          <FormErrorAlert message={error} />
        </div>
        <DialogFooter>
          <Button variant="secondary" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button onClick={() => void submit()} disabled={record.isPending}>
            {record.isPending ? 'Saving…' : 'Add to the diary'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
