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
import { FormErrorAlert } from '@/components/boarding/shared/FormBits';
import { ApiValidationError } from '@/lib/api/client';
import { describeBoardingError, getViolationCode } from '@/lib/api/errors';
import { useJoinBoardingWaitlist } from '@/lib/hooks/useMyBoarding';
import { formatStayDates } from '@/lib/format/boarding';
import type { StaySearchParams } from '@/lib/api/types';

/**
 * `components/boarding/booking/WaitlistJoinDialog.tsx` — HO-BRD-002 §11.1, US-BRD-02 AC3. Dates and
 * cat count are prefilled; it says plainly that the waitlist holds nothing — *"bookings are first
 * come, first served"* (HO-BRD-001 ADR-235). `UnitsStillAvailable` means a unit has just freed up:
 * the dialog says so and asks the caller to refetch the results instead of joining.
 */
export function WaitlistJoinDialog({
  open,
  onOpenChange,
  search,
  onUnitsAvailable,
  onJoined,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  search: StaySearchParams;
  onUnitsAvailable: () => void;
  onJoined: () => void;
}) {
  const join = useJoinBoardingWaitlist();
  const [error, setError] = useState<string | null>(null);
  const returnFocusRef = useDialogReturnFocus(open);

  async function submit() {
    setError(null);
    try {
      await join.mutateAsync(search);
      onJoined();
      onOpenChange(false);
    } catch (e) {
      if (e instanceof ApiValidationError && getViolationCode(e) === 'UnitsStillAvailable') {
        onUnitsAvailable();
        onOpenChange(false);
        return;
      }
      setError(describeBoardingError(e));
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent hideClose returnFocusRef={returnFocusRef}>
        <DialogHeader>
          <DialogTitle>Join the waitlist</DialogTitle>
          <DialogDescription>
            {formatStayDates(search.arrivalOn, search.departureOn)} for {search.catCount} cat
            {search.catCount === 1 ? '' : 's'}. We’ll message you if a unit frees up — bookings are
            first come, first served, so nothing is held for you.
          </DialogDescription>
        </DialogHeader>
        <FormErrorAlert message={error} />
        <DialogFooter>
          <Button variant="secondary" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button
            onClick={() => void submit()}
            disabled={join.isPending}
            aria-busy={join.isPending}
          >
            {join.isPending ? 'Joining…' : 'Join the waitlist'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
