'use client';

import Link from 'next/link';
import { useState } from 'react';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { PageHeader } from '@/components/billing/PageHeader';
import { QueryBoundary } from '@/components/billing/QueryBoundary';
import { Button, buttonVariants } from '@/components/ui/button';
import { describeOrderingError, parseLineProperty } from '@/lib/api/errors';
import { ApiValidationError } from '@/lib/api/client';
import type {
  CustomerOrderDetail,
  ItemCondition,
  ReturnLineInput,
  ReturnReason,
} from '@/lib/api/my-orders';
import { itemConditionLabel, returnReasonLabel } from '@/lib/format/orders';
import { useMyOrder, useRequestMyReturn } from '@/lib/hooks/useMyOrders';

const REASONS: ReturnReason[] = ['WrongItem', 'Damaged', 'QualityDefect', 'ChangedMind', 'Recalled', 'Other'];
const CONDITIONS: ItemCondition[] = ['Sealed', 'Opened', 'Used'];
const NOTE_MAX = 500;

interface LineDraft {
  selected: boolean;
  quantity: number;
  reason: ReturnReason;
  condition: ItemCondition;
  note: string;
}

const POLICY: Record<string, string> = {
  Food: "Opened food can only be returned if there's a quality problem or a recall.",
  Litter: "Used litter can't be returned.",
};

