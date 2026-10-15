'use client';

import { useEffect, useState } from 'react';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { FormRootError, TextField } from '@/components/billing/Fields';
import { PageHeader, Section } from '@/components/billing/PageHeader';
import { QueryBoundary } from '@/components/billing/QueryBoundary';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { Button } from '@/components/ui/button';
import { ApiForbiddenError } from '@/lib/api/client';
import type { BankTransferAccount, BankTransferAccountInput } from '@/lib/api/ordering-admin';
import { useBankTransferAccount, useSaveBankTransferAccount } from '@/lib/hooks/useOrderingAdmin';
import { splitServerErrors, type SplitErrors } from './serverErrors';

/**
 * `components/ordering-admin/PaymentSettingsPage.tsx` — HO-ORD-002 §11.12, US-ORD-19. The bank account
 * customers are asked to transfer to. Saving asks first (UI-3: *"Customers will be asked to transfer to
 * this account."*) and a **preview** shows the instructions exactly as the customer sees them. Nothing
 * about a card or a gateway exists here: the shop takes bank transfer, cash on delivery and cash or
 * card-terminal payments at the branch.
 */
const FIELDS = ['accountName', 'accountNumber', 'bankName', 'branchName', 'routingNumber'] as const;

function toInput(account: BankTransferAccount): BankTransferAccountInput {
  return {
    accountName: account.accountName ?? '',
    accountNumber: account.accountNumber ?? '',
    bankName: account.bankName ?? '',
    branchName: account.branchName ?? '',
    routingNumber: account.routingNumber ?? '',
    isActive: account.configured ? account.isActive : true,
  };
}

function Form({ account }: { account: BankTransferAccount }) {
  const save = useSaveBankTransferAccount();
  const [values, setValues] = useState(() => toInput(account));
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [errors, setErrors] = useState<SplitErrors>({ fields: {}, root: null });
  const [saved, setSaved] = useState(false);
  useEffect(() => setValues(toInput(account)), [account]);

  async function commit() {
    setErrors({ fields: {}, root: null });
    setSaved(false);
    try {
      await save.mutateAsync(values);
      setSaved(true);
    } catch (error) {
      setErrors(splitServerErrors(error, FIELDS));
    }
    setConfirmOpen(false);
  }

  const set = (name: keyof BankTransferAccountInput) => (e: React.ChangeEvent<HTMLInputElement>) => setValues({ ...values, [name]: e.target.value });
  return (
    <div className="grid gap-5 lg:grid-cols-2">
      <form onSubmit={(e) => { e.preventDefault(); setConfirmOpen(true); }} noValidate className="flex flex-col gap-3">
        <FormRootError message={errors.root ?? undefined} />
        <TextField id="bank-account-name" label="Account name" required maxLength={200} value={values.accountName} error={errors.fields.accountName} onChange={set('accountName')} />
        <TextField id="bank-account-number" label="Account number" required maxLength={50} value={values.accountNumber} error={errors.fields.accountNumber} onChange={set('accountNumber')} />
        <TextField id="bank-name" label="Bank" required maxLength={200} value={values.bankName} error={errors.fields.bankName} onChange={set('bankName')} />
        <TextField id="bank-branch" label="Branch" required maxLength={200} value={values.branchName} error={errors.fields.branchName} onChange={set('branchName')} />
        <TextField id="bank-routing" label="Routing number" maxLength={50} hint="Optional." value={values.routingNumber} error={errors.fields.routingNumber} onChange={set('routingNumber')} />
        <label className="flex items-center gap-2 text-sm">
          <input type="checkbox" className="h-4 w-4 accent-[var(--accent)]" checked={values.isActive} onChange={(e) => setValues({ ...values, isActive: e.target.checked })} />
          Offer bank transfer at checkout
        </label>
        {saved && <p role="status" className="text-sm font-semibold text-accent-ink">Saved. Customers will now be asked to transfer to this account.</p>}
        <div>
          <Button type="submit" disabled={save.isPending}>Save account</Button>
        </div>
      </form>
      <Section title="What the customer sees" description="The instructions shown after they choose bank transfer.">
        <dl className="m-0 grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-sm">
          <dt className="text-ink-muted">Account name</dt><dd className="m-0 [overflow-wrap:anywhere]">{values.accountName || '—'}</dd>
          <dt className="text-ink-muted">Account number</dt><dd className="m-0 [overflow-wrap:anywhere]">{values.accountNumber || '—'}</dd>
          <dt className="text-ink-muted">Bank</dt><dd className="m-0 [overflow-wrap:anywhere]">{values.bankName || '—'}</dd>
          <dt className="text-ink-muted">Branch</dt><dd className="m-0 [overflow-wrap:anywhere]">{values.branchName || '—'}</dd>
          {values.routingNumber && (<><dt className="text-ink-muted">Routing number</dt><dd className="m-0">{values.routingNumber}</dd></>)}
          <dt className="text-ink-muted">Payment reference</dt><dd className="m-0 font-semibold">The order number, such as ORD-2026-000123</dd>
        </dl>
        {!values.isActive && <p className="text-sm font-semibold text-warning-ink">Bank transfer is switched off, so customers will not be offered it.</p>}
      </Section>
      <ConfirmDialog
        open={confirmOpen}
        onOpenChange={setConfirmOpen}
        title="Save the bank account"
        consequence="Customers will be asked to transfer to this account."
        confirmLabel="Save account"
        onConfirm={commit}
      />
    </div>
  );
}

export function PaymentSettingsPage() {
  const query = useBankTransferAccount();
  if (query.error instanceof ApiForbiddenError) return <AccessRefusedState error={query.error} />;
  return (
    <div className="flex flex-col gap-5">
      <PageHeader title="Payment settings" description="The bank account for bank-transfer orders." />
      <QueryBoundary query={query} loadingLabel="Loading the account…">
        {(account) => <Form account={account} />}
      </QueryBoundary>
    </div>
  );
}
