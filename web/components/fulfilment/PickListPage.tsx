'use client';

import { useRouter, useSearchParams } from 'next/navigation';
import { useState } from 'react';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { QueryBoundary } from '@/components/billing/QueryBoundary';
import { StaffOrdersTabs } from '@/components/orders/StaffOrdersTabs';
import { PrintSheet } from '@/components/print/PrintSheet';
import { Button } from '@/components/ui/button';
import { ApiForbiddenError } from '@/lib/api/client';
import { describeOrderingError } from '@/lib/api/errors';
import type { PickList } from '@/lib/api/staff-orders';
import { formatDateOnly, formatInZone } from '@/lib/format/time';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { usePickList, useStaffOrderQueue, useStartPacking } from '@/lib/hooks/useStaffOrders';

/**
 * `components/fulfilment/PickListPage.tsx` — ⭐ HO-ORD-002 §11.10, US-ORD-10 AC1, ADR-FE-094. The stock to
 * pull for the chosen orders (or every confirmed and packing order at the branch), **grouped by location,
 * then batch**: SKU, batch number, expiry, total quantity and the orders that need it. **Print** uses the
 * shared A4 `PrintSheet`; **Start packing** moves the listed confirmed orders to packing, one request
 * per order, with progress and each order's own failure listed. Orders are chosen in the page; the
 * choice is in the URL (`?orderIds=`).
 */
function Sheet({ list, zone }: { list: PickList; zone: string }) {
  if (list.groups.length === 0) {
    return <p className="text-sm text-ink-muted">Nothing to pick. There are no confirmed or packing orders.</p>;
  }
  return (
    <div className="flex flex-col gap-5">
      <p className="text-xs text-ink-muted">Generated {formatInZone(list.generatedOn, zone)}</p>
      {list.groups.map((group) => (
        <section key={group.locationId} aria-labelledby={`pick-${group.locationId}`}>
          <h2 id={`pick-${group.locationId}`} className="mb-2 border-b border-border-strong pb-1 text-base font-semibold">
            {group.locationName}
          </h2>
          <div tabIndex={0} role="region" aria-label={`Batches to pick from ${group.locationName}`} className="overflow-x-auto print:overflow-visible">
          <table className="w-full border-collapse text-left text-sm">
            <caption className="sr-only">Batches to pick from {group.locationName}</caption>
            <thead>
              <tr className="border-b border-border">
                <th scope="col" className="py-1 pr-2">Product</th>
                <th scope="col" className="py-1 pr-2">Batch</th>
                <th scope="col" className="py-1 pr-2">Expires</th>
                <th scope="col" className="py-1 pr-2 text-right">Quantity</th>
                <th scope="col" className="py-1">Orders</th>
              </tr>
            </thead>
            <tbody>
              {group.batches.map((batch) => (
                <tr key={batch.batchId} className="border-b border-border align-top last:border-0">
                  <td className="py-1.5 pr-2 [overflow-wrap:anywhere]">{`${batch.productName} ${batch.variantLabel}`.trim()}</td>
                  <td className="py-1.5 pr-2 font-semibold">{batch.batchNumber}</td>
                  <td className="py-1.5 pr-2">{batch.expiresOn ? formatDateOnly(batch.expiresOn) : '—'}</td>
                  <td className="py-1.5 pr-2 text-right font-semibold tabular-nums">{batch.quantity}</td>
                  <td className="py-1.5 [overflow-wrap:anywhere]">{batch.orders.map((o) => `${o.orderNumber} (${o.quantity})`).join(', ')}</td>
                </tr>
              ))}
            </tbody>
          </table>
          </div>
        </section>
      ))}
    </div>
  );
}

