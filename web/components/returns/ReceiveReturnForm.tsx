'use client';

import { useState } from 'react';
import { Section } from '@/components/billing/PageHeader';
import { Button } from '@/components/ui/button';
import { ApiValidationError } from '@/lib/api/client';
import { describeOrderingError, parseLineProperty } from '@/lib/api/errors';
import type { StaffReturnDetail } from '@/lib/api/staff-returns';
import { useReceiveReturn } from '@/lib/hooks/useStaffReturns';

/**
 * `components/returns/ReceiveReturnForm.tsx` — HO-ORD-002 §11.10, US-ORD-14 AC1. Record what actually
 * came back, per line, up to the approved quantity (the server enforces the limit and its message is
 * shown on the line). Received goods go to the branch's quarantine location — a received return stays
 * *Received*; restocking or writing off is Inventory's screen (HO-ORD-001 gate fix 3).
 */
export function ReceiveReturnForm({ ret }: { ret: StaffReturnDetail }) {
  const receive = useReceiveReturn(ret.id);
  const [quantities, setQuantities] = useState<Record<string, string>>({});
  const [lineErrors, setLineErrors] = useState<Record<string, string>>({});
  const [rootError, setRootError] = useState<string | null>(null);
  const valueOf = (id: string, approved: number) => quantities[id] ?? String(approved);

  function submit(event: React.FormEvent) {
    event.preventDefault();
    setLineErrors({});
    setRootError(null);
    receive.mutate(
      ret.lines.map((l) => ({ orderLineId: l.orderLineId, quantityReceived: Number.parseInt(valueOf(l.orderLineId, l.quantity), 10) || 0 })),
      {
        onError: (error) => {
          if (error instanceof ApiValidationError) {
            const perLine: Record<string, string> = {};
            const other: string[] = [];
            for (const violation of error.response.errors) {
              const parsed = parseLineProperty(violation.propertyName);
              const target = parsed ? ret.lines[parsed.index]?.orderLineId : undefined;
              if (target) perLine[target] = violation.errorMessage;
              else other.push(violation.errorMessage);
            }
            setLineErrors(perLine);
            setRootError(other.length > 0 ? other.join(' ') : null);
          } else {
            setRootError(describeOrderingError(error));
          }
        },
      },
    );
  }

  return (
    <Section title="Receive the goods" description="Received items go to quarantine until Inventory decides what happens to them.">
      <form onSubmit={submit} noValidate className="flex flex-col gap-3">
        {rootError && <p role="alert" className="rounded-[var(--radius-sm)] bg-danger-bg p-3 text-sm font-semibold text-danger-ink">{rootError}</p>}
        <ul aria-label="Lines to receive" className="m-0 flex list-none flex-col gap-3 p-0">
          {ret.lines.map((line) => {
            const name = `${line.productName} ${line.variantLabel}`.trim();
            const id = `recv-${line.orderLineId}`;
            return (
              <li key={line.orderLineId} className="flex flex-wrap items-center justify-between gap-3 rounded-[var(--radius-sm)] border border-border p-3">
                <div className="min-w-0 flex-1 basis-48">
                  <label htmlFor={id} className="text-sm font-semibold [overflow-wrap:anywhere]">Received: {name}</label>
                  <p className="text-xs text-ink-muted">Approved quantity: {line.quantity}</p>
                  {lineErrors[line.orderLineId] && <p role="alert" className="text-xs font-semibold text-danger-ink">{lineErrors[line.orderLineId]}</p>}
                </div>
                <input
                  id={id}
                  type="number"
                  min={0}
                  max={line.quantity}
                  inputMode="numeric"
                  value={valueOf(line.orderLineId, line.quantity)}
                  onChange={(e) => setQuantities((cur) => ({ ...cur, [line.orderLineId]: e.target.value }))}
                  aria-invalid={!!lineErrors[line.orderLineId]}
                  className="w-24 rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
                />
              </li>
            );
          })}
        </ul>
        <div>
          <Button type="submit" disabled={receive.isPending} aria-busy={receive.isPending}>
            {receive.isPending ? 'Recording…' : 'Record receipt'}
          </Button>
        </div>
      </form>
    </Section>
  );
}
