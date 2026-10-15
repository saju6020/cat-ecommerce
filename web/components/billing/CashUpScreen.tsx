'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { Button } from '@/components/ui/button';
import { useToast } from '@/components/ui/use-toast';
import type { CashUp } from '@/lib/api/billing-cashup';
import { ApiForbiddenError } from '@/lib/api/client';
import { describeBillingError, submitBillingForm } from '@/lib/api/errors';
import { formatDateOnly, formatInZone, todayInZone } from '@/lib/format/time';
import { MONEY_FORMAT_MESSAGE, MONEY_PATTERN, a11yMoney, moneySign, normaliseMoney, withoutSign } from '@/lib/money';
import { useActiveBranchZone } from '@/lib/hooks/billing/useActiveBranchZone';
import { useCashUp } from '@/lib/hooks/billing/useBillingQueries';
import { useCloseCashUp, useRecordCashCount } from '@/lib/hooks/billing/useBillingMutations';
import { useUrlFilterState } from '@/lib/hooks/useUrlFilterState';
import { Amount } from './Amount';
import { FormRootError, MoneyField, TextAreaField, TextField } from './Fields';
import { humanise } from './labels';
import { PageHeader, Section, Stat } from './PageHeader';
import { QueryBoundary } from './QueryBoundary';
import { guardedText } from './schemas';

/**
 * `components/billing/CashUpScreen.tsx` — HO-BIL-002 §11.6 (US-BIL-12, US-BIL-06 AC3), ⭐. The day's till
 * for a date (default today in the branch's zone, never in the future). **Expected** — cash, card,
 * transfer, cash refunds, reversals and carried-in items — is the server's, each line listed. Enter the
 * **counted cash**; the **variance** comes back from the server and reads *Short BDT 10.00* /
 * *Over …* / *Balanced*. A non-zero variance makes the **explanation** required (the server refuses
 * a close without it). Pending approvals show a banner. **Close day** is a UI-3 confirm and cannot
 * be undone; a closed day is read-only with who and when.
 */
const schema = z.object({
  countedCash: z.string().trim().min(1, 'Enter the cash you counted.').regex(MONEY_PATTERN, MONEY_FORMAT_MESSAGE),
  explanation: guardedText(500),
});
type Values = z.infer<typeof schema>;

function VarianceText({ view }: { view: CashUp }) {
  if (!view.variance) return <span>Not counted yet</span>;
  const sign = moneySign(view.variance.amount);
  if (sign === 'zero') return <span className="font-semibold text-accent-ink">Balanced</span>;
  return (
    <span className="font-semibold text-danger-ink">
      {sign === 'negative' ? 'Short' : 'Over'} {a11yMoney(withoutSign(view.variance))}
    </span>
  );
}

