'use client';

import { useState } from 'react';
import { Check, Link2 } from 'lucide-react';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { Button } from '@/components/ui/button';
import { useToast } from '@/components/ui/use-toast';
import { ApiForbiddenError } from '@/lib/api/client';
import type { BankLine, BankMatch, UnmatchedPayment } from '@/lib/api/billing-reconciliation';
import { describeBillingError } from '@/lib/api/errors';
import { formatDateOnly, formatInstantDate } from '@/lib/format/time';
import { a11yMoney } from '@/lib/money';
import { useBankReconciliation } from '@/lib/hooks/billing/useBillingQueries';
import { useAutoMatch, useMatchBankLine, useUnmatchBankLine } from '@/lib/hooks/billing/useBillingMutations';
import { Amount } from './Amount';
import { PageHeader, Section } from './PageHeader';
import { QueryBoundary } from './QueryBoundary';
import { ReasonDialog } from './ReasonDialog';

/**
 * `components/billing/BankMatchView.tsx` — HO-BIL-002 §11.8 (US-BIL-06 AC2), ⭐. Three panes, flagged
 * **both ways**: statement credits with no payment, bank-transfer payments with no credit, and what is
 * matched (Auto or Manual). **Auto-match** reports *matched / ambiguous / none*. To match by hand, press
 * *Select* on one line and one payment — a keyboard path (Tab, Enter) with the selections announced —
 * then **Match**; the server enforces equal amounts and says so. **Unmatch** takes a reason and a
 * confirm. No amount is compared here.
 */
function SelectButton({ selected, label, onToggle }: { selected: boolean; label: string; onToggle: () => void }) {
  return (
    <Button variant={selected ? 'primary' : 'secondary'} size="sm" aria-pressed={selected} onClick={onToggle}>
      {selected ? <Check size={14} aria-hidden="true" /> : null}
      {selected ? 'Selected' : 'Select'}
      <span className="sr-only"> {label}</span>
    </Button>
  );
}

