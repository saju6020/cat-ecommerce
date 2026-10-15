'use client';

import { useState } from 'react';
import { Section } from '@/components/billing/PageHeader';
import { OrderReasonDialog } from '@/components/orders/OrderReasonDialog';
import { Button } from '@/components/ui/button';
import { describeOrderingError, parseLineProperty } from '@/lib/api/errors';
import { ApiValidationError } from '@/lib/api/client';
import type { StaffReturnDetail } from '@/lib/api/staff-returns';
import { useApproveReturn, useRejectReturn } from '@/lib/hooks/useStaffReturns';

/**
 * `components/returns/ReturnDecisionPanel.tsx` — ⭐ HO-ORD-002 §11.10, US-ORD-13 AC2. Approve or reject a
 * requested return. A line the server flags `requiresDefectConfirmation` (opened food for a quality
 * problem, a recalled batch…) needs **Defect confirmed** ticked and a note before **Approve** is
 * available — and the button says why while it is not. Whether a defect is required is the server's
 * flag; the server also re-checks and its per-line message is shown on that line.
 */
interface Draft {
  confirmed: boolean;
  note: string;
}

export function ReturnDecisionPanel({ ret }: { ret: StaffReturnDetail }) {
  const approve = useApproveReturn(ret.id);
  const reject = useRejectReturn(ret.id);
  const [drafts, setDrafts] = useState<Record<string, Draft>>({});
  const [decisionNote, setDecisionNote] = useState('');
  const [rejectOpen, setRejectOpen] = useState(false);
  const [lineErrors, setLineErrors] = useState<Record<string, string>>({});
  const [rootError, setRootError] = useState<string | null>(null);
  const draftOf = (id: string): Draft => drafts[id] ?? { confirmed: false, note: '' };
  const patch = (id: string, change: Partial<Draft>) => setDrafts((cur) => ({ ...cur, [id]: { ...draftOf(id), ...change } }));

  const flagged = ret.lines.filter((l) => l.requiresDefectConfirmation);
  const missing = flagged.filter((l) => !draftOf(l.orderLineId).confirmed || !draftOf(l.orderLineId).note.trim());

  function onApprove() {
    setLineErrors({});
    setRootError(null);
    approve.mutate(
      {
        note: decisionNote.trim() || undefined,
        lines: ret.lines.map((l) => ({
          orderLineId: l.orderLineId,
          defectConfirmed: draftOf(l.orderLineId).confirmed,
          defectNote: draftOf(l.orderLineId).confirmed ? draftOf(l.orderLineId).note.trim() : undefined,
        })),
      },
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
    <Section title="Decision" description="Approve the return so the goods can be sent back, or reject it with a reason.">
      {flagged.length > 0 && (
        <ul aria-label="Lines that need a defect confirmation" className="m-0 flex list-none flex-col gap-3 p-0">
          {flagged.map((line) => {
            const draft = draftOf(line.orderLineId);
            const name = `${line.productName} ${line.variantLabel}`.trim();
            return (
              <li key={line.orderLineId} className="rounded-[var(--radius-sm)] border border-border p-3">
                <p className="text-sm font-semibold">{name}</p>
                <p className="text-sm text-ink-muted">
                  {line.anyBatchRecalled ? 'A batch of this item has been recalled. ' : ''}A defect must be confirmed before this line can be approved.
                </p>
                <label className="mt-2 flex items-center gap-2 text-sm font-semibold">
                  <input
                    type="checkbox"
                    className="h-4 w-4 accent-[var(--accent)]"
                    checked={draft.confirmed}
                    onChange={(e) => patch(line.orderLineId, { confirmed: e.target.checked })}
                  />
                  Defect confirmed<span className="sr-only"> for {name}</span>
                </label>
                <label htmlFor={`defect-${line.orderLineId}`} className="mt-2 block text-sm font-semibold">
                  Defect note<span className="sr-only"> for {name}</span>
                </label>
                <textarea
                  id={`defect-${line.orderLineId}`}
                  rows={2}
                  maxLength={500}
                  value={draft.note}
                  onChange={(e) => patch(line.orderLineId, { note: e.target.value })}
                  aria-invalid={!!lineErrors[line.orderLineId]}
                  className="mt-1 w-full rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
                />
                {lineErrors[line.orderLineId] && (
                  <p role="alert" className="mt-1 text-xs font-semibold text-danger-ink">{lineErrors[line.orderLineId]}</p>
                )}
              </li>
            );
          })}
        </ul>
      )}
      <div className="flex flex-col gap-1.5">
        <label htmlFor="return-decision-note" className="text-sm font-semibold">Note to keep with the decision (optional)</label>
        <textarea
          id="return-decision-note"
          rows={2}
          maxLength={500}
          value={decisionNote}
          onChange={(e) => setDecisionNote(e.target.value)}
          className="w-full rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
        />
      </div>
      {rootError && <p role="alert" className="rounded-[var(--radius-sm)] bg-danger-bg p-3 text-sm font-semibold text-danger-ink">{rootError}</p>}
      <div className="flex flex-wrap items-center gap-2">
        <Button
          onClick={onApprove}
          disabled={missing.length > 0 || approve.isPending}
          aria-busy={approve.isPending}
          aria-describedby={missing.length > 0 ? 'approve-why' : undefined}
        >
          {approve.isPending ? 'Approving…' : 'Approve return'}
        </Button>
        <Button variant="destructive" onClick={() => setRejectOpen(true)} disabled={approve.isPending}>
          Reject return
        </Button>
      </div>
      {missing.length > 0 && (
        <p id="approve-why" className="text-sm text-ink-muted">
          Approve is unavailable until a defect is confirmed, with a note, on: {missing.map((l) => l.productName).join(', ')}.
        </p>
      )}
      <OrderReasonDialog
        open={rejectOpen}
        onOpenChange={setRejectOpen}
        title="Reject this return"
        consequence={`The customer is told the return for ${ret.orderNumber} was not approved, with your reason.`}
        confirmLabel="Reject return"
        reasonLabel="Why is it rejected?"
        onSubmit={(note) => reject.mutateAsync(note)}
      />
    </Section>
  );
}