export function PickListPage() {
  const router = useRouter();
  const params = useSearchParams();
  const zone = useBranchTimeZone();
  const chosen = (params.get('orderIds') ?? '').split(',').filter(Boolean);
  const list = usePickList(chosen.length > 0 ? chosen : undefined);
  const confirmed = useStaffOrderQueue({ status: 'Confirmed', pageNumber: 1, pageSize: 100 });
  const startPacking = useStartPacking();
  const [selection, setSelection] = useState<string[]>(chosen);
  const [progress, setProgress] = useState<{ done: number; total: number; failures: Array<{ number: string; message: string }> } | null>(null);
  const [running, setRunning] = useState(false);

  if (list.error instanceof ApiForbiddenError) return <AccessRefusedState error={list.error} />;

  const confirmedOrders = confirmed.data?.items ?? [];
  const listed = new Set(list.data?.groups.flatMap((g) => g.batches.flatMap((b) => b.orders.map((o) => o.orderNumber))) ?? []);
  const toPack = confirmedOrders.filter((o) => listed.has(o.orderNumber));

  async function packAll() {
    setRunning(true);
    const failures: Array<{ number: string; message: string }> = [];
    let done = 0;
    setProgress({ done, total: toPack.length, failures });
    for (const order of toPack) {
      try {
        await startPacking.mutateAsync(order.id);
      } catch (error) {
        failures.push({ number: order.orderNumber, message: describeOrderingError(error) });
      }
      done += 1;
      setProgress({ done, total: toPack.length, failures: [...failures] });
    }
    setRunning(false);
  }

  const apply = () => router.replace(selection.length > 0 ? `?orderIds=${selection.join(',')}` : '?');

  return (
    <PrintSheet
      title="Pick list"
      description="The stock to pull for the orders waiting to be packed."
      back={{ href: '/staff/orders', label: 'Orders' }}
      size="A4"
      label="Pick list"
      controls={
        <div className="flex flex-col gap-4">
          <StaffOrdersTabs />
          <details className="rounded-[var(--radius-md)] border border-border bg-surface p-3">
            <summary className="cursor-pointer text-sm font-semibold text-ink">
              Choose orders {chosen.length > 0 ? `(${chosen.length} chosen)` : '(all confirmed and packing orders)'}
            </summary>
            <fieldset className="m-0 mt-3 flex flex-col gap-2 border-0 p-0">
              <legend className="sr-only">Confirmed orders</legend>
              {confirmedOrders.length === 0 && <p className="text-sm text-ink-muted">No confirmed orders are waiting.</p>}
              {confirmedOrders.map((o) => (
                <label key={o.id} className="flex items-center gap-2 text-sm">
                  <input
                    type="checkbox"
                    className="h-4 w-4 accent-[var(--accent)]"
                    checked={selection.includes(o.id)}
                    onChange={(e) => setSelection((cur) => (e.target.checked ? [...cur, o.id] : cur.filter((id) => id !== o.id)))}
                  />
                  {o.orderNumber} — {o.customerName}
                </label>
              ))}
            </fieldset>
            <div className="mt-3 flex flex-wrap gap-2">
              <Button size="sm" onClick={apply}>Build the pick list</Button>
              <Button size="sm" variant="secondary" onClick={() => { setSelection([]); router.replace('?'); }}>Use all orders</Button>
            </div>
          </details>
          <div className="flex flex-wrap items-center gap-3">
            <Button onClick={() => void packAll()} disabled={running || toPack.length === 0} aria-busy={running}>
              {running ? 'Starting…' : `Start packing ${toPack.length} order${toPack.length === 1 ? '' : 's'}`}
            </Button>
            {progress && (
              <p role="status" className="text-sm font-semibold text-ink">
                {progress.done} of {progress.total} started{progress.failures.length > 0 ? `, ${progress.failures.length} could not be started` : ''}.
              </p>
            )}
          </div>
          {progress && progress.failures.length > 0 && (
            <ul aria-label="Orders that could not be started" className="m-0 list-none rounded-[var(--radius-sm)] bg-danger-bg p-3 text-sm text-danger-ink">
              {progress.failures.map((f) => (
                <li key={f.number}><strong>{f.number}</strong>: {f.message}</li>
              ))}
            </ul>
          )}
        </div>
      }
    >
      <QueryBoundary query={list} loadingLabel="Building the pick list…">
        {(data) => <Sheet list={data} zone={zone} />}
      </QueryBoundary>
    </PrintSheet>
  );
}
