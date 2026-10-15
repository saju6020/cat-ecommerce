'use client';

import { Button } from '@/components/ui/button';
import { BUSINESS_LINES, type BusinessLine } from '@/lib/api/billing-staff';
import type { Money } from '@/lib/format/money';
import { formatDateOnly } from '@/lib/format/time';
import { Amount } from './Amount';

/**
 * `components/billing/ChargePicker.tsx` — HO-BIL-002 §11.2 (US-BIL-02, ORD-17, AC-16): the unbilled
 * charges of one customer **grouped by business line** — Clinic, Shop, Boarding — each with its own
 * *Select all in group*. A row the server has since put on another invoice is marked *"Already on
 * another invoice"*, is not selectable and is dropped from the selection. Nothing is totalled here:
 * the preview beside it is the server's.
 */
export interface PickerItem {
  chargeId: string;
  businessLine: BusinessLine;
  description: string;
  serviceDate?: string;
  quantity: string;
  unitPrice: Money;
  lineTotal: Money;
}

export function ChargePicker({
  items,
  selected,
  taken,
  onToggle,
  onToggleGroup,
  disabled,
}: {
  items: PickerItem[];
  selected: string[];
  taken: string[];
  onToggle: (chargeId: string) => void;
  onToggleGroup: (line: BusinessLine, select: boolean) => void;
  disabled?: boolean;
}) {
  return (
    <div className="flex flex-col gap-4">
      {BUSINESS_LINES.map((line) => {
        const group = items.filter((item) => item.businessLine === line);
        const selectable = group.filter((item) => !taken.includes(item.chargeId));
        const allSelected = selectable.length > 0 && selectable.every((i) => selected.includes(i.chargeId));
        const headingId = `picker-${line.toLowerCase()}`;
        return (
          <section key={line} aria-labelledby={headingId} className="flex flex-col gap-2">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <h3 id={headingId} className="text-sm font-semibold uppercase tracking-wide text-ink-muted">
                {line}
              </h3>
              {selectable.length > 0 && (
                <Button
                  variant="ghost"
                  size="sm"
                  disabled={disabled}
                  onClick={() => onToggleGroup(line, !allSelected)}
                >
                  {allSelected ? `Clear ${line}` : `Select all in ${line}`}
                </Button>
              )}
            </div>
            {group.length === 0 ? (
              <p className="text-sm text-ink-muted">No unbilled {line.toLowerCase()} charges.</p>
            ) : (
              <ul className="flex flex-col gap-2">
                {group.map((item) => {
                  const isTaken = taken.includes(item.chargeId);
                  const inputId = `charge-${item.chargeId}`;
                  return (
                    <li
                      key={item.chargeId}
                      className="flex min-w-0 items-start gap-3 rounded-[var(--radius-sm)] border border-border bg-surface p-3"
                    >
                      <input
                        id={inputId}
                        type="checkbox"
                        className="mt-1 h-4 w-4 flex-none"
                        checked={selected.includes(item.chargeId)}
                        disabled={disabled || isTaken}
                        onChange={() => onToggle(item.chargeId)}
                      />
                      <label htmlFor={inputId} className="min-w-0 flex-1 text-sm">
                        <span className="block font-semibold text-ink [overflow-wrap:anywhere]">
                          {item.description}
                        </span>
                        <span className="block text-ink-muted">
                          {item.serviceDate ? `${formatDateOnly(item.serviceDate)} · ` : ''}{item.quantity} ×{' '}
                          <Amount money={item.unitPrice} />
                        </span>
                        {isTaken && (
                          <span className="mt-1 block font-semibold text-danger-ink">
                            Already on another invoice
                          </span>
                        )}
                      </label>
                      <Amount money={item.lineTotal} className="flex-none text-sm font-semibold" />
                    </li>
                  );
                })}
              </ul>
            )}
          </section>
        );
      })}
    </div>
  );
}
