'use client';

import { CatProfileHeader } from '@/components/cat/CatProfileHeader';
import { LoadingState } from '@/components/common/LoadingState';
import { useCat } from '@/lib/hooks/useCat';

/** The full cat header (UI-7, HO-CAT-002) for an Appointments screen: loading and unavailable
 *  states are stated, never silent — a missing header must not look like a cat with no alerts. */
export function CatHeader({ catId }: { catId: string }) {
  const { data, isLoading, isError } = useCat(catId);
  if (isLoading) return <LoadingState variant="card" rows={2} label="Loading the cat’s details…" />;
  if (isError || !data) {
    return (
      <p
        role="status"
        className="rounded-[var(--radius-md)] border border-border bg-surface p-3 text-sm text-ink-muted"
      >
        The cat’s details couldn’t be loaded, so allergies and alerts are not shown here.
      </p>
    );
  }
  return <CatProfileHeader cat={data} />;
}
