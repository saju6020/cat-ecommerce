'use client';

import { Button } from '@/components/ui/button';
import type { InvoiceSummary } from '@/lib/api/billing-staff';
import { formatDateOnly } from '@/lib/format/time';
import { Amount } from './Amount';

/**
 * `components/billing/AllocationRows.tsx` — HO-BIL-002 §11.4 *apply to*. One row per invoice with
 * something outstanding: tick it, enter the amount to put towards it. The state is **keyed by invoice
 * id** and the rows come from the live invoice list, so a refetch that reorders or changes the list can
 * never make a row show one invoice and submit another. **Pay the rest** copies the invoice's outstanding
 * **as the server reported it** (a string) — the only convenience here, no arithmetic. The server caps
 * and validates the allocations; its message appears at the top of the form.
 */
export function AllocationRows({
  invoices,
  alloc,
  rowErrors,
  onChange,
  onPayTheRest,
}: {
  invoices: InvoiceSummary[];
  alloc: Record<string, { selected: boolean; amount: string }>;
  rowErrors: Record<string, string>;
  onChange: (invoiceId: string, patch: Partial<{ selected: boolean; amount: string }>) => void;
  onPayTheRest: (invoiceId: string, outstanding: string) => void;
}) {
  if (invoices.length === 0) {
    return (
      <p className="text-sm text-ink-muted">
        This customer has no invoice with something outstanding. The payment will be kept as credit.
      </p>
    );
  }
  return (
    <fieldset className="flex min-w-0 flex-col gap-2 border-0 p-0">
      <legend className="mb-1 text-sm font-semibold text-ink">Apply to</legend>
      <ul className="flex flex-col gap-2">
        {invoices.map((invoice) => {
          const row = alloc[invoice.invoiceId];
          return (
            <li key={invoice.invoiceId} className="flex min-w-0 flex-col gap-2 rounded-[var(--radius-sm)] border border-border p-3">
              <label className="flex flex-wrap items-center gap-x-3 gap-y-1 text-sm">
                <input
                  type="checkbox"
                  className="h-4 w-4"
                  checked={!!row?.selected}
                  onChange={(event) => onChange(invoice.invoiceId, { selected: event.target.checked })}
                />
                <span className="font-semibold text-ink">{invoice.number}</span>
                <span className="text-ink-muted">
                  due {invoice.dueOn ? formatDateOnly(invoice.dueOn) : '—'} · outstanding <Amount money={invoice.outstanding} />
                </span>
              </label>
              {row?.selected && (
                <div className="flex flex-wrap items-end gap-2">
                  <div className="flex min-w-0 flex-col gap-1">
                    <label htmlFor={`alloc-amount-${invoice.invoiceId}`} className="text-xs font-semibold text-ink">
                      Amount for {invoice.number} ({invoice.outstanding.currencyCode})
                    </label>
                    <input
                      id={`alloc-amount-${invoice.invoiceId}`}
                      type="text"
                      inputMode="decimal"
                      autoComplete="off"
                      spellCheck={false}
                      aria-invalid={!!rowErrors[invoice.invoiceId]}
                      value={row.amount}
                      onChange={(event) => onChange(invoice.invoiceId, { amount: event.target.value })}
                      className="w-40 rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
                    />
                  </div>
                  <Button variant="secondary" size="sm" onClick={() => onPayTheRest(invoice.invoiceId, invoice.outstanding.amount)}>
                    Pay the rest<span className="sr-only"> of {invoice.number}</span>
                  </Button>
                </div>
              )}
              {rowErrors[invoice.invoiceId] && (
                <p role="alert" className="text-xs font-semibold text-danger-ink">
                  {rowErrors[invoice.invoiceId]}
                </p>
              )}
            </li>
          );
        })}
      </ul>
    </fieldset>
  );
}
