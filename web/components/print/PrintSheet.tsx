'use client';

import type { ReactNode } from 'react';
import { Printer } from 'lucide-react';
import { PageHeader } from '@/components/billing/PageHeader';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';

/**
 * `components/print/PrintSheet.tsx` — HO-ORD-002 §8.4, ADR-FE-094. The shared shell of the three
 * printables (pick list, packing slip, receipt): the page chrome (back link, **Print**, layout switch)
 * sits OUTSIDE the sheet, the sheet itself is an ordinary accessible document, and the print stylesheet
 * hides everything but the sheet so no navigation reaches the paper. `size` is the paper: `A4` for the
 * pick list and packing slip, `80mm` for the receipt (the browser and OS driver do the printing, HW-1).
 * No PDF library, no server render.
 */
export type PrintSize = 'A4' | '80mm';

/** The print stylesheet — a plain static string (no user content), hiding all but `.print-sheet`. */
export function printSheetCss(size: PrintSize): string {
  const page = size === '80mm' ? '80mm auto' : 'A4';
  const margin = size === '80mm' ? '3mm' : '12mm';
  return `
@media print {
  @page { size: ${page}; margin: ${margin}; }
  body * { visibility: hidden !important; }
  .print-sheet, .print-sheet * { visibility: visible !important; }
  .print-sheet { position: absolute; left: 0; top: 0; width: 100% !important; max-width: none !important; box-shadow: none !important; border: 0 !important; }
}`;
}

export function PrintSheet({
  title,
  description,
  back,
  size,
  actions,
  controls,
  label,
  children,
}: {
  /** The page heading (outside the sheet). */
  title: string;
  description?: ReactNode;
  back: { href: string; label: string };
  size: PrintSize;
  /** Extra controls beside **Print** (a layout switch, a plain-text toggle). */
  actions?: ReactNode;
  /** Screen-only controls between the heading and the sheet (a chooser, a bulk action). */
  controls?: ReactNode;
  /** The sheet's accessible name. */
  label: string;
  children: ReactNode;
}) {
  return (
    <div className="flex flex-col gap-5">
      <style>{printSheetCss(size)}</style>
      <PageHeader
        title={title}
        description={description}
        back={back}
        actions={
          <>
            {actions}
            <Button onClick={() => window.print()}>
              <Printer size={14} aria-hidden="true" />
              Print
            </Button>
          </>
        }
      />
      {controls}
      <div className="rounded-[var(--radius-md)] border border-border bg-surface-sunken p-3 sm:p-4">
        <article
          aria-label={label}
          className={cn(
            'print-sheet mx-auto bg-surface p-4 text-ink sm:p-6',
            size === '80mm' ? 'w-[80mm] max-w-full text-[12px] leading-snug' : 'w-full max-w-[210mm] text-sm',
          )}
        >
          {children}
        </article>
      </div>
    </div>
  );
}
