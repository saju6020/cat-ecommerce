import { Info } from 'lucide-react';
import type { InvoicePreview as Preview, InvoicePreviewLine } from '@/lib/api/billing-staff';
import { Amount } from './Amount';
import { ResponsiveTable, type Column } from './ResponsiveTable';

/**
 * `components/billing/InvoicePreview.tsx` — HO-BIL-002 §11.2 (US-BIL-07). The server's preview: lines
 * with the tax class code and **rate**, net, tax, gross, then the totals — labelled *"Estimate at
 * today's tax rates. Rates are fixed when the invoice is issued."* (ADR-FE-080: the browser computes
 * none of it). While a new preview loads, the previous figures stay; the totals region is
 * `aria-live="polite"` so a recompute is announced.
 */
export function InvoicePreview({
  preview,
  isRecomputing,
  isError,
  hasSelection,
  onRetry,
}: {
  preview: Preview | undefined;
  isRecomputing: boolean;
  isError: boolean;
  hasSelection: boolean;
  onRetry: () => void;
}) {
  const columns: Array<Column<InvoicePreviewLine>> = [
    { id: 'description', header: 'Item', primary: true, cell: (l) => l.description },
    { id: 'line', header: 'Business line', cell: (l) => l.businessLine },
    { id: 'qty', header: 'Qty', numeric: true, cell: (l) => l.quantity },
    { id: 'unit', header: 'Unit price', numeric: true, cell: (l) => <Amount money={l.unitPrice} /> },
    { id: 'class', header: 'Tax class', cell: (l) => l.taxClassCode ?? '—' },
    { id: 'rate', header: 'Rate', numeric: true, cell: (l) => (l.taxRatePercent ? `${l.taxRatePercent}%` : '—') },
    { id: 'net', header: 'Net', numeric: true, cell: (l) => <Amount money={l.netAmount} /> },
    { id: 'tax', header: 'Tax', numeric: true, cell: (l) => <Amount money={l.taxAmount} /> },
    { id: 'gross', header: 'Gross', numeric: true, cell: (l) => <Amount money={l.grossAmount} /> },
  ];

  return (
    <div className="flex min-w-0 flex-col gap-3">
      <p className="flex items-start gap-2 rounded-[var(--radius-sm)] border border-border bg-warning-bg p-3 text-sm font-semibold text-warning-ink">
        <Info size={16} className="mt-0.5 flex-none" aria-hidden="true" />
        Estimate at today&rsquo;s tax rates. Rates are fixed when the invoice is issued.
      </p>
      {!hasSelection ? (
        <p className="text-sm text-ink-muted">Tick the charges to include and the preview appears here.</p>
      ) : isError && !preview ? (
        <div role="alert" className="text-sm text-danger-ink">
          The preview could not be loaded.{' '}
          <button type="button" className="font-semibold underline" onClick={onRetry}>
            Try again
          </button>
        </div>
      ) : (
        <>
          {preview && preview.problems.length > 0 && (
            <ul role="alert" className="flex flex-col gap-1 text-sm font-semibold text-danger-ink">
              {preview.problems.map((problem) => (
                <li key={problem}>{problem}</li>
              ))}
            </ul>
          )}
          <ResponsiveTable
            caption="Invoice preview"
            columns={columns}
            rows={preview?.lines}
            getRowId={(l) => l.chargeId}
            isLoading={!preview}
            emptyState={{ title: 'No lines.' }}
          />
          <div aria-live="polite" aria-busy={isRecomputing} className="flex flex-col gap-1">
            <span className="sr-only">{isRecomputing ? 'Updating the totals…' : 'Totals are up to date.'}</span>
            {preview && (
              <dl className="ml-auto grid w-full max-w-xs grid-cols-[1fr_auto] gap-x-4 gap-y-1 text-sm">
                <dt className="text-ink-muted">Net</dt>
                <dd className="text-right"><Amount money={preview.totals.net} /></dd>
                <dt className="text-ink-muted">Discounts</dt>
                <dd className="text-right"><Amount money={preview.totals.discount} /></dd>
                <dt className="text-ink-muted">Tax</dt>
                <dd className="text-right"><Amount money={preview.totals.tax} /></dd>
                <dt className="font-semibold text-ink">Estimated total</dt>
                <dd className="text-right font-semibold"><Amount money={preview.totals.total} /></dd>
              </dl>
            )}
            {isRecomputing && (
              <p aria-hidden="true" className="text-right text-xs text-ink-muted">
                Updating…
              </p>
            )}
          </div>
        </>
      )}
    </div>
  );
}