export function BankMatchView({ statementId }: { statementId: string }) {
  const { toast } = useToast();
  const query = useBankReconciliation(statementId);
  const autoMatch = useAutoMatch(statementId);
  const match = useMatchBankLine(statementId);
  const unmatch = useUnmatchBankLine();
  const [line, setLine] = useState<BankLine | null>(null);
  const [payment, setPayment] = useState<UnmatchedPayment | null>(null);
  const [message, setMessage] = useState<{ kind: 'ok' | 'error'; text: string } | null>(null);
  const [unmatching, setUnmatching] = useState<BankMatch | null>(null);
  const [busy, setBusy] = useState(false);

  async function runAuto() {
    setBusy(true);
    setMessage(null);
    try {
      const r = await autoMatch.mutateAsync();
      setMessage({ kind: 'ok', text: `Auto-match finished: ${r.matched} matched, ${r.ambiguous} ambiguous (more than one possible payment), ${r.none} with no match.` });
      setLine(null);
      setPayment(null);
    } catch (failure) {
      setMessage({ kind: 'error', text: describeBillingError(failure) });
    } finally {
      setBusy(false);
    }
  }

  async function runMatch() {
    if (!line || !payment) return;
    setBusy(true);
    setMessage(null);
    try {
      await match.mutateAsync({ statementLineId: line.lineId, paymentId: payment.paymentId });
      setMessage({ kind: 'ok', text: `Matched line ${line.lineNumber} to ${payment.receiptNumber ?? 'the payment'}.` });
      setLine(null);
      setPayment(null);
    } catch (failure) {
      setMessage({ kind: 'error', text: describeBillingError(failure) });
    } finally {
      setBusy(false);
    }
  }

  if (query.error instanceof ApiForbiddenError) return <AccessRefusedState error={query.error} />;

  return (
    <QueryBoundary query={query} notFoundTitle="Statement not found" loadingLabel="Loading the statement…">
      {(data) => (
        <div className="flex flex-col gap-5">
          <PageHeader
            title={data.statement.bankAccountLabel}
            back={{ href: '/staff/billing/reconciliation/bank', label: 'All statements' }}
            description={`${formatDateOnly(data.statement.periodFrom)} – ${formatDateOnly(data.statement.periodTo)} · ${data.statement.matchedCount} of ${data.statement.lineCount} lines matched`}
            actions={
              <Button onClick={() => void runAuto()} disabled={busy} aria-busy={busy}>
                Auto-match
              </Button>
            }
          />
          {message && (
            <p role={message.kind === 'error' ? 'alert' : 'status'} className={message.kind === 'error' ? 'text-sm font-semibold text-danger-ink' : 'text-sm font-semibold text-accent-ink'}>
              {message.text}
            </p>
          )}
          <div aria-live="polite" className="rounded-[var(--radius-sm)] border border-border bg-surface-sunken p-3 text-sm">
            {line ? `Selected statement line ${line.lineNumber}, ${a11yMoney(line.amount)}.` : 'No statement line selected.'}{' '}
            {payment ? `Selected payment ${payment.receiptNumber ?? ''}, ${a11yMoney(payment.amount)}.` : 'No payment selected.'}
            <div className="mt-2">
              <Button disabled={!line || !payment || busy} onClick={() => void runMatch()}>
                <Link2 size={14} aria-hidden="true" />
                Match the selected pair
              </Button>
            </div>
          </div>
          <div className="grid min-w-0 gap-5 xl:grid-cols-3">
            <Section title={`Unmatched statement lines (${data.unmatchedStatementLines.length})`} description="Credits on the statement with no payment yet.">
              {data.unmatchedStatementLines.length === 0 ? (
                <p className="text-sm text-ink-muted">Every credit on this statement is matched.</p>
              ) : (
                <ul className="flex flex-col gap-2">
                  {data.unmatchedStatementLines.map((l) => (
                    <li key={l.lineId} className="flex min-w-0 flex-wrap items-center justify-between gap-2 rounded-[var(--radius-sm)] border border-border p-2 text-sm">
                      <span className="min-w-0 [overflow-wrap:anywhere]">
                        <strong>Line {l.lineNumber}</strong> · {formatDateOnly(l.transactionDate)} · <Amount money={l.amount} />
                        <br />
                        <span className="text-ink-muted">{l.reference ?? l.description ?? ''}</span>
                      </span>
                      <SelectButton selected={line?.lineId === l.lineId} label={`statement line ${l.lineNumber}`} onToggle={() => setLine(line?.lineId === l.lineId ? null : l)} />
                    </li>
                  ))}
                </ul>
              )}
              {data.debitLineCount > 0 && (
                <p className="text-xs text-ink-muted">{data.debitLineCount} debit line(s) are not shown — only credits are matched to payments.</p>
              )}
            </Section>
            <Section title={`Unmatched transfer payments (${data.unmatchedPayments.length})`} description="Bank-transfer payments with no statement line yet.">
              {data.unmatchedPayments.length === 0 ? (
                <p className="text-sm text-ink-muted">Every transfer payment is matched.</p>
              ) : (
                <ul className="flex flex-col gap-2">
                  {data.unmatchedPayments.map((p) => (
                    <li key={p.paymentId} className="flex min-w-0 flex-wrap items-center justify-between gap-2 rounded-[var(--radius-sm)] border border-border p-2 text-sm">
                      <span className="min-w-0 [overflow-wrap:anywhere]">
                        <strong>{p.receiptNumber}</strong> · {p.customerName} · <Amount money={p.amount} />
                        <br />
                        <span className="text-ink-muted">{formatDateOnly(p.receivedOn)} · {p.reference ?? 'no reference'}</span>
                      </span>
                      <SelectButton selected={payment?.paymentId === p.paymentId} label={`payment ${p.receiptNumber ?? ''}`} onToggle={() => setPayment(payment?.paymentId === p.paymentId ? null : p)} />
                    </li>
                  ))}
                </ul>
              )}
            </Section>
            <Section title={`Matched (${data.matches.length})`}>
              {data.matches.length === 0 ? (
                <p className="text-sm text-ink-muted">Nothing is matched yet.</p>
              ) : (
                <ul className="flex flex-col gap-2">
                  {data.matches.map((m) => (
                    <li key={m.matchId} className="flex min-w-0 flex-wrap items-center justify-between gap-2 rounded-[var(--radius-sm)] border border-border p-2 text-sm">
                      <span className="min-w-0 [overflow-wrap:anywhere]">
                        <strong>{m.receiptNumber}</strong> · <Amount money={m.amount} />
                        <br />
                        <span className="text-ink-muted">{m.method} match · {formatInstantDate(m.matchedOn)}</span>
                      </span>
                      <Button variant="ghost" size="sm" onClick={() => setUnmatching(m)}>
                        Unmatch<span className="sr-only"> {m.receiptNumber}</span>
                      </Button>
                    </li>
                  ))}
                </ul>
              )}
            </Section>
          </div>
          <ReasonDialog
            open={unmatching !== null}
            onOpenChange={(open) => !open && setUnmatching(null)}
            title="Unmatch this payment"
            consequence={`Unmatch ${unmatching?.receiptNumber ?? 'the payment'} (${unmatching ? a11yMoney(unmatching.amount) : ''}) from its statement line? Both go back to the unmatched lists. The match stays in the history with your reason.`}
            confirmLabel="Unmatch"
            pendingLabel="Unmatching…"
            onSubmit={async (reason) => {
              if (!unmatching) return;
              await unmatch.mutateAsync({ matchId: unmatching.matchId, reason });
              toast({ title: 'Unmatched' });
            }}
          />
        </div>
      )}
    </QueryBoundary>
  );
}
