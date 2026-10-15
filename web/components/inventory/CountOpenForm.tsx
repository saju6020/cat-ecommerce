'use client';

import * as React from 'react';
import { Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { Banner } from './fields';
import { LocationSelect } from './LocationSelect';
import { SkuPicker } from './SkuPicker';
import { COUNT_SCOPE_LABELS } from './labels';
import { describeUnexpectedError, inventoryViolations } from '@/lib/api/errors';
import type { SkuLookup, StockLocation } from '@/lib/api/inventory-lookup';
import type { CountScope } from '@/lib/api/inventory-control';

/**
 * `components/inventory/CountOpenForm.tsx` — HO-IMP-002 §11.5, US-IMP-14. Opens a count: the
 * location, and either the whole location or only selected items. `CountLocationBusy` (a count is
 * already open there) is the server's sentence, shown against the location.
 */
export function CountOpenForm({
  locations,
  onOpen,
  isPending,
}: {
  locations: StockLocation[] | undefined;
  onOpen: (request: { locationId: string; scope: CountScope; skuIds: string[] }) => Promise<void>;
  isPending: boolean;
}) {
  const [locationId, setLocationId] = React.useState('');
  const [scope, setScope] = React.useState<CountScope>('FullLocation');
  const [skus, setSkus] = React.useState<SkuLookup[]>([]);
  const [locationError, setLocationError] = React.useState<string | undefined>();
  const [skuError, setSkuError] = React.useState<string | undefined>();
  const [general, setGeneral] = React.useState<string | null>(null);
  const [dirty, setDirty] = React.useState(false);
  const [submitting, setSubmitting] = React.useState(false);

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    setGeneral(null);
    setLocationError(locationId ? undefined : 'Choose the location to count.');
    setSkuError(scope === 'SelectedSkus' && skus.length === 0 ? 'Choose at least one item to count.' : undefined);
    if (!locationId || (scope === 'SelectedSkus' && skus.length === 0)) return;
    setSubmitting(true);
    try {
      await onOpen({ locationId, scope, skuIds: scope === 'SelectedSkus' ? skus.map((s) => s.skuId) : [] });
      setDirty(false);
    } catch (error) {
      setSubmitting(false);
      const violations = inventoryViolations(error);
      if (violations.length === 0) {
        setGeneral(describeUnexpectedError(error));
        return;
      }
      for (const violation of violations) {
        if (violation.propertyName === 'LocationId') setLocationError(violation.message);
        else if (violation.propertyName === 'SkuIds') setSkuError(violation.message);
        else setGeneral(violation.message);
      }
    }
  }

  return (
    <form onSubmit={submit} noValidate className="flex max-w-2xl flex-col gap-5">
      <UnsavedChangesGuard when={dirty && !submitting} />
      {general && <Banner tone="danger">{general}</Banner>}
      <LocationSelect
        id="count-locationId"
        label="Location to count"
        required
        locations={locations}
        value={locationId}
        error={locationError}
        onChange={(v) => { setLocationId(v); setDirty(true); }}
      />
      <fieldset className="flex flex-col gap-2">
        <legend className="text-sm font-semibold text-ink">What to count</legend>
        {(['FullLocation', 'SelectedSkus'] as const).map((value) => (
          <label key={value} className="flex items-center gap-2 text-sm text-ink">
            <input type="radio" name="count-scope" checked={scope === value} onChange={() => { setScope(value); setDirty(true); }} className="h-4 w-4 accent-[var(--accent)]" />
            {COUNT_SCOPE_LABELS[value]}
            {value === 'FullLocation' && <span className="text-ink-muted"> — anything you do not count is counted as none</span>}
          </label>
        ))}
      </fieldset>
      {scope === 'SelectedSkus' && (
        <section aria-labelledby="count-items-heading" className="flex flex-col gap-3">
          <h2 id="count-items-heading" className="text-sm font-semibold text-ink">Items to count</h2>
          <SkuPicker
            id="count-open-sku"
            label="Add an item"
            error={skuError}
            onSelect={(sku) => { setSkus((cur) => (cur.some((s) => s.skuId === sku.skuId) ? cur : [...cur, sku])); setDirty(true); }}
          />
          <ul className="flex flex-col gap-1 text-sm">
            {skus.map((s) => (
              <li key={s.skuId} className="flex items-center justify-between gap-2 rounded-[var(--radius-sm)] border border-border px-3 py-1.5">
                <span>{s.displayName} — {s.skuCode}</span>
                <Button variant="ghost" size="sm" aria-label={`Remove ${s.displayName}`} onClick={() => setSkus((cur) => cur.filter((x) => x.skuId !== s.skuId))}>
                  <Trash2 size={14} aria-hidden="true" />
                  Remove
                </Button>
              </li>
            ))}
          </ul>
        </section>
      )}
      <div>
        <Button type="submit" disabled={isPending || submitting}>Open the count</Button>
      </div>
    </form>
  );
}
