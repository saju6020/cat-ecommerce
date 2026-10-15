'use client';

import { useRouter } from 'next/navigation';
import { useState } from 'react';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { FormRootError, MoneyField, SelectField, TextField } from '@/components/billing/Fields';
import { Section } from '@/components/billing/PageHeader';
import { Button } from '@/components/ui/button';
import type { DeliveryZone, DeliveryZoneInput } from '@/lib/api/ordering-admin';
import { useAdminBranches } from '@/lib/hooks/useAdminBranches';
import { useSaveDeliveryZone } from '@/lib/hooks/useOrderingAdmin';
import { splitServerErrors, type SplitErrors } from './serverErrors';
import { ZoneServiceEditor, type ZoneService } from './ZoneServiceEditor';
import { ZoneTestBox } from './ZoneTestBox';

/**
 * `components/ordering-admin/ZoneForm.tsx` — ⭐ HO-ORD-002 §11.12, US-ORD-06. Create or edit a delivery
 * zone: name, branch, priority, active, cash on delivery (and its ceiling), **coverage rows** (a district
 * plus optional postal codes, comma-separated) and both **service levels**. The server's rejection
 * (duplicate name, nothing covered, a service the combination does not allow) is shown by the field or at
 * the top; nothing typed is lost.
 */
const FIELDS = ['name', 'branchId', 'priority', 'codMaxOrderValue', 'coverage', 'services'] as const;
const blankService = (serviceLevel: ZoneService['serviceLevel']): ZoneService => ({
  serviceLevel, isActive: serviceLevel === 'Standard', baseCharge: '0.00', freeOverOrderValue: '', leadTimeDays: serviceLevel === 'Standard' ? 2 : 1, weightBands: [], slotWindows: [],
});

function initial(zone: DeliveryZone | null): DeliveryZoneInput {
  const service = (level: ZoneService['serviceLevel']): ZoneService => {
    const found = zone?.services.find((s) => s.serviceLevel === level);
    if (!found) return blankService(level);
    return {
      serviceLevel: level, isActive: found.isActive, baseCharge: found.baseCharge.amount, freeOverOrderValue: found.freeOverOrderValue?.amount ?? '', leadTimeDays: found.leadTimeDays,
      weightBands: found.weightBands.map((b) => ({ upToGrams: b.upToGrams, surcharge: b.surcharge.amount })),
      slotWindows: found.slotWindows.map((w) => ({ dayOfWeek: w.dayOfWeek, start: w.start, end: w.end })),
    };
  };
  return {
    branchId: zone?.branchId ?? '', name: zone?.name ?? '', priority: zone?.priority ?? 1, isActive: zone?.isActive ?? true, codEnabled: zone?.codEnabled ?? true,
    codMaxOrderValue: zone?.codMaxOrderValue?.amount ?? '',
    coverage: zone?.coverage.map((c) => ({ district: c.district, postalCodes: c.postalCodes })) ?? [{ district: '', postalCodes: [] }],
    services: [service('Standard'), service('Express')],
  };
}

const parseCodes = (text: string): string[] => text.split(',').map((s) => s.trim()).filter(Boolean);

/** Keeps what is being typed (a trailing comma, a space) in its own text, and reports the parsed list. */
function PostalCodesField({ id, label, codes, onCodes }: { id: string; label: string; codes: string[]; onCodes: (codes: string[]) => void }) {
  const [text, setText] = useState(codes.join(', '));
  // A row removed above this one hands it different codes: show them, unless they are what was typed.
  if (parseCodes(text).join('|') !== codes.join('|')) setText(codes.join(', '));
  return (
    <TextField
      id={id}
      label={label}
      hint="Comma-separated."
      value={text}
      onChange={(ev) => {
        setText(ev.target.value);
        onCodes(parseCodes(ev.target.value));
      }}
    />
  );
}

