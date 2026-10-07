'use client';

import { forwardRef, useEffect, useState } from 'react';
import { Search, UserPlus, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import type { StaffCustomerSummary } from '@/lib/api/staff-customers';

export interface PickedCustomer {
  id: string;
  fullName: string;
  mobileNumber: string | null;
  /** US-CLN-08 — shown inline, as text, the moment the customer is chosen. */
  noShowCount?: number | null;
}

/**
 * `components/appointments/CustomerPicker.tsx` — HO-APT-002 §11.3/§11.5: HO-ACC-002's customer
 * lookup (by mobile number or email — `GET /api/staff/customers`) as a keyboard-first control.
 * Type, press Enter to search, Tab to a result, Enter/Space to choose. Once chosen it shows the
 * customer with an **inline no-show badge** (*"2 no-shows"*, text) — US-CLN-08, "visible at
 * booking". `New customer` hands over to the caller's inline walk-in customer form.
 */
export const CustomerPicker = forwardRef<
  HTMLInputElement,
  {
    selected: PickedCustomer | null;
    onSelect: (customer: StaffCustomerSummary) => void;
    onClear: () => void;
    onSearch: (term: string) => void;
    results: StaffCustomerSummary[] | undefined;
    isSearching: boolean;
    searchedTerm: string | null;
    onNewCustomer?: () => void;
  }
>(function CustomerPicker(
  { selected, onSelect, onClear, onSearch, results, isSearching, searchedTerm, onNewCustomer },
  inputRef,
) {
  const [term, setTerm] = useState('');

  // A chosen customer clears the search box, so the next lookup starts empty.
  useEffect(() => {
    if (selected) setTerm('');
  }, [selected]);

  if (selected) {
    return (
      <div className="flex flex-wrap items-center justify-between gap-3 rounded-[var(--radius-md)] border border-border bg-surface-sunken p-3 text-sm">
        <div className="flex flex-wrap items-center gap-2">
          <span className="font-semibold text-ink">{selected.fullName}</span>
          {selected.mobileNumber && <span className="text-ink-muted">{selected.mobileNumber}</span>}
          {selected.noShowCount != null && selected.noShowCount > 0 && (
            <Badge variant="danger">
              {selected.noShowCount} {selected.noShowCount === 1 ? 'no-show' : 'no-shows'}
            </Badge>
          )}
        </div>
        <Button variant="ghost" size="sm" onClick={onClear}>
          <X size={14} aria-hidden="true" />
          Change customer
        </Button>
      </div>
    );
  }

  function search() {
    if (term.trim()) onSearch(term.trim());
  }

  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-col gap-1.5">
        <label htmlFor="customer-search" className="text-sm font-semibold text-ink">
          Find the customer by mobile number or email
        </label>
        <div className="flex gap-2">
          <input
            ref={inputRef}
            id="customer-search"
            type="search"
            autoComplete="off"
            value={term}
            onChange={(event) => setTerm(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === 'Enter') {
                event.preventDefault();
                search();
              }
            }}
            className="min-w-0 flex-1 rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          />
          <Button variant="secondary" onClick={search} disabled={isSearching || !term.trim()}>
            <Search size={15} aria-hidden="true" />
            {isSearching ? 'Searching…' : 'Search'}
          </Button>
        </div>
      </div>

      {searchedTerm && !isSearching && results && (
        <div role="status" className="flex flex-col gap-2 text-sm">
          {results.length === 0 ? (
            <p className="text-ink-muted">No customer found for “{searchedTerm}”.</p>
          ) : (
            <ul className="m-0 flex list-none flex-col gap-1.5 p-0">
              {results.map((customer) => (
                <li key={customer.id}>
                  <button
                    type="button"
                    onClick={() => onSelect(customer)}
                    className="flex w-full flex-wrap items-center gap-2 rounded-[var(--radius-sm)] border border-border bg-surface px-3 py-2 text-left hover:bg-surface-sunken focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
                  >
                    <span className="font-semibold text-ink">{customer.fullName}</span>
                    <span className="text-ink-muted">
                      {customer.mobileNumber ?? customer.emailAddress}
                    </span>
                    {customer.mobileMatchedOn === 'Additional' && (
                      <Badge variant="warning">Additional number only</Badge>
                    )}
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}

      {onNewCustomer && (
        <div>
          <Button variant="ghost" size="sm" onClick={onNewCustomer}>
            <UserPlus size={14} aria-hidden="true" />
            New customer
          </Button>
        </div>
      )}
    </div>
  );
});
