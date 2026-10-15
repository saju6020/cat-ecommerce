'use client';

import { AlertTriangle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { CustomerPicker, type PickedCustomer } from '@/components/appointments/CustomerPicker';
import { VisitCatHeader } from '@/components/visit/VisitCatHeader';
import type { StaffCustomerSummary } from '@/lib/api/staff-customers';
import { errorClass, inputClass, labelClass } from './styles';

export interface CustomerCatStepProps {
  customer: PickedCustomer | null;
  onSelectCustomer: (customer: StaffCustomerSummary) => void;
  onClearCustomer: () => void;
  onSearch: (term: string) => void;
  results: StaffCustomerSummary[] | undefined;
  isSearching: boolean;
  searchedTerm: string | null;
  /** Alive cats only. */
  cats: Array<{ id: string; name: string }>;
  catId: string;
  onCatChange: (catId: string) => void;
  catError?: string;
  /** The server's "already neutered" answer (offered as *Book anyway*). */
  neuteredMessage?: string | null;
  neuteredConfirmed: boolean;
  onBookAnyway: () => void;
}

/**
 * `SurgeryCustomerCatStep` — HO-SRG-002 §11.1 part 1. Customer search (HO-ACC-002's picker), the alive
 * cats, then the UI-7 cat header. An *already neutered* answer from the server is shown with its message
 * and offers *"Book anyway — the record may be wrong"*, which re-submits with
 * `confirmNeuterStatusDiscrepancy`. The browser never decides that the cat is neutered.
 */
export function SurgeryCustomerCatStep(props: CustomerCatStepProps) {
  const { customer, cats, catId } = props;
  return (
    <section aria-labelledby="srg-step-cat" className="flex flex-col gap-4">
      <h2 id="srg-step-cat" className="font-display text-lg font-semibold text-ink">
        1. Customer and cat
      </h2>
      <CustomerPicker
        selected={customer}
        onSelect={props.onSelectCustomer}
        onClear={props.onClearCustomer}
        onSearch={props.onSearch}
        results={props.results}
        isSearching={props.isSearching}
        searchedTerm={props.searchedTerm}
      />
      <div className="flex flex-col gap-1.5">
        <label htmlFor="surgery-cat" className={labelClass}>
          Cat
        </label>
        <select
          id="surgery-cat"
          className={inputClass}
          disabled={!customer}
          value={catId}
          aria-invalid={props.catError ? true : undefined}
          aria-describedby={props.catError ? 'surgery-cat-error' : undefined}
          onChange={(event) => props.onCatChange(event.target.value)}
        >
          <option value="">{customer ? 'Choose…' : 'Choose a customer first'}</option>
          {cats.map((cat) => (
            <option key={cat.id} value={cat.id}>
              {cat.name}
            </option>
          ))}
        </select>
        {props.catError && (
          <p id="surgery-cat-error" role="alert" className={errorClass}>
            {props.catError}
          </p>
        )}
      </div>
      {catId && <VisitCatHeader catId={catId} />}
      {props.neuteredMessage && !props.neuteredConfirmed && (
        <div
          role="alert"
          className="flex flex-col gap-2 rounded-[var(--radius-md)] border border-warning-ink/30 bg-warning-bg p-3 text-sm text-warning-ink"
        >
          <p className="flex items-start gap-2 font-semibold">
            <AlertTriangle size={16} className="mt-0.5 flex-none" aria-hidden="true" />
            {props.neuteredMessage}
          </p>
          <div>
            <Button variant="secondary" size="sm" onClick={props.onBookAnyway}>
              Book anyway — the record may be wrong
            </Button>
          </div>
        </div>
      )}
      {props.neuteredConfirmed && (
        <p className="text-sm font-semibold text-warning-ink" role="status">
          Booking even though the cat is recorded as neutered.
        </p>
      )}
    </section>
  );
}
