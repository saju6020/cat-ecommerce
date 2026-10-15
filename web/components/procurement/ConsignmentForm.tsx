'use client';

import * as React from 'react';
import { Plus, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { Banner, CheckboxField, SelectField, TextField } from '@/components/inventory/fields';
import { countryOptions } from '@/components/inventory/countries';
import { EMPTY_ERRORS, distributeViolations, focusErrorTarget, type DistributedErrors } from '@/components/inventory/lineErrors';
import { HS_CODE_PATTERN } from '@/lib/validation/inventory';
import { ApiConflictError, ApiValidationError } from '@/lib/api/client';
import { describeUnexpectedError } from '@/lib/api/errors';
import { formatDateOnly } from '@/lib/format/time';
import type { ConsignmentDetailsInput } from '@/lib/api/procurement-consignments';
import type { PurchaseOrderSummary } from '@/lib/api/procurement-orders';

export interface ConsignmentFormState {
  originCountryCode: string;
  shipmentReference: string;
  purchaseOrderIds: string[];
  hsCodes: Array<{ key: string; code: string; description: string }>;
  importPermitNumber: string;
  healthCertificateNumber: string;
  customsDeclarationNumber: string;
  expectedArrivalOn: string;
}

const TOP_FIELDS = [
  'originCountryCode', 'shipmentReference', 'purchaseOrderIds', 'importPermitNumber', 'healthCertificateNumber',
  'customsDeclarationNumber', 'expectedArrivalOn',
] as const;

const orNull = (v: string) => (v.trim() === '' ? null : v.trim());
let hsKey = 0;
export const newHsRow = (code = '', description = '') => ({ key: `hs-${(hsKey += 1)}`, code, description });

/**
 * `components/procurement/ConsignmentForm.tsx` — HO-IMP-002 §11.2, US-IMP-03. Origin country (a
 * `<select>` of names, value = alpha-2), shipment reference, the issued purchase orders it covers
 * (multi-select), repeatable HS codes (code + description), the permit / certificate / declaration
 * numbers — with the note that clearance needs all three — and the expected arrival.
 * `clearanceError` is the server's `ClearanceDocumentsMissing` message, shown against each of the
 * three number fields that is still empty.
 */
export function ConsignmentForm({
  initial,
  orders,
  readOnly,
  clearanceError,
  onSubmit,
  isPending,
  submitLabel,
  conflictMessage,
  onReload,
}: {
  initial: ConsignmentFormState;
  orders: PurchaseOrderSummary[];
  readOnly?: boolean;
  clearanceError?: string | null;
  onSubmit: (request: ConsignmentDetailsInput) => Promise<void>;
  isPending: boolean;
  submitLabel: string;
  conflictMessage?: string;
  onReload?: () => void;
}) {
  const [state, setState] = React.useState<ConsignmentFormState>(initial);
  const [errors, setErrors] = React.useState<DistributedErrors>(EMPTY_ERRORS);
  const [dirty, setDirty] = React.useState(false);
  const [submitting, setSubmitting] = React.useState(false);
  const [conflict, setConflict] = React.useState(false);
  const countries = React.useMemo(() => countryOptions(), []);

  function patch(next: Partial<ConsignmentFormState>) {
    setState((current) => ({ ...current, ...next }));
    setDirty(true);
  }

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    const local: DistributedErrors = { fields: {}, lines: {}, general: [], firstTarget: null };
    if (!state.originCountryCode) { local.fields.originCountryCode = 'Choose the country of origin.'; local.firstTarget ??= 'originCountryCode'; }
    if (!state.shipmentReference.trim()) { local.fields.shipmentReference = 'Enter the shipment reference.'; local.firstTarget ??= 'shipmentReference'; }
    if (state.purchaseOrderIds.length === 0) { local.fields.purchaseOrderIds = 'Choose at least one purchase order.'; local.firstTarget ??= 'purchaseOrderIds'; }
    state.hsCodes.forEach((row, index) => {
      if (row.code.trim() !== '' && !HS_CODE_PATTERN.test(row.code.trim())) {
        local.lines[index] = { code: 'An HS code is four digits followed by up to three groups of two (for example 2309.10).' };
        local.firstTarget ??= `line-${index}-code`;
      }
    });
    if (local.firstTarget) {
      setErrors(local);
      focusErrorTarget('cn', local.firstTarget);
      return;
    }
    setErrors(EMPTY_ERRORS);
    setConflict(false);
    setSubmitting(true);
    try {
      await onSubmit({
        originCountryCode: state.originCountryCode,
        shipmentReference: state.shipmentReference.trim(),
        purchaseOrderIds: state.purchaseOrderIds,
        hsCodes: state.hsCodes.filter((h) => h.code.trim() !== '').map((h) => ({ code: h.code.trim(), description: orNull(h.description) })),
        importPermitNumber: orNull(state.importPermitNumber),
        healthCertificateNumber: orNull(state.healthCertificateNumber),
        customsDeclarationNumber: orNull(state.customsDeclarationNumber),
        expectedArrivalOn: state.expectedArrivalOn || null,
      });
      setDirty(false);
    } catch (error) {
      setSubmitting(false);
      if (error instanceof ApiConflictError) setConflict(true);
      const distributed =
        error instanceof ApiValidationError || error instanceof ApiConflictError
          ? distributeViolations(error, { collection: 'hsCodes', knownFields: TOP_FIELDS })
          : { ...EMPTY_ERRORS, general: [describeUnexpectedError(error)] };
      setErrors(distributed);
      focusErrorTarget('cn', distributed.firstTarget);
    }
  }

  const needed = (value: string) => (clearanceError && value.trim() === '' ? clearanceError : undefined);
  const selectable = orders.filter((o) => o.status === 'Issued' || state.purchaseOrderIds.includes(o.id));

  return (
    <form onSubmit={submit} noValidate className="flex max-w-4xl flex-col gap-5">
      <UnsavedChangesGuard when={dirty && !submitting} />
      {errors.general.length > 0 && (
        <Banner tone="danger">
          <ul className="list-disc pl-5">{errors.general.map((m) => <li key={m}>{m}</li>)}</ul>
        </Banner>
      )}
      {conflict && (
        <Banner tone="warning">
          {conflictMessage ?? 'Someone else changed this consignment. Reload to see their version — what you typed is kept until you do.'}{' '}
          {onReload && <Button variant="secondary" size="sm" onClick={onReload}>Reload</Button>}
        </Banner>
      )}
      <fieldset disabled={readOnly || isPending} className="flex flex-col gap-4 border-0 p-0">
        <div className="grid gap-4 sm:grid-cols-2">
          <SelectField id="cn-originCountryCode" label="Country of origin" required value={state.originCountryCode} error={errors.fields.originCountryCode} onChange={(e) => patch({ originCountryCode: e.target.value })}>
            <option value="">Choose a country</option>
            {countries.map((c) => (<option key={c.value} value={c.value}>{c.label}</option>))}
          </SelectField>
          <TextField id="cn-shipmentReference" label="Shipment reference" required value={state.shipmentReference} error={errors.fields.shipmentReference} onChange={(e) => patch({ shipmentReference: e.target.value })} />
          <TextField id="cn-expectedArrivalOn" label="Expected arrival" type="date" value={state.expectedArrivalOn} error={errors.fields.expectedArrivalOn} onChange={(e) => patch({ expectedArrivalOn: e.target.value })} />
        </div>

        <fieldset className="flex flex-col gap-2 rounded-[var(--radius-md)] border border-border p-3" aria-describedby={errors.fields.purchaseOrderIds ? 'cn-purchaseOrderIds-error' : undefined}>
          <legend className="px-1 text-sm font-semibold text-ink">Purchase orders on this consignment *</legend>
          {selectable.length === 0 ? (
            <p className="text-sm text-ink-muted">No issued purchase orders at this branch yet.</p>
          ) : (
            selectable.map((order, index) => (
              <CheckboxField
                key={order.id}
                id={index === 0 ? 'cn-purchaseOrderIds' : `cn-po-${order.id}`}
                label={`${order.number} · ${order.supplierName} · due ${formatDateOnly(order.expectedDeliveryOn)}`}
                checked={state.purchaseOrderIds.includes(order.id)}
                onChange={(e) =>
                  patch({
                    purchaseOrderIds: e.target.checked
                      ? [...state.purchaseOrderIds, order.id]
                      : state.purchaseOrderIds.filter((id) => id !== order.id),
                  })
                }
              />
            ))
          )}
          {errors.fields.purchaseOrderIds && (
            <p id="cn-purchaseOrderIds-error" role="alert" className="text-xs font-semibold text-danger-ink">{errors.fields.purchaseOrderIds}</p>
          )}
        </fieldset>

        <fieldset className="flex flex-col gap-2 rounded-[var(--radius-md)] border border-border p-3">
          <legend className="px-1 text-sm font-semibold text-ink">HS codes</legend>
          {state.hsCodes.map((row, index) => (
            <div key={row.key} className="grid items-end gap-2 sm:grid-cols-[10rem_1fr_auto]">
              <TextField id={`cn-line-${index}-code`} label={`HS code ${index + 1}`} value={row.code} error={errors.lines[index]?.code} onChange={(e) => patch({ hsCodes: state.hsCodes.map((h) => (h.key === row.key ? { ...h, code: e.target.value } : h)) })} />
              <TextField id={`cn-line-${index}-description`} label="Description" value={row.description} onChange={(e) => patch({ hsCodes: state.hsCodes.map((h) => (h.key === row.key ? { ...h, description: e.target.value } : h)) })} />
              <Button variant="ghost" size="sm" aria-label={`Remove HS code ${index + 1}`} onClick={() => patch({ hsCodes: state.hsCodes.filter((h) => h.key !== row.key) })}>
                <Trash2 size={14} aria-hidden="true" />
                Remove
              </Button>
            </div>
          ))}
          <div>
            <Button variant="secondary" size="sm" onClick={() => patch({ hsCodes: [...state.hsCodes, newHsRow()] })}>
              <Plus size={14} aria-hidden="true" />
              Add an HS code
            </Button>
          </div>
        </fieldset>

        <fieldset className="flex flex-col gap-3 rounded-[var(--radius-md)] border border-border p-3">
          <legend className="px-1 text-sm font-semibold text-ink">Permit, certificate and declaration</legend>
          <p className="text-xs text-ink-muted">Customs clearance needs all three numbers.</p>
          <div className="grid gap-4 sm:grid-cols-3">
            <TextField id="cn-importPermitNumber" label="Import permit number" value={state.importPermitNumber} error={errors.fields.importPermitNumber ?? needed(state.importPermitNumber)} onChange={(e) => patch({ importPermitNumber: e.target.value })} />
            <TextField id="cn-healthCertificateNumber" label="Health certificate number" value={state.healthCertificateNumber} error={errors.fields.healthCertificateNumber ?? needed(state.healthCertificateNumber)} onChange={(e) => patch({ healthCertificateNumber: e.target.value })} />
            <TextField id="cn-customsDeclarationNumber" label="Customs declaration number" value={state.customsDeclarationNumber} error={errors.fields.customsDeclarationNumber ?? needed(state.customsDeclarationNumber)} onChange={(e) => patch({ customsDeclarationNumber: e.target.value })} />
          </div>
        </fieldset>
      </fieldset>
      {!readOnly && (
        <div>
          <Button type="submit" disabled={isPending} aria-busy={isPending}>
            {isPending ? 'Saving…' : submitLabel}
          </Button>
        </div>
      )}
    </form>
  );
}
