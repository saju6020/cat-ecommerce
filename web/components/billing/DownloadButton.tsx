'use client';

import { useState } from 'react';
import { Download } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { downloadBillingFile } from '@/lib/api/billing-staff';
import { describeBillingError } from '@/lib/api/errors';

/**
 * `components/billing/DownloadButton.tsx` — a staff download (invoice or credit-note PDF, invoice or
 * payment CSV). Branch-scoped files need `X-Branch-Id`, which an anchor cannot send, so this fetches
 * with the CSRF and branch headers (`downloadBillingFile`) and saves the result; a refusal is shown
 * next to the button in plain language instead of navigating away to an error body.
 */
export function DownloadButton({ path, fallbackName, label }: { path: string; fallbackName: string; label: string }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function run() {
    setBusy(true);
    setError(null);
    try {
      await downloadBillingFile(path, fallbackName);
    } catch (failure) {
      setError(describeBillingError(failure));
    } finally {
      setBusy(false);
    }
  }

  return (
    <span className="inline-flex flex-col gap-1">
      <Button variant="secondary" size="sm" onClick={() => void run()} disabled={busy} aria-busy={busy}>
        <Download size={14} aria-hidden="true" />
        {busy ? 'Preparing…' : label}
      </Button>
      {error && (
        <span role="alert" className="text-xs font-semibold text-danger-ink">
          {error}
        </span>
      )}
    </span>
  );
}
