'use client';

import { useRouter } from 'next/navigation';
import { useState } from 'react';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { FormRootError, MoneyField, SelectField, TextAreaField, TextField } from '@/components/billing/Fields';
import { Button } from '@/components/ui/button';
import type { DiscountCode, DiscountCodeInput } from '@/lib/api/ordering-admin';
import { branchLocalToInstant, instantToBranchLocal } from '@/lib/format/orders';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { useSaveDiscountCode } from '@/lib/hooks/useOrderingAdmin';
import { ProductScopePicker } from './ProductScopePicker';
import { splitServerErrors, type SplitErrors } from './serverErrors';

/**
 * `components/ordering-admin/DiscountCodeForm.tsx` — ⭐ HO-ORD-002 §11.12, US-ORD-05 AC1. Create or edit a
 * discount code: code (upper-cased as typed, fixed once created), kind and value, minimum spend, scope
 * (whole order, chosen products or categories), first-order-only, overall and per-customer limits and the
 * validity window (read in the branch's zone). Every rejection is the server's message under the field it
 * concerns; what was typed is kept.
 */
const FIELDS = ['code', 'description', 'kind', 'percent', 'amount', 'minimumSpend', 'scope', 'productIds', 'categoryIds', 'usageLimit', 'perCustomerLimit', 'validFrom', 'validTo'] as const;

function initial(code: DiscountCode | null, zone: string): DiscountCodeInput {
  return {
    code: code?.code ?? '',
    description: code?.description ?? '',
    kind: code?.kind ?? 'Percentage',
    percent: code?.percent !== null && code?.percent !== undefined ? String(code.percent) : '',
    amount: code?.amount?.amount ?? '',
    minimumSpend: code?.minimumSpend?.amount ?? '',
    scope: code?.scope ?? 'Order',
    productIds: code?.productIds ?? [],
    categoryIds: code?.categoryIds ?? [],
    firstOrderOnly: code?.firstOrderOnly ?? false,
    usageLimit: code?.usageLimit !== null && code?.usageLimit !== undefined ? String(code.usageLimit) : '',
    perCustomerLimit: code?.perCustomerLimit !== null && code?.perCustomerLimit !== undefined ? String(code.perCustomerLimit) : '',
    validFrom: code?.validFrom ? instantToBranchLocal(code.validFrom, zone) : '',
    validTo: code?.validTo ? instantToBranchLocal(code.validTo, zone) : '',
    isActive: code?.isActive ?? true,
  };
}