export function ZoneForm({ zone }: { zone: DeliveryZone | null }) {
  const router = useRouter();
  const branches = useAdminBranches();
  const save = useSaveDeliveryZone(zone?.id);
  const [values, setValues] = useState(() => initial(zone));
  const [dirty, setDirty] = useState(false);
  const [errors, setErrors] = useState<SplitErrors>({ fields: {}, root: null });
  const patch = (change: Partial<DeliveryZoneInput>) => { setDirty(true); setValues((cur) => ({ ...cur, ...change })); };

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    setErrors({ fields: {}, root: null });
    try {
      await save.mutateAsync(values);
      setDirty(false);
      router.push('/staff/admin/delivery-zones');
    } catch (error) {
      setErrors(splitServerErrors(error, FIELDS));
    }
  }
  const e = errors.fields;
  return (
    <div className="flex flex-col gap-5">
      <form onSubmit={submit} noValidate className="flex flex-col gap-5">
        <UnsavedChangesGuard when={dirty && !save.isPending} />
        <FormRootError message={errors.root ?? undefined} />
        <Section title="Zone">
          <div className="grid gap-3 sm:grid-cols-2">
            <TextField id="zone-name" label="Name" required maxLength={100} value={values.name} error={e.name} onChange={(ev) => patch({ name: ev.target.value })} />
            <SelectField id="zone-branch" label="Branch" required value={values.branchId} error={e.branchId} onChange={(ev) => patch({ branchId: ev.target.value })}>
              <option value="">Choose a branch</option>
              {(branches.data ?? []).map((b) => <option key={b.id} value={b.id}>{b.name}</option>)}
            </SelectField>
            <TextField id="zone-priority" label="Priority" type="number" min={1} hint="The lowest number wins when zones overlap." value={values.priority} error={e.priority} onChange={(ev) => patch({ priority: Number.parseInt(ev.target.value, 10) || 1 })} />
          </div>
          <label className="flex items-center gap-2 text-sm"><input type="checkbox" className="h-4 w-4 accent-[var(--accent)]" checked={values.isActive} onChange={(ev) => patch({ isActive: ev.target.checked })} />Zone is active</label>
          <label className="flex items-center gap-2 text-sm"><input type="checkbox" className="h-4 w-4 accent-[var(--accent)]" checked={values.codEnabled} onChange={(ev) => patch({ codEnabled: ev.target.checked })} />Allow cash on delivery</label>
          {values.codEnabled && <MoneyField id="zone-cod-max" label="Cash on delivery ceiling" hint="Optional. Orders above this must pay another way." value={values.codMaxOrderValue} error={e.codMaxOrderValue} onChange={(ev) => patch({ codMaxOrderValue: ev.target.value })} />}
        </Section>
        <Section title="Coverage" description="Districts this zone serves. Leave postal codes empty to cover the whole district.">
          {e.coverage && <p role="alert" className="text-sm font-semibold text-danger-ink">{e.coverage}</p>}
          {values.coverage.map((row, index) => (
            <div key={index} className="flex flex-wrap items-end gap-2">
              <TextField id={`cov-${index}-d`} label={`Area ${index + 1}: district`} value={row.district} onChange={(ev) => patch({ coverage: values.coverage.map((r, i) => (i === index ? { ...r, district: ev.target.value } : r)) })} />
              <PostalCodesField id={`cov-${index}-p`} label={`Area ${index + 1}: postal codes`} codes={row.postalCodes} onCodes={(postalCodes) => patch({ coverage: values.coverage.map((r, i) => (i === index ? { ...r, postalCodes } : r)) })} />
              <Button variant="ghost" size="sm" onClick={() => patch({ coverage: values.coverage.filter((_, i) => i !== index) })}>Remove area {index + 1}</Button>
            </div>
          ))}
          <div><Button variant="secondary" size="sm" onClick={() => patch({ coverage: [...values.coverage, { district: '', postalCodes: [] }] })}>Add an area</Button></div>
        </Section>
        {e.services && <p role="alert" className="text-sm font-semibold text-danger-ink">{e.services}</p>}
        {values.services.map((service, index) => (
          <ZoneServiceEditor key={service.serviceLevel} service={service} onChange={(next) => patch({ services: values.services.map((s, i) => (i === index ? next : s)) })} />
        ))}
        <div className="flex gap-2">
          <Button type="submit" disabled={save.isPending} aria-busy={save.isPending}>{save.isPending ? 'Saving…' : 'Save zone'}</Button>
          <Button variant="secondary" onClick={() => router.push('/staff/admin/delivery-zones')} disabled={save.isPending}>Cancel</Button>
        </div>
      </form>
      <ZoneTestBox />
    </div>
  );
}
