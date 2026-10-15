'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { WriteButton } from '@/components/common/WriteButton';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { toast } from '@/components/ui/use-toast';
import { useEraseCustomer, useRetentionSchedule } from '@/lib/hooks/useRetention';
import { describeOperationsError, hasOperationsCode, OPERATIONS_CODE_MESSAGES } from '@/lib/api/errors';
import {
  ERASURE_CHANNELS,
  ERASURE_IDENTITY_CHECKS,
  type ErasureChannel,
  type ErasureIdentityCheck,
} from '@/lib/api/admin-erasure';
import { ERASURE_CHANNEL_LABEL, ERASURE_IDENTITY_CHECK_LABEL } from '@/lib/retention/labels';

const schema = z.object({
  channel: z.enum(ERASURE_CHANNELS, { message: 'Choose how the request was made.' }),
  identityCheck: z.enum(ERASURE_IDENTITY_CHECKS, { message: 'Choose how identity was checked.' }),
});
type FormValues = z.infer<typeof schema>;

export interface ErasableCustomer {
  id: string;
  fullName: string;
  status: 'Active' | 'Merged' | 'Anonymised';
  mergedIntoCustomerId: string | null;
}

/**
 * `components/retention/ErasePersonalDataPanel.tsx` — HO-XCT-002 §11.5, U-B5/U-B6, UI-3, OQ-6.
 * On the staff customer page for people who may erase (the server still decides). It says what is
 * erased and what is KEPT and why, asks for the channel and how identity was checked (four fixed
 * choices, no free text, no reason box — HO-XCT-001 2026-10-12), and a confirmation names the
 * customer once and says "This cannot be undone." An already-anonymised customer gets
 * "Personal data was erased." with a link to the ledger; a merged record points to the survivor.
 */
export function ErasePersonalDataPanel({ customer }: { customer: ErasableCustomer }) {
  const schedule = useRetentionSchedule();
  const erase = useEraseCustomer(customer.id);
  const [pending, setPending] = useState<FormValues | null>(null);
  const [mergedNotice, setMergedNotice] = useState(false);
  const { register, handleSubmit, setError, formState: { errors } } = useForm<FormValues>({
    resolver: zodResolver(schema),
  });

  if (customer.status === 'Anonymised') {
    return (
      <section aria-labelledby="erase-done-title" className="rounded-[var(--radius-md)] border border-border bg-surface p-4">
        <h2 id="erase-done-title" className="text-[15px] font-semibold text-ink">Personal data was erased.</h2>
        <p className="mt-1 text-sm text-ink-muted">
          This record now shows a placeholder. See what was kept, and until when, in the{' '}
          <Link href={`/staff/admin/retention/erasures/${customer.id}`} className="font-semibold text-accent-ink hover:underline">
            erasure ledger
          </Link>
          .
        </p>
      </section>
    );
  }

  const years = (recordClass: string) =>
    schedule.data?.classes.find((c) => c.recordClass === recordClass)?.effectivePeriod ?? null;
  const clinical = years('Clinical');
  const financial = years('Financial');

  async function confirm() {
    if (!pending) return;
    const values = pending;
    try {
      await erase.mutateAsync(values as { channel: ErasureChannel; identityCheck: ErasureIdentityCheck });
      setPending(null);
      toast({ title: 'Personal data erased', description: 'The record now shows a placeholder.' });
    } catch (error) {
      setPending(null);
      if (hasOperationsCode(error, 'CustomerRecordMerged')) setMergedNotice(true);
      else setError('root', { message: describeOperationsError(error) });
    }
  }

  return (
    <section aria-labelledby="erase-title" className="flex flex-col gap-3 rounded-[var(--radius-md)] border border-border bg-surface p-4">
      <h2 id="erase-title" className="text-[15px] font-semibold text-ink">Erase personal data</h2>
      <div className="grid gap-3 text-sm sm:grid-cols-2">
        <div>
          <p className="font-semibold text-ink">Erased</p>
          <p className="text-ink-muted">
            Name, phone number, email address, addresses, marketing consent and the contents of
            messages sent to this customer.
          </p>
        </div>
        <div>
          <p className="font-semibold text-ink">Kept, and why</p>
          <p className="text-ink-muted">
            Clinical records for {clinical ?? 'the statutory number of'} years and financial
            records for {financial ?? 'the statutory number of'} years, because the law requires
            it, and the audit trail. They no longer show who the customer was.
          </p>
        </div>
      </div>

      {(mergedNotice || customer.status === 'Merged') && (
        <p role="status" className="text-sm text-ink">
          {OPERATIONS_CODE_MESSAGES.CustomerRecordMerged}{' '}
          {customer.mergedIntoCustomerId && (
            <Link href={`/staff/customers/${customer.mergedIntoCustomerId}`} className="font-semibold text-accent-ink hover:underline">
              Open the surviving record
            </Link>
          )}
        </p>
      )}

      {customer.status !== 'Merged' && (
        <form onSubmit={handleSubmit((values) => setPending(values))} noValidate className="flex flex-col gap-3">
          <div className="grid gap-3 sm:grid-cols-2">
            <div className="flex flex-col gap-1">
              <label htmlFor="erase-channel" className="text-sm font-semibold text-ink">How was the request made?</label>
              <select id="erase-channel" defaultValue="" aria-invalid={Boolean(errors.channel)}
                className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-2 py-2 text-sm text-ink" {...register('channel')}>
                <option value="" disabled>Choose…</option>
                {ERASURE_CHANNELS.map((c) => <option key={c} value={c}>{ERASURE_CHANNEL_LABEL[c]}</option>)}
              </select>
              {errors.channel && <p role="alert" className="text-sm text-danger-ink">{errors.channel.message}</p>}
            </div>
            <div className="flex flex-col gap-1">
              <label htmlFor="erase-identity" className="text-sm font-semibold text-ink">How was identity checked?</label>
              <select id="erase-identity" defaultValue="" aria-invalid={Boolean(errors.identityCheck)}
                className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-2 py-2 text-sm text-ink" {...register('identityCheck')}>
                <option value="" disabled>Choose…</option>
                {ERASURE_IDENTITY_CHECKS.map((c) => <option key={c} value={c}>{ERASURE_IDENTITY_CHECK_LABEL[c]}</option>)}
              </select>
              {errors.identityCheck && <p role="alert" className="text-sm text-danger-ink">{errors.identityCheck.message}</p>}
            </div>
          </div>
          {errors.root && <p role="alert" className="text-sm text-danger-ink">{errors.root.message}</p>}
          <div>
            <WriteButton type="submit" variant="destructive">Erase personal data…</WriteButton>
          </div>
        </form>
      )}

      <ConfirmDialog
        open={pending !== null}
        onOpenChange={(open) => !open && setPending(null)}
        title="Erase personal data?"
        consequence={
          <>
            <span className="block">
              You are erasing the personal data of <strong>{customer.fullName}</strong>: name,
              phone, email, addresses, marketing consent and message contents.
            </span>
            <span className="mt-2 block">
              Clinical and financial records are kept for the periods the law requires, and the
              audit trail is kept.
            </span>
            <span className="mt-2 block font-semibold">This cannot be undone.</span>
          </>
        }
        confirmLabel="Erase personal data"
        variant="destructive"
        onConfirm={confirm}
      />
    </section>
  );
}
