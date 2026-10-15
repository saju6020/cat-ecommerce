'use client';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  useDialogReturnFocus,
} from '@/components/ui/dialog';
import { WeightEntryForm, type WeightEntryFormValues } from '@/components/cat/WeightEntryForm';

/**
 * `RecordWeightDialog` — HO-PHM-002 §11.2 (US-PHM-02 AC2). The stale-weight refusal's **Record weight**
 * opens HO-CAT-002's own weight form in a dialog (on `components/ui/dialog.tsx`); on save the composer
 * re-runs the safety check. The form already carries the unit beside the number.
 */
export function RecordWeightDialog({
  open,
  onOpenChange,
  catName,
  onRecord,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  catName: string;
  onRecord: (values: WeightEntryFormValues) => Promise<unknown>;
}) {
  const returnFocusRef = useDialogReturnFocus(open);
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent returnFocusRef={returnFocusRef}>
        <DialogHeader>
          <DialogTitle>Record {catName}’s weight</DialogTitle>
          <DialogDescription>
            The safety check runs again as soon as the weight is saved. What you have typed in the
            prescription stays where it is.
          </DialogDescription>
        </DialogHeader>
        <WeightEntryForm
          onSubmit={async (values) => {
            await onRecord(values);
            onOpenChange(false);
          }}
        />
      </DialogContent>
    </Dialog>
  );
}
