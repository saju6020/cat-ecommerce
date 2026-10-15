'use client';

import { useState } from 'react';
import { Download } from 'lucide-react';
import { SelectField, TextField } from '@/components/billing/Fields';
import { Button } from '@/components/ui/button';
import { describeOrderingError } from '@/lib/api/errors';
import { downloadOrderingFile, manifestPath } from '@/lib/api/staff-orders';
import { todayInZone } from '@/lib/format/time';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { useDispatchCouriers } from '@/lib/hooks/useStaffOrders';

/**
 * `components/fulfilment/ManifestDownload.tsx` — HO-ORD-002 §11.10, US-ORD-11 AC2, UI-4 (CSV where the
 * API provides it). Choose a courier and a dispatch date and download the manifest the courier collects
 * with the parcels. The file is fetched through the one client download path (`downloadOrderingFile`:
 * CSRF and `X-Branch-Id` attached; no second `fetch` site), and a refusal is shown here in words.
 */
export function ManifestDownload() {
  const couriers = useDispatchCouriers();
  const zone = useBranchTimeZone();
  const [courierId, setCourierId] = useState('');
  const [date, setDate] = useState(() => todayInZone(zone));
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function download() {
    setBusy(true);
    setError(null);
    try {
      await downloadOrderingFile(manifestPath(courierId, date), `manifest-${date}.csv`);
    } catch (failure) {
      setError(describeOrderingError(failure));
    } finally {
      setBusy(false);
    }
  }

  return (
    <details className="rounded-[var(--radius-md)] border border-border bg-surface p-3">
      <summary className="cursor-pointer text-sm font-semibold text-ink">Courier manifest (CSV)</summary>
      <div className="mt-3 grid items-end gap-3 sm:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_auto]">
        <SelectField id="manifest-courier" label="Courier" value={courierId} onChange={(e) => setCourierId(e.target.value)}>
          <option value="">Choose a courier</option>
          {(couriers.data ?? []).map((courier) => (
            <option key={courier.id} value={courier.id}>{courier.name}</option>
          ))}
        </SelectField>
        <TextField id="manifest-date" label="Dispatched on" type="date" value={date} onChange={(e) => setDate(e.target.value)} />
        <Button onClick={() => void download()} disabled={busy || !courierId || !date} aria-busy={busy}>
          <Download size={14} aria-hidden="true" />
          {busy ? 'Preparing…' : 'Download manifest'}
        </Button>
      </div>
      {error && <p role="alert" className="mt-2 text-sm font-semibold text-danger-ink">{error}</p>}
    </details>
  );
}
