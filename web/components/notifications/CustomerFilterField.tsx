'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import type { StaffCustomerSummary } from '@/lib/api/staff-customers';

const INPUT_CLASS =
  'min-w-0 rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink';

/**
 * `components/notifications/CustomerFilterField.tsx` — HO-NTF-002 §11.5. The log's "customer"
 * filter, built on HO-ACC-002's customer lookup (mobile number or email -> matches): the person
 * types a mobile number or email address, picks a match, and the filter stores that customer's id
 * (`recipientId`). A deep link from the customer page arrives with `recipientId` already set, shown
 * as `selectedLabel` with a **Clear** button. Takes the lookup as a prop; the page wires the hook.
 */
export function CustomerFilterField({
  selectedId,
  selectedLabel,
  onSelect,
  onClear,
  onSearch,
}: {
  selectedId: string;
  selectedLabel: string | null;
  onSelect: (customer: StaffCustomerSummary) => void;
  onClear: () => void;
  onSearch: (term: string) => Promise<StaffCustomerSummary[]>;
}) {
  const [term, setTerm] = useState('');
  const [results, setResults] = useState<StaffCustomerSummary[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [searching, setSearching] = useState(false);

  async function search() {
    if (term.trim() === '') return;
    setSearching(true);
    setError(null);
    try {
      setResults(await onSearch(term));
    } catch {
      setResults(null);
      setError("We couldn't search customers just now. Please try again.");
    } finally {
      setSearching(false);
    }
  }

  if (selectedId) {
    return (
      <div className="flex min-w-0 flex-col gap-1.5">
        <span className="text-xs font-semibold text-ink-muted">Customer</span>
        <div className="flex items-center gap-2">
          <span className="rounded-full bg-accent-soft px-3 py-1.5 text-sm font-semibold text-accent-ink">
            {selectedLabel ?? 'Selected customer'}
          </span>
          <Button type="button" variant="ghost" size="sm" onClick={onClear}>
            Clear customer
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div className="flex min-w-0 flex-col gap-1.5">
      <label htmlFor="log-customer-search" className="text-xs font-semibold text-ink-muted">
        Customer (mobile number or email)
      </label>
      <div className="flex gap-2">
        <input
          id="log-customer-search"
          value={term}
          onChange={(event) => setTerm(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === 'Enter') {
              event.preventDefault();
              void search();
            }
          }}
          className={`${INPUT_CLASS} min-w-0 flex-1`}
        />
        <Button type="button" variant="secondary" size="sm" onClick={search} disabled={searching}>
          {searching ? 'Searching…' : 'Find'}
        </Button>
      </div>
      {error && (
        <p role="alert" className="text-xs font-semibold text-danger-ink">
          {error}
        </p>
      )}
      {results && results.length === 0 && (
        <p role="status" className="text-xs text-ink-muted">
          No customer matches that.
        </p>
      )}
      {results && results.length > 0 && (
        <ul className="m-0 flex list-none flex-col gap-1 p-0">
          {results.map((customer) => (
            <li key={customer.id}>
              <button
                type="button"
                onClick={() => {
                  onSelect(customer);
                  setResults(null);
                  setTerm('');
                }}
                className="w-full rounded-[var(--radius-sm)] border border-border px-3 py-1.5 text-left text-sm text-ink hover:bg-surface-sunken"
              >
                {customer.fullName}
                <span className="text-xs text-ink-muted">
                  {' '}
                  · {customer.mobileNumber ?? customer.emailAddress ?? 'no contact details'}
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
