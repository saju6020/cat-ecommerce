'use client';

import { useState } from 'react';
import Link from 'next/link';
import { Search } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { useCustomerLookup } from '@/lib/hooks/useCustomerLookup';
import { describeUnexpectedError } from '@/lib/api/errors';

/**
 * `components/billing/BillingCustomerSearch.tsx` — HO-BIL-002 §11.1. Billing is customer-first: find
 * the customer by mobile number or email (HO-ACC-002's lookup, `GET /api/staff/customers`) and open
 * their billing account. Type, press Enter (or Search), Tab to a result — keyboard only.
 */
export function BillingCustomerSearch({
  hrefFor = (customerId) => `/staff/billing/customers/${customerId}`,
  label = 'Find a customer by mobile number or email',
}: {
  hrefFor?: (customerId: string) => string;
  label?: string;
}) {
  const [term, setTerm] = useState('');
  const lookup = useCustomerLookup();
  const results = lookup.data;

  return (
    <div className="flex min-w-0 flex-col gap-3">
      <form
        role="search"
        aria-label="Customer search"
        className="flex flex-wrap items-end gap-2"
        onSubmit={(event) => {
          event.preventDefault();
          if (term.trim()) lookup.mutate(term);
        }}
      >
        <div className="flex min-w-0 flex-1 flex-col gap-1.5">
          <label htmlFor="billing-customer-search" className="text-sm font-semibold text-ink">
            {label}
          </label>
          <input
            id="billing-customer-search"
            type="search"
            value={term}
            onChange={(event) => setTerm(event.target.value)}
            className="w-full min-w-0 rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          />
        </div>
        <Button type="submit" variant="secondary" disabled={lookup.isPending || !term.trim()}>
          <Search size={14} aria-hidden="true" />
          {lookup.isPending ? 'Searching…' : 'Search'}
        </Button>
      </form>
      {lookup.isError && (
        <p role="alert" className="text-sm font-semibold text-danger-ink">
          {describeUnexpectedError(lookup.error)}
        </p>
      )}
      {results && results.length === 0 && (
        <p role="status" className="text-sm text-ink-muted">
          No customer matches that. Check the number or email and try again.
        </p>
      )}
      {results && results.length > 0 && (
        <ul aria-label="Matching customers" className="flex flex-col gap-2">
          {results.map((customer) => (
            <li key={customer.id}>
              <Link
                href={hrefFor(customer.id)}
                className="flex min-w-0 flex-col gap-0.5 rounded-[var(--radius-sm)] border border-border bg-surface p-3 text-sm no-underline hover:bg-surface-sunken"
              >
                <span className="font-semibold text-ink [overflow-wrap:anywhere]">{customer.fullName}</span>
                <span className="text-ink-muted [overflow-wrap:anywhere]">
                  {[customer.mobileNumber, customer.emailAddress].filter(Boolean).join(' · ')}
                </span>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
