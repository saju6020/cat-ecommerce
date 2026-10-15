import { ShieldAlert } from 'lucide-react';
import type { MedicineLabel as MedicineLabelData } from '@/lib/api/pharmacy';
import { formatDay } from '@/lib/format/pharmacy';
import { cn } from '@/lib/utils';

/**
 * ⭐ `MedicineLabel` — HO-PHM-002 §11.4, ADR-FE-064 (US-PHM-05 AC1). Prints the FROZEN label snapshot
 * the dispensing stored — a reprint is the same label. Every field the acceptance criterion names is
 * here: clinic name and phone, cat, owner, the medicine line, the dose line (large), instructions,
 * quantity, dispensed date, **expiry**, prescription number, prescriber, the *Controlled drug* notice
 * when there is one, and the standard warnings. Text only (no HTML from the server).
 *
 * The size comes from `--label-width` / `--label-height` (default 62 × 100 mm portrait, OQ-1): a
 * different label stock is a one-line change to the two constants below. `@page` cannot read a CSS
 * variable in every browser, so the print stylesheet is generated from the same constants.
 */
export const LABEL_WIDTH = '62mm';
export const LABEL_HEIGHT = '100mm';

/** The print stylesheet (a plain static string built from the constants above, like the receipt's). */
export const labelPrintCss = `
:root { --label-width: ${LABEL_WIDTH}; --label-height: ${LABEL_HEIGHT}; }
@media print {
  @page { size: ${LABEL_WIDTH} ${LABEL_HEIGHT}; margin: 0; }
  body * { visibility: hidden !important; }
  .medicine-label, .medicine-label * { visibility: visible !important; }
  .medicine-label {
    position: absolute; left: 0; top: 0;
    width: var(--label-width) !important; height: var(--label-height) !important;
    max-width: none !important; border: 0 !important; box-shadow: none !important;
    page-break-inside: avoid; overflow: hidden;
  }
}`;

export function MedicineLabel({
  label,
  className,
}: {
  label: MedicineLabelData;
  className?: string;
}) {
  return (
    <article
      aria-label={`Medicine label for ${label.catName}`}
      style={{ width: 'var(--label-width, 62mm)', minHeight: 'var(--label-height, 100mm)' }}
      className={cn(
        'medicine-label flex max-w-full flex-col gap-1.5 border border-border-strong bg-white p-3 text-[11px] leading-snug text-black',
        className,
      )}
    >
      <style>{labelPrintCss}</style>
      <header className="border-b border-black pb-1.5 text-center">
        <p className="text-[12px] font-bold [overflow-wrap:anywhere]">{label.clinicName}</p>
        <p>{label.clinicPhone}</p>
        {label.clinicAddress && (
          <p className="text-[10px] [overflow-wrap:anywhere]">{label.clinicAddress}</p>
        )}
      </header>
      <p className="[overflow-wrap:anywhere]">
        <b>{label.catName}</b> — owner {label.ownerName}
      </p>
      <p className="font-semibold [overflow-wrap:anywhere]">{label.medicineLine}</p>
      <p
        data-clinical-value
        className="text-[15px] font-bold leading-tight [overflow-wrap:anywhere]"
      >
        {label.doseLine}
      </p>
      <p className="whitespace-pre-wrap [overflow-wrap:anywhere]">{label.instructions}</p>
      <p>
        Quantity: <b data-clinical-value>{label.quantityLine}</b>
      </p>
      <p>
        Dispensed: <b>{formatDay(label.dispensedOn)}</b>
        {label.expiresOn && (
          <>
            {' · '}Expires: <b>{formatDay(label.expiresOn)}</b>
          </>
        )}
      </p>
      <p>
        {label.prescriptionNumber} · Prescriber: {label.prescriberName}
      </p>
      {label.controlledNotice && (
        <p className="flex items-start gap-1 border border-black p-1 font-bold">
          <ShieldAlert size={12} className="mt-0.5 flex-none" aria-hidden="true" />
          <span>{label.controlledNotice}</span>
        </p>
      )}
      <p className="mt-auto text-[9px] [overflow-wrap:anywhere]">{label.standardWarnings}</p>
    </article>
  );
}