function CashUpBody({ view, date, zone }: { view: CashUp; date: string; zone: string }) {
  const { toast } = useToast();
  const count = useRecordCashCount(date);
  const close = useCloseCashUp(date);
  const [closeOpen, setCloseOpen] = useState(false);
  const [closeError, setCloseError] = useState<unknown>(null);
  const closed = view.status === 'Closed';
  const nonZero = !!view.variance && moneySign(view.variance.amount) !== 'zero';

  const {
    register,
    handleSubmit,
    setError,
    reset,
    formState: { errors, isSubmitting, isDirty },
  } = useForm<Values>({
    resolver: zodResolver(schema),
    mode: 'onChange',
    defaultValues: { countedCash: view.counted?.amount ?? '', explanation: view.explanation ?? '' },
  });

  useEffect(() => {
    reset({ countedCash: view.counted?.amount ?? '', explanation: view.explanation ?? '' });
  }, [view.version, view.counted?.amount, view.explanation, reset]);

  const save = handleSubmit(async (values) => {
    const ok = await submitBillingForm(
      async () => {
        await count.mutateAsync({ countedCash: normaliseMoney(values.countedCash), explanation: values.explanation.trim() || undefined });
        return true as const;
      },
      setError,
      { knownFields: ['countedCash', 'explanation'] },
    );
    if (ok) toast({ title: 'Count saved' });
  });

  async function closeDay() {
    setCloseError(null);
    try {
      await close.mutateAsync(view.version);
      toast({ title: 'Day closed', description: 'It can no longer be changed.' });
    } catch (failure) {
      setCloseError(failure);
    }
    setCloseOpen(false);
  }

  const lines = view.lines;
  return (
    <div className="flex flex-col gap-5">
      <UnsavedChangesGuard when={isDirty && !closed} />
      {closed && (
        <p role="status" className="rounded-[var(--radius-sm)] border border-border bg-surface-sunken p-3 text-sm text-ink">
          This day is closed and read-only. Closed by <strong>{view.closedByName ?? 'a colleague'}</strong>
          {view.closedOn ? ` on ${formatInZone(view.closedOn, zone)}` : ''}.
        </p>
      )}
      {!closed && view.pendingApprovals > 0 && (
        <p role="alert" className="rounded-[var(--radius-sm)] border border-border bg-warning-bg p-3 text-sm font-semibold text-warning-ink">
          {view.pendingApprovals === 1 ? '1 payment' : `${view.pendingApprovals} payments`} recorded for this day{' '}
          {view.pendingApprovals === 1 ? 'is' : 'are'} still waiting for approval.{' '}
          <Link href="/staff/billing/approvals" className="underline">Open the approvals queue</Link>. The day cannot be closed until they are decided.
        </p>
      )}

      <Section title="Expected">
        <dl className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          <Stat label={`Cash payments (${view.expected.cashCount})`}><Amount money={view.expected.cash} /></Stat>
          <Stat label={`Card payments (${view.expected.cardCount})`}><Amount money={view.expected.card} /></Stat>
          <Stat label={`Bank transfers (${view.expected.transferCount})`}><Amount money={view.expected.transfer} /></Stat>
          <Stat label="Cash refunds"><Amount money={view.expected.cashRefunds} /></Stat>
          <Stat label="Cash reversals"><Amount money={view.expected.cashReversals} /></Stat>
          <Stat label="Carried in"><Amount money={view.expected.carriedInTotal} /></Stat>
        </dl>
        <p className="text-base font-semibold text-ink">
          Expected cash in the till: <Amount money={view.expected.expectedCash} />
        </p>
        {view.expected.carriedIn.length > 0 && (
          <div>
            <h3 className="text-sm font-semibold text-ink">Carried in from earlier</h3>
            <ul className="mt-1 flex flex-col gap-1 text-sm">
              {view.expected.carriedIn.map((c) => (
                <li key={c.id}>{c.label ?? humanise(c.kind)} — <Amount money={c.amount} /></li>
              ))}
            </ul>
          </div>
        )}
        <details className="text-sm">
          <summary className="cursor-pointer font-semibold text-accent-ink">
            Show the {lines.length === 1 ? '1 line' : `${lines.length} lines`} behind these totals
          </summary>
          <ul className="mt-2 flex flex-col gap-1">
            {lines.map((l) => (
              <li key={l.id} className="flex flex-wrap justify-between gap-2 border-b border-border pb-1">
                <span className="[overflow-wrap:anywhere]">
                  {humanise(l.kind)}
                  {l.method ? ` · ${humanise(l.method)}` : ''}
                  {l.label ? ` · ${l.label}` : ''}
                  {l.carriedIn ? ' · carried in' : ''}
                  {l.countsInCash ? '' : ' · not cash'}
                </span>
                <Amount money={l.amount} />
              </li>
            ))}
          </ul>
        </details>
      </Section>

      <Section title="Count">
        {closed ? (
          <dl className="grid gap-3 sm:grid-cols-3">
            <Stat label="Counted cash">{view.counted ? <Amount money={view.counted} /> : '—'}</Stat>
            <Stat label="Variance"><VarianceText view={view} /></Stat>
            <Stat label="Explanation">{view.explanation ?? '—'}</Stat>
          </dl>
        ) : (
          <form onSubmit={save} noValidate className="flex max-w-xl flex-col gap-4">
            <FormRootError message={errors.root?.message} />
            <MoneyField id="cashup-counted" label="Counted cash" required error={errors.countedCash?.message} {...register('countedCash')} />
            {view.variance && (
              <p aria-live="polite" role="status" className="text-sm">
                Variance: <VarianceText view={view} />
              </p>
            )}
            <TextAreaField
              id="cashup-explanation"
              label="Explanation"
              required={nonZero}
              hint={nonZero ? 'Required because the count differs from the expected cash.' : 'Needed only if the count differs from the expected cash.'}
              error={errors.explanation?.message}
              {...register('explanation')}
            />
            <div className="flex flex-wrap gap-2">
              <Button type="submit" variant="secondary" disabled={isSubmitting} aria-busy={isSubmitting}>
                {isSubmitting ? 'Saving…' : 'Save count'}
              </Button>
              <Button disabled={!view.counted || isDirty || isSubmitting} onClick={() => setCloseOpen(true)}>
                Close day
              </Button>
            </div>
            {isDirty && view.counted && <p className="text-xs text-ink-muted">Save your changes before closing the day.</p>}
          </form>
        )}
        {closeError instanceof ApiForbiddenError ? (
          <PermissionDeniedState />
        ) : closeError ? (
          <p role="alert" className="text-sm font-semibold text-danger-ink">{describeBillingError(closeError)}</p>
        ) : null}
      </Section>

      <ConfirmDialog
        open={closeOpen}
        onOpenChange={setCloseOpen}
        title="Close the day"
        variant="destructive"
        consequence={`Close ${formatDateOnly(date)}? Expected ${a11yMoney(view.expected.expectedCash)}, counted ${view.counted ? a11yMoney(view.counted) : 'nothing'}${view.variance && nonZero ? `, ${moneySign(view.variance.amount) === 'negative' ? 'short' : 'over'} ${a11yMoney(withoutSign(view.variance))}` : ''}. A closed day cannot be reopened or changed.`}
        confirmLabel="Close day"
        pendingLabel="Closing…"
        onConfirm={closeDay}
      />
    </div>
  );
}

export function CashUpScreen() {
  const { zone, ready } = useActiveBranchZone();
  const { values, setValue } = useUrlFilterState(['date'] as const);
  const today = todayInZone(zone);
  const date = values.date || (ready ? today : '');
  const query = useCashUp(date);

  if (query.error instanceof ApiForbiddenError) return <AccessRefusedState error={query.error} />;
  return (
    <div className="flex flex-col gap-5">
      <PageHeader title="Daily cash-up" description="Count the till against what the system expects." />
      <form role="search" aria-label="Cash-up date" className="max-w-xs" onSubmit={(e) => e.preventDefault()}>
        <TextField id="cashup-date" label="Day" type="date" value={date} max={ready ? today : undefined} onChange={(e) => setValue('date', e.target.value)} />
      </form>
      {date === '' ? null : (
        <QueryBoundary query={query} loadingLabel="Loading the cash-up…">
          {(view) => <CashUpBody key={view.businessDate} view={view} date={date} zone={zone} />}
        </QueryBoundary>
      )}
    </div>
  );
}
