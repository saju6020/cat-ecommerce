'use client';

import { useFieldArray, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Plus, Trash2 } from 'lucide-react';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { useToast } from '@/components/ui/use-toast';
import type { BillingProfile, SetBillingProfileRequest } from '@/lib/api/billing-profile';
import { submitBillingForm } from '@/lib/api/errors';
import { FormRootError, TextAreaField, TextField } from './Fields';
import { guardedText, requiredGuardedText } from './schemas';

/**
 * `components/billing/BillingProfileForm.tsx` — HO-BIL-002 §11.10 (NFR-LEG-3). A branch's legal
 * identity as it prints on invoices and receipts: legal name, tax registration number, address,
 * phone, email, footer and up to five bank accounts. Changes apply from the next invoice issued;
 * issued invoices keep what was printed on them. Free text runs the C-5 guard; the tax registration,
 * account and routing numbers are legal identifiers and are not guarded (HO-BIL-001 §11.4 C1).
 */
const MAX_ACCOUNTS = 5;

const accountSchema = z.object({
  bankName: requiredGuardedText(100, 'Enter the bank name.'),
  accountName: requiredGuardedText(150, 'Enter the account name.'),
  accountNumber: z.string().trim().min(1, 'Enter the account number.').max(50),
  branchName: guardedText(100),
  routingNumber: z.string().trim().max(30),
});

const schema = z.object({
  legalName: requiredGuardedText(200, 'Enter the legal name.'),
  taxRegistrationNumber: z.string().trim().min(1, 'Enter the tax registration number.').max(50),
  address: requiredGuardedText(500, 'Enter the address.'),
  phone: guardedText(40),
  email: guardedText(150),
  invoiceFooter: guardedText(500),
  bankAccounts: z.array(accountSchema).max(MAX_ACCOUNTS, `Add at most ${MAX_ACCOUNTS} bank accounts.`),
});
type Values = z.infer<typeof schema>;

function toValues(profile: BillingProfile | null): Values {
  return {
    legalName: profile?.legalName ?? '',
    taxRegistrationNumber: profile?.taxRegistrationNumber ?? '',
    address: profile?.addressLines.join('\n') ?? '',
    phone: profile?.phone ?? '',
    email: profile?.email ?? '',
    invoiceFooter: profile?.invoiceFooter ?? '',
    bankAccounts: (profile?.bankAccounts ?? []).map((a) => ({
      bankName: a.bankName,
      accountName: a.accountName,
      accountNumber: a.accountNumber,
      branchName: a.branchName ?? '',
      routingNumber: a.routingNumber ?? '',
    })),
  };
}

const KNOWN_FIELDS = [
  'legalName',
  'taxRegistrationNumber',
  'address',
  'phone',
  'email',
  'invoiceFooter',
  'bankAccounts',
] as const;

