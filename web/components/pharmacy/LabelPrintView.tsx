'use client';

import * as React from 'react';
import Link from 'next/link';
import { Printer } from 'lucide-react';
import { Button, buttonVariants } from '@/components/ui/button';
import type { Dispensing } from '@/lib/api/pharmacy';
import { MedicineLabel } from './MedicineLabel';

/**
 * `LabelPrintView` — HO-PHM-002 §11.4, ADR-FE-064. The dedicated print route's content: the label from
 * the dispensing's FROZEN snapshot, a **Print** button and **Back**. **Print** calls `window.print()`
 * and then records the print — ONE `POST …/label-prints` per click, never per render (re-rendering,
 * refetching or reloading this view posts nothing), so a reprint is counted. The toolbar is hidden
 * when printing; so is everything else but the label (the label's own stylesheet).
 */
export function LabelPrintView({
  dispensing,
  onPrinted,
}: {
  dispensing: Dispensing;
  /** Records one print. Called once per Print click, after the browser's print dialog. */
  onPrinted: () => Promise<unknown>;
}) {
  const [recordError, setRecordError] = React.useState(false);
  const [busy, setBusy] = React.useState(false);

  async function print() {
    setRecordError(false);
    window.print();
    setBusy(true);
    try {
      await onPrinted();
    } catch {
      setRecordError(true);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="no-print flex flex-wrap items-center gap-2 print:hidden">
        <Button onClick={() => void print()} disabled={busy} aria-busy={busy}>
          <Printer size={14} aria-hidden="true" />
          Print
        </Button>
        <Link
          href={`/staff/pharmacy/dispensings/${dispensing.id}`}
          className={buttonVariants({ variant: 'secondary', size: 'md' })}
        >
          Back
        </Link>
        <span className="text-sm text-ink-muted">
          Printed {dispensing.labelPrintCount} {dispensing.labelPrintCount === 1 ? 'time' : 'times'}{' '}
          so far.
        </span>
      </div>
      {recordError && (
        <p role="alert" className="print:hidden text-sm font-semibold text-danger-ink">
          The label was sent to the printer, but the print could not be recorded just now.
        </p>
      )}
      <MedicineLabel label={dispensing.label} />
    </div>
  );
}
