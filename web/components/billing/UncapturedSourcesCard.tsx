'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import type { CaptureResult, UncapturedSource } from '@/lib/api/billing-staff';
import { describeBillingError } from '@/lib/api/errors';
import { formatInZone } from '@/lib/format/time';
import { Section } from './PageHeader';

/**
 * `components/billing/UncapturedSourcesCard.tsx` — HO-BIL-002 §11.1 (US-BIL-02). Completed work that
 * has no charge yet (the pull path, ADR-178). **Capture** asks the server to record it and shows what
 * came back — how many charges were captured, and every *skipped* item with its reason — so a source
 * that cannot be billed (say, a service with no price) is never silently dropped.
 */
export function UncapturedSourcesCard({
  sources,
  zone,
  isLoading,
  isError,
  onRetry,
  onCapture,
}: {
  sources: UncapturedSource[] | undefined;
  zone: string;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
  onCapture: (source: UncapturedSource) => Promise<CaptureResult>;
}) {
  const [results, setResults] = useState<Record<string, CaptureResult | string>>({});
  const [busy, setBusy] = useState<string | null>(null);

  async function capture(source: UncapturedSource) {
    setBusy(source.sourceId);
    try {
      const result = await onCapture(source);
      setResults((current) => ({ ...current, [source.sourceId]: result }));
    } catch (failure) {
      setResults((current) => ({ ...current, [source.sourceId]: describeBillingError(failure) }));
    } finally {
      setBusy(null);
    }
  }

  const shown = [
    ...(sources ?? []),
    ...Object.keys(results)
      .filter((id) => !(sources ?? []).some((s) => s.sourceId === id))
      .map((id) => ({ sourceKind: '', sourceId: id, customerId: '', completedOn: '', label: 'Captured item' })),
  ];

  return (
    <Section title="Uncaptured charges" description="Completed work with no charge yet, from the last 30 days.">
      {isLoading ? (
        <p role="status" className="text-sm text-ink-muted">Loading…</p>
      ) : isError ? (
        <p role="alert" className="text-sm text-danger-ink">
          We couldn’t load these.{' '}
          <button type="button" className="font-semibold underline" onClick={onRetry}>Try again</button>
        </p>
      ) : shown.length === 0 ? (
        <p className="text-sm text-ink-muted">Everything completed has been captured.</p>
      ) : (
        <ul className="flex flex-col gap-3">
          {shown.map((source) => {
            const result = results[source.sourceId];
            return (
              <li key={source.sourceId} className="flex min-w-0 flex-col gap-2 rounded-[var(--radius-sm)] border border-border p-3">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div className="min-w-0 text-sm">
                    <p className="font-semibold text-ink [overflow-wrap:anywhere]">{source.label}</p>
                    {source.completedOn && (
                      <p className="text-ink-muted">Completed {formatInZone(source.completedOn, zone)}</p>
                    )}
                  </div>
                  {!result && (
                    <Button variant="secondary" size="sm" disabled={busy !== null} aria-busy={busy === source.sourceId} onClick={() => void capture(source)}>
                      {busy === source.sourceId ? 'Capturing…' : 'Capture'}
                      <span className="sr-only"> {source.label}</span>
                    </Button>
                  )}
                </div>
                {typeof result === 'string' && <p role="alert" className="text-sm font-semibold text-danger-ink">{result}</p>}
                {result && typeof result !== 'string' && (
                  <div role="status" className="text-sm">
                    <p className="font-semibold text-ink">
                      {result.captured === 1 ? '1 charge captured.' : `${result.captured} charges captured.`}
                    </p>
                    {result.skipped.map((s) => (
                      <p key={s.reason} className="text-warning-ink">Skipped: {s.reason}</p>
                    ))}
                  </div>
                )}
              </li>
            );
          })}
        </ul>
      )}
    </Section>
  );
}
