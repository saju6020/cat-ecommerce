'use client';

import Link from 'next/link';
import { Button } from '@/components/ui/button';
import { describeApiError } from '@/lib/api/errors';
import { useState } from 'react';
import type { PersonalDataExport } from '@/lib/api/account';

/**
 * `components/account/PersonalDataExportCard.tsx` — US-ACC-13. The export is never put into the
 * query cache (the page's own `useExportPersonalData` is a plain mutation) — it goes straight
 * from the response to a Blob URL, downloaded, and the URL is revoked immediately after (U-J1).
 *
 * ARCH gate HIGH 3 (first pass) — takes `onExport`/`isPending` as props; the page
 * (`app/(portal)/portal/account/data/page.tsx`) wires `useExportPersonalData()`.
 */
export interface PersonalDataExportCardProps {
  onExport: () => Promise<PersonalDataExport>;
  isPending: boolean;
}

export function PersonalDataExportCard({ onExport, isPending }: PersonalDataExportCardProps) {
  const [error, setError] = useState<string | null>(null);

  async function handleDownload() {
    setError(null);
    try {
      const data = await onExport();
      const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = 'purrcare-my-data.json';
      document.body.appendChild(anchor);
      anchor.click();
      anchor.remove();
      URL.revokeObjectURL(url);
    } catch (err) {
      setError(describeApiError(err));
    }
  }

  return (
    <div className="flex flex-col gap-4 rounded-[var(--radius-md)] border border-border bg-surface p-6">
      <div>
        <h2 className="font-display text-lg font-semibold text-ink">Your data</h2>
        <p className="mt-1 text-sm text-ink-muted">
          Download a copy of your profile, addresses, notification preferences and your cats&rsquo;
          basic details, as a JSON file.
        </p>
      </div>
      {error && (
        <p role="alert" className="text-sm font-semibold text-danger-ink">
          {error}
        </p>
      )}
      <Button onClick={handleDownload} disabled={isPending} aria-busy={isPending} className="w-fit">
        {isPending ? 'Preparing…' : 'Download my data'}
      </Button>
      <Link
        href="/portal/account/data/delete"
        className="text-sm font-semibold text-accent-ink no-underline hover:underline"
      >
        Delete my account
      </Link>
    </div>
  );
}
