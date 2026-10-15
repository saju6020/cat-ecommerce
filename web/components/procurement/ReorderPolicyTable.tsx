'use client';

import * as React from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { DataTable } from '@/components/common/DataTable';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  useDialogReturnFocus,
} from '@/components/ui/dialog';
import { SelectField } from '@/components/inventory/fields';
import { QuantityInput } from '@/components/inventory/QuantityInput';
import { describeInventoryError } from '@/lib/api/errors';
import { formatQuantity } from '@/lib/format/inventory';
import { quantityProblem } from '@/lib/validation/inventory';
import type { ReorderPolicy, SetReorderPolicyRequest } from '@/lib/api/procurement-reorder';
import type { SupplierSummary } from '@/lib/api/procurement-suppliers';

export interface PolicyUnit {
  stockUnit: string;
  allowsFractional: boolean;
}

/**
 * `components/procurement/ReorderPolicyTable.tsx` — HO-IMP-002 §11.2, US-IMP-12. Reorder point, an
 * optional fixed reorder quantity and the preferred supplier per item × location. A row using the
 * default point is marked *"default"*. The policy rows carry no unit, so each item's unit is supplied
 * by the page (`units`); a row whose unit cannot be resolved offers no edit. A point of 0 silences the
 * pair (the dialog says so).
 */
export function ReorderPolicyTable({
  rows,
  totalCount,
  pageSize,
  isLoading,
  isError,
  onRetry,
  units,
  suppliers,
  canEdit,
  onSave,
}: {
  rows: ReorderPolicy[] | undefined;
  totalCount: number;
  pageSize: number;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
  units: Record<string, PolicyUnit | undefined>;
  suppliers: SupplierSummary[];
  canEdit: boolean;
  onSave: (policy: ReorderPolicy, request: SetReorderPolicyRequest) => Promise<unknown>;
}) {
  const [editing, setEditing] = React.useState<ReorderPolicy | null>(null);
  return (
    <>
      <DataTable
        caption="Reorder policies"
        rows={rows}
        getRowId={(row) => `${row.skuId}-${row.locationId}`}
        isLoading={isLoading}
        isError={isError}
        onRetry={onRetry}
        pagination={{ totalCount, pageSize }}
        emptyState={{ title: 'No reorder policies', description: 'Items with stock appear here with the default reorder point.' }}
        columns={[
          { id: 'item', header: 'Item', cell: (r) => `${r.displayName} (${r.skuCode})` },
          { id: 'loc', header: 'Location', cell: (r) => r.locationCode ?? '—' },
          {
            id: 'point',
            header: 'Reorder point',
            cell: (r) => (
              <span>
                {units[r.skuId] ? formatQuantity(r.reorderPoint, units[r.skuId]!.stockUnit) : '—'} {r.isDefaulted && <Badge variant="neutral">default</Badge>}
              </span>
            ),
          },
          { id: 'qty', header: 'Fixed quantity', cell: (r) => (r.reorderQuantity && units[r.skuId] ? formatQuantity(r.reorderQuantity, units[r.skuId]!.stockUnit) : 'Worked out from use') },
          { id: 'supplier', header: 'Preferred supplier', cell: (r) => r.preferredSupplierName ?? 'None set' },
          {
            id: 'edit',
            header: 'Edit',
            cell: (r) =>
              canEdit && units[r.skuId] ? (
                <Button size="sm" variant="secondary" onClick={() => setEditing(r)} aria-label={`Edit the reorder policy for ${r.displayName} at ${r.locationCode ?? 'this location'}`}>
                  Edit
                </Button>
              ) : null,
          },
        ]}
      />
      {editing && units[editing.skuId] && (
        <PolicyDialog
          policy={editing}
          unit={units[editing.skuId]!}
          suppliers={suppliers}
          onClose={() => setEditing(null)}
          onSave={(request) => onSave(editing, request)}
        />
      )}
    </>
  );
}

function PolicyDialog({
  policy,
  unit,
  suppliers,
  onClose,
  onSave,
}: {
  policy: ReorderPolicy;
  unit: PolicyUnit;
  suppliers: SupplierSummary[];
  onClose: () => void;
  onSave: (request: SetReorderPolicyRequest) => Promise<unknown>;
}) {
  const [point, setPoint] = React.useState(policy.reorderPoint);
  const [quantity, setQuantity] = React.useState(policy.reorderQuantity ?? '');
  const [supplierId, setSupplierId] = React.useState(policy.preferredSupplierId ?? '');
  const [error, setError] = React.useState<string | null>(null);
  const [fieldError, setFieldError] = React.useState<{ point?: string; quantity?: string }>({});
  const [pending, setPending] = React.useState(false);
  const returnFocusRef = useDialogReturnFocus(true);
  const options = { stockUnit: unit.stockUnit, allowsFractional: unit.allowsFractional };

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    const pointProblem = point.trim() === '' ? `Enter the reorder point in ${unit.stockUnit}.` : quantityProblem(point, options);
    const quantityProblemText = quantity.trim() === '' ? null : quantityProblem(quantity, options);
    setFieldError({ point: pointProblem ?? undefined, quantity: quantityProblemText ?? undefined });
    if (pointProblem || quantityProblemText) return;
    setPending(true);
    setError(null);
    try {
      await onSave({ reorderPoint: point.trim(), reorderQuantity: quantity.trim() === '' ? null : quantity.trim(), preferredSupplierId: supplierId || null });
      onClose();
    } catch (caught) {
      setError(describeInventoryError(caught));
    } finally {
      setPending(false);
    }
  }

  return (
    <Dialog open onOpenChange={(open) => !open && !pending && onClose()}>
      <DialogContent hideClose returnFocusRef={returnFocusRef}>
        <form onSubmit={submit} noValidate className="flex flex-col gap-3">
          <DialogHeader>
            <DialogTitle>Reorder policy — {policy.displayName}</DialogTitle>
            <DialogDescription>
              At {policy.locationCode ?? 'this location'}. A reorder point of 0 silences low-stock alerts and suggestions for this item here.
            </DialogDescription>
          </DialogHeader>
          <QuantityInput id="policy-point" label="Reorder point" required value={point} onValueChange={setPoint} {...options} allowsFractional={unit.allowsFractional} error={fieldError.point} />
          <QuantityInput id="policy-quantity" label="Fixed reorder quantity" value={quantity} onValueChange={setQuantity} {...options} allowsFractional={unit.allowsFractional} hint="Optional — leave blank to let the server work it out from use." error={fieldError.quantity} />
          <SelectField id="policy-supplier" label="Preferred supplier" value={supplierId} onChange={(e) => setSupplierId(e.target.value)}>
            <option value="">None</option>
            {suppliers.map((s) => (<option key={s.id} value={s.id}>{s.name}</option>))}
          </SelectField>
          {error && <p role="alert" className="text-sm font-semibold text-danger-ink">{error}</p>}
          <DialogFooter>
            <Button variant="secondary" onClick={onClose} disabled={pending}>Cancel</Button>
            <Button type="submit" disabled={pending} aria-busy={pending}>{pending ? 'Saving…' : 'Save policy'}</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
