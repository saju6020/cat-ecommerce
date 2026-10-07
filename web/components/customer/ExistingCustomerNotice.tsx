import Link from 'next/link';
import { Badge } from '@/components/ui/badge';
import type { StaffCustomerSummary } from '@/lib/api/staff-customers';

/**
 * `components/customer/ExistingCustomerNotice.tsx` — US-ACC-10, HO-ACC-001 §1 Amended
 * (SECURITY gate). `mobileMatchedOn` distinguishes a genuine primary-number match from an
 * unverified additional-number claim — staff are authorised to see both, but must be able to tell
 * them apart before treating a hit as the same person.
 */
export function ExistingCustomerNotice({ results }: { results: StaffCustomerSummary[] }) {
  if (results.length === 0) {
    return (
      <p role="status" className="text-sm text-ink-muted">
        No existing record was found for that number — try again, or check the number entered.
      </p>
    );
  }

  return (
    <div
      role="status"
      className="flex flex-col gap-2 rounded-[var(--radius-md)] border border-border bg-surface p-4"
    >
      <p className="text-sm font-semibold text-ink">This mobile number is already registered.</p>
      <ul className="flex flex-col gap-2">
        {results.map((result) => (
          <li key={result.id} className="flex items-center gap-2">
            <Link
              href={`/staff/customers/${result.id}`}
              className="font-semibold text-accent-ink no-underline hover:underline"
            >
              {result.fullName}
            </Link>
            {result.mobileMatchedOn === 'Additional' && (
              <Badge variant="warning">Additional number only</Badge>
            )}
          </li>
        ))}
      </ul>
    </div>
  );
}
