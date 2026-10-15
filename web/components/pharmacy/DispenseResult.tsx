import Link from 'next/link';
import { CheckCircle2, Info, Printer } from 'lucide-react';
import { buttonVariants } from '@/components/ui/button';
import type { DispenseResult as DispenseResultData } from '@/lib/api/pharmacy';

/**
 * `DispenseResult` — HO-PHM-002 §11.4. *"Dispensed — supply 2 of 3."* with **Print label** (primary) and
 * **Back to queue**. When the response lists unpriced lines a neutral notice says Billing has been told
 * (OQ-3: no price is shown here).
 */
export function DispenseResult({
  result,
  suppliesAuthorised,
}: {
  result: DispenseResultData;
  suppliesAuthorised: number;
}) {
  return (
    <section
      role="status"
      aria-label="Dispensed"
      className="flex flex-col gap-3 rounded-[var(--radius-md)] border border-accent bg-accent-soft p-4"
    >
      <p className="flex items-center gap-2 text-base font-semibold text-accent-ink">
        <CheckCircle2 size={18} aria-hidden="true" />
        Dispensed — supply {result.supplyNumber} of {suppliesAuthorised}.
      </p>
      {result.unpricedLines.length > 0 && (
        <p className="flex items-start gap-2 text-sm text-ink">
          <Info size={16} className="mt-0.5 flex-none" aria-hidden="true" />
          {result.unpricedLines.length === 1
            ? 'One item has no price set — Billing has been told.'
            : `${result.unpricedLines.length} items have no price set — Billing has been told.`}
        </p>
      )}
      <div className="flex flex-wrap gap-2">
        <Link
          href={`/staff/pharmacy/dispensings/${result.dispensingId}/label`}
          className={buttonVariants({ variant: 'primary', size: 'md' })}
        >
          <Printer size={14} aria-hidden="true" />
          Print label
        </Link>
        <Link
          href="/staff/pharmacy"
          className={buttonVariants({ variant: 'secondary', size: 'md' })}
        >
          Back to queue
        </Link>
        <Link
          href={`/staff/pharmacy/dispensings/${result.dispensingId}`}
          className={buttonVariants({ variant: 'ghost', size: 'md' })}
        >
          View this supply
        </Link>
      </div>
    </section>
  );
}