function Form({ order }: { order: CustomerOrderDetail }) {
  const request = useRequestMyReturn(order.id);
  const returnable = order.returnableLines
    .map((line) => ({ returnable: line, line: order.lines.find((l) => l.lineId === line.lineId) }))
    .filter((entry): entry is { returnable: (typeof order.returnableLines)[number]; line: (typeof order.lines)[number] } => !!entry.line);
  const [drafts, setDrafts] = useState<Record<string, LineDraft>>({});
  const [lineErrors, setLineErrors] = useState<Record<string, string>>({});
  const [rootError, setRootError] = useState<string | null>(null);
  const [done, setDone] = useState(false);

  const draftOf = (lineId: string): LineDraft =>
    drafts[lineId] ?? { selected: false, quantity: 1, reason: 'ChangedMind', condition: 'Sealed', note: '' };
  const patch = (lineId: string, change: Partial<LineDraft>) =>
    setDrafts((current) => ({ ...current, [lineId]: { ...draftOf(lineId), ...change } }));
  const selected = returnable.filter(({ line }) => draftOf(line.lineId).selected);
  const dirty = Object.keys(drafts).length > 0;
  const notReturnable = order.lines.filter((l) => !returnable.some((r) => r.line.lineId === l.lineId));

  function submit(event: React.FormEvent) {
    event.preventDefault();
    setLineErrors({});
    setRootError(null);
    const lines: ReturnLineInput[] = selected.map(({ line }) => {
      const draft = draftOf(line.lineId);
      return {
        orderLineId: line.lineId,
        quantity: draft.quantity,
        reason: draft.reason,
        condition: draft.condition,
        customerNote: draft.note.trim() || undefined,
      };
    });
    request.mutate(lines, {
      onSuccess: () => setDone(true),
      onError: (error) => {
        if (error instanceof ApiValidationError) {
          const perLine: Record<string, string> = {};
          const other: string[] = [];
          for (const violation of error.response.errors) {
            const parsed = parseLineProperty(violation.propertyName);
            const target = parsed ? selected[parsed.index]?.line.lineId : undefined;
            const text = violation.errorMessage || describeOrderingError(error);
            if (target) perLine[target] = perLine[target] ? `${perLine[target]} ${text}` : text;
            else other.push(text);
          }
          setLineErrors(perLine);
          setRootError(other.length > 0 ? other.join(' ') : null);
        } else {
          setRootError(describeOrderingError(error));
        }
      },
    });
  }

  if (done) {
    return (
      <div className="flex flex-col gap-4">
        <PageHeader title="Return requested" />
        <p role="status" className="rounded-[var(--radius-md)] border border-accent/30 bg-accent-soft p-4 text-sm font-semibold text-accent-ink">
          Thanks — we&rsquo;ve received your request. Our team will review it and tell you what happens next.
        </p>
        <p>
          <Link href={`/portal/orders/${order.id}`} className={buttonVariants({ variant: 'secondary' })}>
            Back to order {order.orderNumber}
          </Link>
        </p>
      </div>
    );
  }

  return (
    <form onSubmit={submit} noValidate className="flex flex-col gap-5">
      <UnsavedChangesGuard when={dirty && !request.isPending} />
      <PageHeader
        back={{ href: `/portal/orders/${order.id}`, label: `Order ${order.orderNumber}` }}
        title="Request a return"
        description="Choose the items you want to send back and tell us why."
      />
      {rootError && (
        <p role="alert" className="rounded-[var(--radius-sm)] bg-danger-bg p-3 text-sm font-semibold text-danger-ink">
          {rootError}
        </p>
      )}
      {returnable.map(({ returnable: info, line }) => {
        const draft = draftOf(line.lineId);
        const error = lineErrors[line.lineId];
        const name = `${line.productName} ${line.variantLabel}`.trim();
        return (
          <fieldset
            key={line.lineId}
            className="m-0 flex min-w-0 flex-col gap-3 rounded-[var(--radius-md)] border border-border bg-surface p-4"
          >
            <legend className="px-1 font-semibold text-ink">{name}</legend>
            <label className="flex items-center gap-2 text-sm font-semibold">
              <input
                type="checkbox"
                className="h-4 w-4 accent-[var(--accent)]"
                checked={draft.selected}
                onChange={(event) => patch(line.lineId, { selected: event.target.checked })}
              />
              {`Return this item (bought ${line.quantity})`}
            </label>
            {draft.selected && (
              <div className="flex flex-col gap-3 text-sm">
                <div className="flex flex-col gap-1">
                  <label htmlFor={`qty-${line.lineId}`} className="font-semibold">
                    How many?
                  </label>
                  <input
                    id={`qty-${line.lineId}`}
                    type="number"
                    min={1}
                    max={info.maxQuantity}
                    value={draft.quantity}
                    onChange={(event) => {
                      const next = event.target.valueAsNumber;
                      if (Number.isInteger(next) && next >= 1) patch(line.lineId, { quantity: Math.min(next, info.maxQuantity) });
                    }}
                    className="h-10 w-24 rounded-[var(--radius-sm)] border border-border-strong bg-surface px-2 tabular-nums"
                  />
                  <span className="text-xs text-ink-muted">{`Up to ${info.maxQuantity}.`}</span>
                </div>
                <fieldset className="m-0 border-0 p-0">
                  <legend className="mb-1 font-semibold">Why are you returning it?</legend>
                  {REASONS.map((reason) => (
                    <label key={reason} className="flex items-center gap-2 py-0.5">
                      <input
                        type="radio"
                        name={`reason-${line.lineId}`}
                        className="h-4 w-4 accent-[var(--accent)]"
                        checked={draft.reason === reason}
                        onChange={() => patch(line.lineId, { reason })}
                      />
                      {returnReasonLabel(reason)}
                    </label>
                  ))}
                </fieldset>
                <fieldset className="m-0 border-0 p-0">
                  <legend className="mb-1 font-semibold">What condition is it in?</legend>
                  {CONDITIONS.map((condition) => (
                    <label key={condition} className="flex items-center gap-2 py-0.5">
                      <input
                        type="radio"
                        name={`condition-${line.lineId}`}
                        className="h-4 w-4 accent-[var(--accent)]"
                        checked={draft.condition === condition}
                        onChange={() => patch(line.lineId, { condition })}
                      />
                      {itemConditionLabel(condition)}
                    </label>
                  ))}
                </fieldset>
                {POLICY[info.returnClass] && (
                  <p className="text-xs text-ink-muted">{POLICY[info.returnClass]}</p>
                )}
                <div className="flex flex-col gap-1">
                  <label htmlFor={`note-${line.lineId}`} className="font-semibold">
                    Anything else we should know? (optional)
                  </label>
                  <textarea
                    id={`note-${line.lineId}`}
                    rows={2}
                    maxLength={NOTE_MAX}
                    value={draft.note}
                    onChange={(event) => patch(line.lineId, { note: event.target.value })}
                    className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2"
                  />
                </div>
              </div>
            )}
            {error && (
              <p role="alert" className="text-sm font-semibold text-danger-ink">
                {error}
              </p>
            )}
          </fieldset>
        );
      })}
      {notReturnable.length > 0 && (
        <p className="text-sm text-ink-muted">
          {`Not returnable online: ${notReturnable.map((l) => l.productName).join(', ')}. Contact the shop if you need help with these.`}
        </p>
      )}
      <div className="flex flex-wrap items-center gap-3">
        <Button type="submit" disabled={selected.length === 0 || request.isPending} aria-busy={request.isPending} aria-describedby={selected.length === 0 ? 'return-why' : undefined}>
          {request.isPending ? 'Sending…' : 'Send return request'}
        </Button>
        {selected.length === 0 && (
          <p id="return-why" className="text-sm text-ink-muted">
            Choose at least one item to return.
          </p>
        )}
      </div>
    </form>
  );
}

/** `/portal/orders/[orderId]/return` — HO-ORD-002 §11.7 `ReturnRequestForm`. */
export function ReturnRequestScreen({ orderId }: { orderId: string }) {
  const query = useMyOrder(orderId);
  return (
    <QueryBoundary query={query} notFoundTitle="We can't find that order" loadingLabel="Loading your order…">
      {(order) =>
        order.canRequestReturn ? (
          <Form order={order} />
        ) : (
          <div className="flex flex-col gap-4">
            <PageHeader back={{ href: `/portal/orders/${order.id}`, label: `Order ${order.orderNumber}` }} title="Request a return" />
            <p role="status" className="rounded-[var(--radius-md)] border border-border bg-surface p-4 text-sm text-ink">
              A return can&rsquo;t be requested for this order — it may not be delivered yet, or the return window
              has closed.
            </p>
          </div>
        )
      }
    </QueryBoundary>
  );
}
