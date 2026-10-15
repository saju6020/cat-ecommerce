'use client';

import * as React from 'react';
import { Plus, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { Banner, Panel, SelectField, TextField } from '@/components/inventory/fields';
import { COST_KIND_LABELS } from '@/components/inventory/labels';
import { EMPTY_ERRORS, distributeViolations, focusErrorTarget, newRowKey, type DistributedErrors } from '@/components/inventory/lineErrors';
import { MoneyInput } from '@/components/inventory/QuantityInput';
import { MONEY_PATTERN, RATE_PATTERN } from '@/lib/validation/inventory';
import { ApiConflictError, ApiValidationError } from '@/lib/api/client';
import { describeUnexpectedError } from '@/lib/api/errors';
import type { ConsignmentCostsInput, CostComponent, CostComponentKind, ExchangeRate } from '@/lib/api/procurement-consignments';

interface CostRow { key: string; kind: CostComponentKind; amount: string; currencyCode: string; note: string }
interface RateRow { key: string; currencyCode: string; rateToBase: string; recordedOn: string }

/**
 * `components/procurement/ConsignmentCosts.tsx` — HO-IMP-002 §11.2, US-IMP-03/05. Cost components
 * (kind, amount, currency, note) and exchange rates (currency → rate to base) edited together and
 * saved with the `expectedVersion` they were loaded at. A `409` says *"Someone else changed these
 * costs — reload."* and KEEPS what was typed until the person chooses to reload (NFR-USA-5).
 */
export function ConsignmentCosts({
  costComponents,
  exchangeRates,
  version,
  baseCurrency,
  today,
  readOnly,
  onSave,
  onReload,
  isPending,
}: {
  costComponents: CostComponent[];
  exchangeRates: ExchangeRate[];
  version: number;
  baseCurrency: string;
  today: string;
  readOnly: boolean;
  onSave: (request: ConsignmentCostsInput) => Promise<unknown>;
  onReload: () => void;
  isPending: boolean;
}) {
  const [costs, setCosts] = React.useState<CostRow[]>(() =>
    costComponents.map((c) => ({ key: newRowKey(), kind: c.kind, amount: c.amount.amount, currencyCode: c.amount.currencyCode, note: c.note ?? '' })),
  );
  const [rates, setRates] = React.useState<RateRow[]>(() =>
    exchangeRates.map((r) => ({ key: newRowKey(), currencyCode: r.currencyCode, rateToBase: r.rateToBase, recordedOn: r.recordedOn })),
  );
  const [costErrors, setCostErrors] = React.useState<DistributedErrors>(EMPTY_ERRORS);
  const [rateErrors, setRateErrors] = React.useState<DistributedErrors>(EMPTY_ERRORS);
  const [general, setGeneral] = React.useState<string[]>([]);
  const [conflict, setConflict] = React.useState(false);
  const [dirty, setDirty] = React.useState(false);
  const [saved, setSaved] = React.useState(false);

  function touch() {
    setDirty(true);
    setSaved(false);
  }

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    const localCosts: DistributedErrors = { fields: {}, lines: {}, general: [], firstTarget: null };
    costs.forEach((c, i) => {
      if (!MONEY_PATTERN.test(c.amount.trim())) { localCosts.lines[i] = { amount: 'Enter an amount with at most 2 decimal places.' }; localCosts.firstTarget ??= `line-${i}-amount`; }
      else if (!/^[A-Za-z]{3}$/.test(c.currencyCode.trim())) { localCosts.lines[i] = { currencyCode: 'Use a three-letter currency code.' }; localCosts.firstTarget ??= `line-${i}-currencyCode`; }
    });
    const localRates: DistributedErrors = { fields: {}, lines: {}, general: [], firstTarget: null };
    rates.forEach((r, i) => {
      if (!/^[A-Za-z]{3}$/.test(r.currencyCode.trim())) { localRates.lines[i] = { currencyCode: 'Use a three-letter currency code.' }; localRates.firstTarget ??= `line-${i}-currencyCode`; }
      else if (!RATE_PATTERN.test(r.rateToBase.trim())) { localRates.lines[i] = { rateToBase: `Enter the rate to ${baseCurrency} with at most 6 decimal places.` }; localRates.firstTarget ??= `line-${i}-rateToBase`; }
    });
    setCostErrors(localCosts);
    setRateErrors(localRates);
    if (localCosts.firstTarget || localRates.firstTarget) {
      focusErrorTarget(localCosts.firstTarget ? 'cost' : 'rate', localCosts.firstTarget ?? localRates.firstTarget);
      return;
    }
    setGeneral([]);
    setConflict(false);
    try {
      await onSave({
        expectedVersion: version,
        costComponents: costs.map((c) => ({ kind: c.kind, amount: c.amount.trim(), currencyCode: c.currencyCode.trim().toUpperCase(), note: c.note.trim() === '' ? null : c.note.trim() })),
        exchangeRates: rates.map((r) => ({ currencyCode: r.currencyCode.trim().toUpperCase(), rateToBase: r.rateToBase.trim(), recordedOn: r.recordedOn || today })),
      });
      setDirty(false);
      setSaved(true);
    } catch (error) {
      if (error instanceof ApiConflictError) {
        setConflict(true);
        return;
      }
      if (error instanceof ApiValidationError) {
        const forCosts = distributeViolations(error, { collection: 'costComponents', knownFields: [] });
        const forRates = distributeViolations(error, { collection: 'exchangeRates', knownFields: [] });
        setCostErrors(forCosts);
        setRateErrors(forRates);
        // A violation that belongs to neither list is "general" in both views.
        setGeneral(forCosts.general.filter((message) => forRates.general.includes(message)));
        focusErrorTarget(forCosts.firstTarget ? 'cost' : 'rate', forCosts.firstTarget ?? forRates.firstTarget);
        return;
      }
      setGeneral([describeUnexpectedError(error)]);
    }
  }

  return (
    <form onSubmit={submit} noValidate className="flex flex-col gap-5">
      <UnsavedChangesGuard when={dirty} />
      {conflict && (
        <Banner tone="warning">
          Someone else changed these costs — reload. What you typed is kept until you do.{' '}
          <Button variant="secondary" size="sm" onClick={onReload}>Reload</Button>
        </Banner>
      )}
      {general.length > 0 && <Banner tone="danger">{general.join(' ')}</Banner>}
      <fieldset disabled={readOnly || isPending} className="flex flex-col gap-5 border-0 p-0">
        <Panel title="Cost components">
          <div className="flex flex-col gap-3">
            {costs.length === 0 && <p className="text-sm text-ink-muted">No costs recorded yet.</p>}
            {costs.map((row, index) => (
              <div key={row.key} className="grid items-end gap-2 sm:grid-cols-[9rem_1fr_6rem_1fr_auto]">
                <SelectField id={`cost-line-${index}-kind`} label={`Cost ${index + 1} kind`} value={row.kind} onChange={(e) => { setCosts(costs.map((c) => (c.key === row.key ? { ...c, kind: e.target.value as CostComponentKind } : c))); touch(); }}>
                  {Object.entries(COST_KIND_LABELS).map(([value, label]) => (<option key={value} value={value}>{label}</option>))}
                </SelectField>
                <MoneyInput id={`cost-line-${index}-amount`} label="Amount" currencyCode={row.currencyCode || '—'} value={row.amount} error={costErrors.lines[index]?.amount} onValueChange={(v) => { setCosts(costs.map((c) => (c.key === row.key ? { ...c, amount: v } : c))); touch(); }} />
                <TextField id={`cost-line-${index}-currencyCode`} label="Currency" maxLength={3} value={row.currencyCode} error={costErrors.lines[index]?.currencyCode} onChange={(e) => { setCosts(costs.map((c) => (c.key === row.key ? { ...c, currencyCode: e.target.value.toUpperCase() } : c))); touch(); }} />
                <TextField id={`cost-line-${index}-note`} label="Note" value={row.note} onChange={(e) => { setCosts(costs.map((c) => (c.key === row.key ? { ...c, note: e.target.value } : c))); touch(); }} />
                <Button variant="ghost" size="sm" aria-label={`Remove cost ${index + 1}`} onClick={() => { setCosts(costs.filter((c) => c.key !== row.key)); touch(); }}>
                  <Trash2 size={14} aria-hidden="true" /> Remove
                </Button>
              </div>
            ))}
            <div>
              <Button variant="secondary" size="sm" onClick={() => { setCosts([...costs, { key: newRowKey(), kind: 'Freight', amount: '', currencyCode: baseCurrency, note: '' }]); touch(); }}>
                <Plus size={14} aria-hidden="true" /> Add a cost
              </Button>
            </div>
          </div>
        </Panel>
        <Panel title={`Exchange rates to ${baseCurrency}`}>
          <div className="flex flex-col gap-3">
            {rates.length === 0 && <p className="text-sm text-ink-muted">No exchange rates recorded. Add one for every currency used in the costs above.</p>}
            {rates.map((row, index) => (
              <div key={row.key} className="grid items-end gap-2 sm:grid-cols-[6rem_1fr_10rem_auto]">
                <TextField id={`rate-line-${index}-currencyCode`} label={`Rate ${index + 1} currency`} maxLength={3} value={row.currencyCode} error={rateErrors.lines[index]?.currencyCode} onChange={(e) => { setRates(rates.map((r) => (r.key === row.key ? { ...r, currencyCode: e.target.value.toUpperCase() } : r))); touch(); }} />
                <TextField id={`rate-line-${index}-rateToBase`} label={`Rate to ${baseCurrency}`} inputMode="decimal" value={row.rateToBase} hint={`1 ${row.currencyCode || 'unit'} = this many ${baseCurrency}`} error={rateErrors.lines[index]?.rateToBase} onChange={(e) => { setRates(rates.map((r) => (r.key === row.key ? { ...r, rateToBase: e.target.value } : r))); touch(); }} />
                <TextField id={`rate-line-${index}-recordedOn`} label="Recorded on" type="date" value={row.recordedOn} onChange={(e) => { setRates(rates.map((r) => (r.key === row.key ? { ...r, recordedOn: e.target.value } : r))); touch(); }} />
                <Button variant="ghost" size="sm" aria-label={`Remove rate ${index + 1}`} onClick={() => { setRates(rates.filter((r) => r.key !== row.key)); touch(); }}>
                  <Trash2 size={14} aria-hidden="true" /> Remove
                </Button>
              </div>
            ))}
            <div>
              <Button variant="secondary" size="sm" onClick={() => { setRates([...rates, { key: newRowKey(), currencyCode: '', rateToBase: '', recordedOn: today }]); touch(); }}>
                <Plus size={14} aria-hidden="true" /> Add a rate
              </Button>
            </div>
          </div>
        </Panel>
      </fieldset>
      {saved && <Banner tone="info">Costs saved. Landed cost is now out of date until you allocate it again.</Banner>}
      {!readOnly && (
        <div>
          <Button type="submit" disabled={isPending} aria-busy={isPending}>
            {isPending ? 'Saving…' : 'Save costs and rates'}
          </Button>
        </div>
      )}
    </form>
  );
}
