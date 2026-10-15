import Link from 'next/link';
import { TriangleAlert } from 'lucide-react';
import { billingViolationCodes, describeBillingError } from '@/lib/api/errors';

/**
 * `components/billing/ComposerAlert.tsx` — HO-BIL-002 §11.2 *Errors*. The composer's refusals in
 * plain language (the server's message is preferred, it names the tax class or the amount), each with
 * the next step: `TaxRateNotConfigured` → a link to the tax classes (HO-ADM-002, administrators),
 * `BillingProfileMissing` → a link to the billing profile. `errorCode` only picks the link; it is
 * never shown.
 */
export function ComposerAlert({ error, notice }: { error?: unknown; notice?: string | null }) {
  if (!error && !notice) return null;
  const codes = error ? billingViolationCodes(error) : [];
  const message = notice ?? (error ? describeBillingError(error) : '');
  return (
    <div
      role="alert"
      className="flex items-start gap-3 rounded-[var(--radius-md)] border border-border bg-danger-bg p-3 text-sm text-danger-ink"
    >
      <TriangleAlert size={16} className="mt-0.5 flex-none" aria-hidden="true" />
      <div className="flex flex-col gap-1">
        <p className="font-semibold">{message}</p>
        {codes.includes('TaxRateNotConfigured') && (
          <p>
            An administrator can set the rate under{' '}
            <Link href="/staff/admin/tax-classes" className="font-semibold underline">
              Tax classes
            </Link>
            .
          </p>
        )}
        {codes.includes('BillingProfileMissing') && (
          <p>
            Set the branch’s legal details under{' '}
            <Link href="/staff/admin/billing-profile" className="font-semibold underline">
              Billing profile
            </Link>
            , then issue again.
          </p>
        )}
      </div>
    </div>
  );
}
