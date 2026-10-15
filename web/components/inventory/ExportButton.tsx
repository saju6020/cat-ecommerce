'use client';

import * as React from 'react';
import { Download } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { downloadInventoryFile } from '@/lib/api/inventory-download';
import { describeInventoryError } from '@/lib/api/errors';

/**
 * `components/inventory/ExportButton.tsx` - HO-IMP-002 3.1. A branch-scoped CSV from the SERVER. An
 * anchor cannot send `X-Branch-Id`, so this requests the file with the CSRF and branch headers
 * (`downloadInventoryFile`) and saves what the server returned. A refusal, including the `413`
 * asking for a narrower range, is shown beside the button in plain language.
 */
export function ExportButton({
  path,
  fallbackName,
  label = 'Export CSV',
}: {
  path: string;
  fallbackName: string;
  label?: string;
}) {
  const [busy, setBusy] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);

  async function run() {
    setBusy(true);
    setError(null);
    try {
      await downloadInventoryFile(path, fallbackName);
    } catch (failure) {
      setError(describeInventoryError(failure));
    } finally {
      setBusy(false);
    }
  }

  return (
    <span className="inline-flex max-w-sm flex-col items-start gap-1">
      <Button variant="secondary" onClick={() => void run()} disabled={busy} aria-busy={busy}>
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
