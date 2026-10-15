'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  useDialogReturnFocus,
} from '@/components/ui/dialog';
import { mapClinicalFormErrors } from '@/lib/api/errors';
import { useLabAnalytes } from '@/lib/hooks/useClinicalCatalogue';
import { DIAGNOSTIC_KINDS, diagnosticKindLabel } from '@/lib/format/clinical';
import { MAX_DIAGNOSTIC_DESCRIPTION } from '@/lib/validation/clinical';
import type { DiagnosticKind, OrderDiagnosticRequest } from '@/lib/api/diagnostics';

/**
 * `OrderDiagnosticDialog` — HO-EMR-002 §11.3 (US-CLN-14 AC1). Kind, a description (REQUIRED for
 * Imaging and Other — the server says so as `DiagnosticDescriptionRequired` on that field), and a
 * multi-select of catalogue analytes that belong to the chosen kind. Built on `ui/dialog.tsx`.
 */
export function OrderDiagnosticDialog({
  open,
  onOpenChange,
  catName,
  order,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  catName: string;
  order: (request: OrderDiagnosticRequest) => Promise<unknown>;
}) {
  const returnFocusRef = useDialogReturnFocus(open);
  const [kind, setKind] = useState<DiagnosticKind>('BloodWork');
  const [description, setDescription] = useState('');
  const [analyteIds, setAnalyteIds] = useState<string[]>([]);
  const [errors, setErrors] = useState<{ fields: Record<string, string>; root: string | null }>({
    fields: {},
    root: null,
  });
  const [pending, setPending] = useState(false);
  const analytes = useLabAnalytes({ activeOnly: true, pageSize: 100 }, open);
  const forKind = (analytes.data?.items ?? []).filter((a) => a.diagnosticKind === kind);
  const needsDescription = kind === 'Imaging' || kind === 'Other';

  async function submit() {
    setErrors({ fields: {}, root: null });
    if (needsDescription && !description.trim()) {
      setErrors({
        fields: { description: `Describe the ${diagnosticKindLabel(kind)} study to be done.` },
        root: null,
      });
      return;
    }
    setPending(true);
    try {
      await order({
        kind,
        ...(description.trim() ? { description: description.trim() } : {}),
        analyteIds: needsDescription ? [] : analyteIds,
      });
      setDescription('');
      setAnalyteIds([]);
      onOpenChange(false);
    } catch (error) {
      setErrors(mapClinicalFormErrors(error, { Description: 'description' }));
    } finally {
      setPending(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={(next) => !pending && onOpenChange(next)}>
      <DialogContent
        returnFocusRef={returnFocusRef}
        className="max-h-[92vh] max-w-lg overflow-y-auto"
      >
        <DialogHeader>
          <DialogTitle>Order a test for {catName}</DialogTitle>
          <DialogDescription>
            The order waits as pending until results are entered.
          </DialogDescription>
        </DialogHeader>
        <form
          noValidate
          className="flex flex-col gap-4"
          onSubmit={(event) => {
            event.preventDefault();
            void submit();
          }}
        >
          <div className="flex flex-col gap-1.5">
            <label htmlFor="order-kind" className="text-sm font-semibold text-ink">
              Kind of test
            </label>
            <select
              id="order-kind"
              value={kind}
              onChange={(event) => {
                setKind(event.target.value as DiagnosticKind);
                setAnalyteIds([]);
              }}
              className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
            >
              {DIAGNOSTIC_KINDS.map((value) => (
                <option key={value} value={value}>
                  {diagnosticKindLabel(value)}
                </option>
              ))}
            </select>
          </div>
          <div className="flex flex-col gap-1.5">
            <label htmlFor="order-description" className="text-sm font-semibold text-ink">
              Description{' '}
              {needsDescription ? (
                <span className="text-danger-ink">(required)</span>
              ) : (
                <span className="font-normal text-ink-muted">(optional)</span>
              )}
            </label>
            <textarea
              id="order-description"
              rows={3}
              maxLength={MAX_DIAGNOSTIC_DESCRIPTION}
              value={description}
              onChange={(event) => setDescription(event.target.value)}
              aria-invalid={errors.fields.description ? true : undefined}
              aria-describedby={errors.fields.description ? 'order-description-error' : undefined}
              className="resize-y rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
            />
            {errors.fields.description && (
              <p
                id="order-description-error"
                role="alert"
                className="text-xs font-semibold text-danger-ink"
              >
                {errors.fields.description}
              </p>
            )}
          </div>
          {!needsDescription && (
            <fieldset className="m-0 flex flex-col gap-1.5 border-0 p-0">
              <legend className="text-sm font-semibold text-ink">Tests to run</legend>
              {analytes.isLoading && <p className="text-sm text-ink-muted">Loading tests…</p>}
              {!analytes.isLoading && forKind.length === 0 && (
                <p className="text-sm text-ink-muted">
                  No catalogue tests for this kind. The order can still be placed.
                </p>
              )}
              <div className="grid gap-1.5 min-[420px]:grid-cols-2">
                {forKind.map((analyte) => (
                  <label
                    key={analyte.id}
                    className="inline-flex items-center gap-2 text-sm text-ink"
                  >
                    <input
                      type="checkbox"
                      checked={analyteIds.includes(analyte.id)}
                      onChange={(event) =>
                        setAnalyteIds((current) =>
                          event.target.checked
                            ? [...current, analyte.id]
                            : current.filter((id) => id !== analyte.id),
                        )
                      }
                    />
                    {analyte.name}
                  </label>
                ))}
              </div>
            </fieldset>
          )}
          {errors.root && (
            <p role="alert" className="text-sm font-semibold text-danger-ink">
              {errors.root}
            </p>
          )}
          <DialogFooter>
            <Button variant="secondary" onClick={() => onOpenChange(false)} disabled={pending}>
              Cancel
            </Button>
            <Button type="submit" disabled={pending} aria-busy={pending}>
              {pending ? 'Ordering…' : 'Place order'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