export function DiscountCodeForm({ code }: { code: DiscountCode | null }) {
  const router = useRouter();
  const zone = useBranchTimeZone();
  const save = useSaveDiscountCode(code?.id);
  const [values, setValues] = useState(() => initial(code, zone));
  const [dirty, setDirty] = useState(false);
  const [errors, setErrors] = useState<SplitErrors>({ fields: {}, root: null });
  const set = <K extends keyof DiscountCodeInput>(name: K, value: DiscountCodeInput[K]) => {
    setDirty(true);
    setValues((cur) => ({ ...cur, [name]: value }));
  };

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    setErrors({ fields: {}, root: null });
    const request: DiscountCodeInput = {
      ...values,
      validFrom: values.validFrom ? branchLocalToInstant(values.validFrom, zone) : '',
      validTo: values.validTo ? branchLocalToInstant(values.validTo, zone) : '',
    };
    try {
      await save.mutateAsync(request);
      setDirty(false);
      router.push('/staff/admin/discount-codes');
    } catch (error) {
      setErrors(splitServerErrors(error, FIELDS));
    }
  }

  const e = errors.fields;
  return (
    <form onSubmit={submit} noValidate className="flex max-w-2xl flex-col gap-4">
      <UnsavedChangesGuard when={dirty && !save.isPending} />
      <FormRootError message={errors.root ?? undefined} />
      <TextField id="dc-code" label="Code" required maxLength={40} value={values.code} disabled={!!code} error={e.code} hint={code ? 'A code cannot be renamed once it exists.' : 'Capitals and numbers, as the customer types it.'} onChange={(ev) => set('code', ev.target.value.toUpperCase())} />
      <TextAreaField id="dc-description" label="Description" maxLength={300} value={values.description} error={e.description} hint="For staff only." onChange={(ev) => set('description', ev.target.value)} />
      <SelectField id="dc-kind" label="Kind" value={values.kind} error={e.kind} onChange={(ev) => set('kind', ev.target.value as DiscountCodeInput['kind'])}>
        <option value="Percentage">Percentage off</option>
        <option value="FixedAmount">Fixed amount off</option>
      </SelectField>
      {values.kind === 'Percentage' ? (
        <TextField id="dc-percent" label="Percent off" required inputMode="decimal" value={values.percent} error={e.percent} onChange={(ev) => set('percent', ev.target.value)} />
      ) : (
        <MoneyField id="dc-amount" label="Amount off" required value={values.amount} error={e.amount} onChange={(ev) => set('amount', ev.target.value)} />
      )}
      <MoneyField id="dc-min" label="Minimum spend" value={values.minimumSpend} error={e.minimumSpend} hint="Optional." onChange={(ev) => set('minimumSpend', ev.target.value)} />
      <SelectField id="dc-scope" label="Applies to" value={values.scope} error={e.scope} onChange={(ev) => set('scope', ev.target.value as DiscountCodeInput['scope'])}>
        <option value="Order">The whole order</option>
        <option value="Products">Chosen products</option>
        <option value="Categories">Chosen categories</option>
      </SelectField>
      {values.scope === 'Products' && <ProductScopePicker selected={values.productIds} onChange={(ids) => set('productIds', ids)} />}
      {values.scope === 'Categories' && (
        <TextField id="dc-categories" label="Category numbers" hint="Comma-separated category numbers." value={values.categoryIds.join(', ')} error={e.categoryIds} onChange={(ev) => set('categoryIds', ev.target.value.split(',').map((s) => Number.parseInt(s.trim(), 10)).filter((n) => Number.isFinite(n)))} />
      )}
      <div className="grid gap-4 sm:grid-cols-2">
        <TextField id="dc-usage" label="Total uses allowed" inputMode="numeric" value={values.usageLimit} error={e.usageLimit} hint="Empty means no limit." onChange={(ev) => set('usageLimit', ev.target.value)} />
        <TextField id="dc-per" label="Uses per customer" inputMode="numeric" value={values.perCustomerLimit} error={e.perCustomerLimit} hint="Empty means no limit." onChange={(ev) => set('perCustomerLimit', ev.target.value)} />
        <TextField id="dc-from" label={`Valid from (${zone})`} type="datetime-local" value={values.validFrom} error={e.validFrom} onChange={(ev) => set('validFrom', ev.target.value)} />
        <TextField id="dc-to" label={`Valid until (${zone})`} type="datetime-local" value={values.validTo} error={e.validTo} onChange={(ev) => set('validTo', ev.target.value)} />
      </div>
      <label className="flex items-center gap-2 text-sm"><input type="checkbox" className="h-4 w-4 accent-[var(--accent)]" checked={values.firstOrderOnly} onChange={(ev) => set('firstOrderOnly', ev.target.checked)} />First order only</label>
      <label className="flex items-center gap-2 text-sm"><input type="checkbox" className="h-4 w-4 accent-[var(--accent)]" checked={values.isActive} onChange={(ev) => set('isActive', ev.target.checked)} />Active</label>
      <div className="flex gap-2">
        <Button type="submit" disabled={save.isPending} aria-busy={save.isPending}>{save.isPending ? 'Saving…' : 'Save code'}</Button>
        <Button variant="secondary" onClick={() => router.push('/staff/admin/discount-codes')} disabled={save.isPending}>Cancel</Button>
      </div>
    </form>
  );
}
