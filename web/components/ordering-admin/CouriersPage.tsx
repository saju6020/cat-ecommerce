'use client';

import { useState } from 'react';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { FormRootError, TextField } from '@/components/billing/Fields';
import { PageHeader, Section } from '@/components/billing/PageHeader';
import { QueryBoundary } from '@/components/billing/QueryBoundary';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ApiForbiddenError } from '@/lib/api/client';
import type { Courier, CourierInput } from '@/lib/api/ordering-admin';
import { useCouriers, useSaveCourier } from '@/lib/hooks/useOrderingAdmin';
import { splitServerErrors } from './serverErrors';

/**
 * `components/ordering-admin/CouriersPage.tsx` — HO-ORD-002 §11.12, US-ORD-11. Couriers: name, the
 * tracking-link template (it must contain `{tracking}` — the server's message appears under the field
 * when it does not), whether the courier takes cash on delivery, and whether it is active. Adding and
 * editing share one form; the list refreshes after a save.
 */
const FIELDS = ['name', 'trackingUrlTemplate'] as const;
const BLANK: CourierInput = { name: '', trackingUrlTemplate: '', supportsCod: false, isActive: true };

function CourierForm({ courier, onDone }: { courier: Courier | null; onDone: () => void }) {
  const save = useSaveCourier(courier?.id);
  const [values, setValues] = useState<CourierInput>(
    courier
      ? { name: courier.name, trackingUrlTemplate: courier.trackingUrlTemplate ?? '', supportsCod: courier.supportsCod, isActive: courier.isActive }
      : BLANK,
  );
  const [errors, setErrors] = useState<{ fields: Record<string, string>; root: string | null }>({ fields: {}, root: null });

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    setErrors({ fields: {}, root: null });
    try {
      await save.mutateAsync(values);
      onDone();
    } catch (error) {
      setErrors(splitServerErrors(error, FIELDS));
    }
  }

  return (
    <form onSubmit={submit} noValidate className="flex flex-col gap-3">
      <FormRootError message={errors.root ?? undefined} />
      <TextField id="courier-name" label="Courier name" required maxLength={100} value={values.name} error={errors.fields.name} onChange={(e) => setValues({ ...values, name: e.target.value })} />
      <TextField
        id="courier-template"
        label="Tracking link template"
        value={values.trackingUrlTemplate}
        hint="Optional. Must start with https:// and contain {tracking} where the tracking number goes."
        error={errors.fields.trackingUrlTemplate}
        onChange={(e) => setValues({ ...values, trackingUrlTemplate: e.target.value })}
      />
      <label className="flex items-center gap-2 text-sm">
        <input type="checkbox" className="h-4 w-4 accent-[var(--accent)]" checked={values.supportsCod} onChange={(e) => setValues({ ...values, supportsCod: e.target.checked })} />
        Takes cash on delivery
      </label>
      <label className="flex items-center gap-2 text-sm">
        <input type="checkbox" className="h-4 w-4 accent-[var(--accent)]" checked={values.isActive} onChange={(e) => setValues({ ...values, isActive: e.target.checked })} />
        Active (offered when dispatching)
      </label>
      <div className="flex gap-2">
        <Button type="submit" disabled={save.isPending} aria-busy={save.isPending}>{save.isPending ? 'Saving…' : 'Save courier'}</Button>
        <Button variant="secondary" onClick={onDone} disabled={save.isPending}>Cancel</Button>
      </div>
    </form>
  );
}

export function CouriersPage() {
  const query = useCouriers();
  const [editing, setEditing] = useState<Courier | 'new' | null>(null);
  if (query.error instanceof ApiForbiddenError) return <AccessRefusedState error={query.error} />;
  return (
    <div className="flex flex-col gap-5">
      <PageHeader title="Couriers" description="The couriers staff choose from when dispatching an order." actions={<Button onClick={() => setEditing('new')}>Add a courier</Button>} />
      {editing && (
        <Section title={editing === 'new' ? 'New courier' : `Edit ${editing.name}`}>
          <CourierForm key={editing === 'new' ? 'new' : editing.id} courier={editing === 'new' ? null : editing} onDone={() => setEditing(null)} />
        </Section>
      )}
      <QueryBoundary query={query} loadingLabel="Loading couriers…">
        {(couriers) =>
          couriers.length === 0 ? (
            <p className="text-sm text-ink-muted">No couriers yet. Add one so orders can be dispatched.</p>
          ) : (
            <ul aria-label="Couriers" className="m-0 flex list-none flex-col gap-2 p-0">
              {couriers.map((c) => (
                <li key={c.id} className="flex flex-wrap items-center justify-between gap-2 rounded-[var(--radius-md)] border border-border bg-surface p-3">
                  <div className="min-w-0">
                    <p className="font-semibold [overflow-wrap:anywhere]">{c.name}</p>
                    <p className="text-xs text-ink-muted [overflow-wrap:anywhere]">{c.trackingUrlTemplate ?? 'No tracking link'}{c.supportsCod ? ' · takes cash on delivery' : ''}</p>
                  </div>
                  <div className="flex items-center gap-2">
                    <Badge variant={c.isActive ? 'success' : 'neutral'}>{c.isActive ? 'Active' : 'Inactive'}</Badge>
                    <Button variant="secondary" size="sm" onClick={() => setEditing(c)}>Edit<span className="sr-only"> {c.name}</span></Button>
                  </div>
                </li>
              ))}
            </ul>
          )
        }
      </QueryBoundary>
    </div>
  );
}