export function BillingProfileForm({
  branchName,
  profile,
  canManage,
  onSave,
}: {
  branchName: string;
  profile: BillingProfile | null;
  canManage: boolean;
  onSave: (request: SetBillingProfileRequest) => Promise<unknown>;
}) {
  const { toast } = useToast();
  const {
    register,
    control,
    handleSubmit,
    setError,
    reset,
    formState: { errors, isSubmitting, isDirty },
  } = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: toValues(profile),
    mode: 'onChange',
  });
  const { fields, append, remove } = useFieldArray({ control, name: 'bankAccounts' });

  const submit = handleSubmit(async (values) => {
    const ok = await submitBillingForm(
      async () => {
        await onSave({
          legalName: values.legalName,
          taxRegistrationNumber: values.taxRegistrationNumber,
          addressLines: values.address
            .split('\n')
            .map((line) => line.trim())
            .filter(Boolean),
          phone: values.phone || undefined,
          email: values.email || undefined,
          invoiceFooter: values.invoiceFooter || undefined,
          bankAccounts: values.bankAccounts.map((a) => ({
            bankName: a.bankName,
            accountName: a.accountName,
            accountNumber: a.accountNumber,
            branchName: a.branchName || undefined,
            routingNumber: a.routingNumber || undefined,
          })),
        });
        return true as const;
      },
      setError,
      { knownFields: KNOWN_FIELDS },
    );
    if (ok) {
      reset(values);
      toast({ title: 'Billing profile saved', description: `${branchName} invoices issued from now on use these details.` });
    }
  });

  return (
    <form onSubmit={submit} noValidate className="flex max-w-3xl flex-col gap-5">
      <UnsavedChangesGuard when={isDirty} />
      <p className="rounded-[var(--radius-sm)] border border-border bg-surface-sunken p-3 text-sm text-ink">
        Changes apply to invoices issued from now on. Issued invoices keep the details printed on
        them.
      </p>
      {!profile && (
        <p className="rounded-[var(--radius-sm)] border border-border bg-warning-bg p-3 text-sm font-semibold text-warning-ink">
          {branchName} has no billing profile yet, so no invoice can be issued there until you save
          one.
        </p>
      )}
      <FormRootError message={errors.root?.message} />
      <fieldset disabled={!canManage || isSubmitting} className="flex min-w-0 flex-col gap-4 border-0 p-0">
        <legend className="sr-only">Seller details for {branchName}</legend>
        <TextField id="bp-legal-name" label="Legal name" required error={errors.legalName?.message} {...register('legalName')} />
        <TextField id="bp-trn" label="Tax registration number" required error={errors.taxRegistrationNumber?.message} {...register('taxRegistrationNumber')} />
        <TextAreaField id="bp-address" label="Address" required hint="One line per row." error={errors.address?.message} {...register('address')} />
        <div className="grid gap-4 sm:grid-cols-2">
          <TextField id="bp-phone" label="Phone" type="tel" error={errors.phone?.message} {...register('phone')} />
          <TextField id="bp-email" label="Email" type="email" error={errors.email?.message} {...register('email')} />
        </div>
        <TextAreaField id="bp-footer" label="Invoice footer" error={errors.invoiceFooter?.message} {...register('invoiceFooter')} />

        <section aria-labelledby="bp-accounts" className="flex flex-col gap-3">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <h2 id="bp-accounts" className="text-base font-semibold text-ink">
              Bank accounts{' '}
              <span className="font-normal text-ink-muted">
                (up to {MAX_ACCOUNTS}; customers see these under “How to pay”)
              </span>
            </h2>
            <Button
              variant="secondary"
              size="sm"
              disabled={fields.length >= MAX_ACCOUNTS}
              onClick={() =>
                append({ bankName: '', accountName: '', accountNumber: '', branchName: '', routingNumber: '' })
              }
            >
              <Plus size={14} aria-hidden="true" />
              Add bank account
            </Button>
          </div>
          {errors.bankAccounts?.message && (
            <p role="alert" className="text-xs font-semibold text-danger-ink">
              {errors.bankAccounts.message}
            </p>
          )}
          {fields.length === 0 && <p className="text-sm text-ink-muted">No bank accounts yet.</p>}
          {fields.map((field, index) => {
            const e = errors.bankAccounts?.[index];
            return (
              <fieldset key={field.id} className="flex min-w-0 flex-col gap-3 rounded-[var(--radius-md)] border border-border p-3">
                <legend className="px-1 text-sm font-semibold text-ink">Bank account {index + 1}</legend>
                <div className="grid gap-3 sm:grid-cols-2">
                  <TextField id={`bp-bank-${index}`} label="Bank" required error={e?.bankName?.message} {...register(`bankAccounts.${index}.bankName`)} />
                  <TextField id={`bp-acc-name-${index}`} label="Account name" required error={e?.accountName?.message} {...register(`bankAccounts.${index}.accountName`)} />
                  <TextField id={`bp-acc-no-${index}`} label="Account number" required error={e?.accountNumber?.message} {...register(`bankAccounts.${index}.accountNumber`)} />
                  <TextField id={`bp-branch-${index}`} label="Branch" error={e?.branchName?.message} {...register(`bankAccounts.${index}.branchName`)} />
                  <TextField id={`bp-routing-${index}`} label="Routing number" error={e?.routingNumber?.message} {...register(`bankAccounts.${index}.routingNumber`)} />
                </div>
                <Button variant="ghost" size="sm" className="w-fit" onClick={() => remove(index)}>
                  <Trash2 size={14} aria-hidden="true" />
                  Remove bank account {index + 1}
                </Button>
              </fieldset>
            );
          })}
        </section>
      </fieldset>
      {canManage ? (
        <div>
          <Button type="submit" disabled={isSubmitting} aria-busy={isSubmitting}>
            {isSubmitting ? 'Saving…' : 'Save billing profile'}
          </Button>
        </div>
      ) : (
        <p className="text-sm text-ink-muted">Only an administrator can change the billing profile.</p>
      )}
    </form>
  );
}
