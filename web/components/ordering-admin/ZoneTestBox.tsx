'use client';

import { useState } from 'react';
import { TextField } from '@/components/billing/Fields';
import { Section } from '@/components/billing/PageHeader';
import { Button } from '@/components/ui/button';
import { describeOrderingError } from '@/lib/api/errors';
import { useResolveZone } from '@/lib/hooks/useOrderingAdmin';

/**
 * `components/ordering-admin/ZoneTestBox.tsx` — HO-ORD-002 §11.12, US-ORD-06 AC1. *Test an address*: type
 * a district (and optionally a postal code) and the server names the zone that would serve it — or says
 * none does. It tests the zones as saved, not the unsaved form.
 */
export function ZoneTestBox() {
  const resolve = useResolveZone();
  const [district, setDistrict] = useState('');
  const [postalCode, setPostalCode] = useState('');
  return (
    <Section title="Test an address" description="Which zone serves this address? Tests the zones as saved.">
      <form
        className="grid items-end gap-3 sm:grid-cols-[1fr_1fr_auto]"
        onSubmit={(e) => {
          e.preventDefault();
          resolve.mutate({ district: district.trim(), postalCode: postalCode.trim() || undefined });
        }}
      >
        <TextField id="zone-test-district" label="District" required value={district} onChange={(e) => setDistrict(e.target.value)} />
        <TextField id="zone-test-postal" label="Postal code" value={postalCode} onChange={(e) => setPostalCode(e.target.value)} />
        <Button type="submit" variant="secondary" disabled={!district.trim() || resolve.isPending}>Test</Button>
      </form>
      <div role="status" aria-live="polite" className="text-sm font-semibold">
        {resolve.data && (resolve.data.matched ? <span className="text-accent-ink">Served by “{resolve.data.zoneName}”.</span> : <span className="text-warning-ink">No zone serves this address. Customers there can only collect from a branch.</span>)}
        {resolve.isError && <span className="text-danger-ink">{describeOrderingError(resolve.error)}</span>}
      </div>
    </Section>
  );
}
