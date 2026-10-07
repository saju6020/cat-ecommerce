'use client';

import { CatProfileFrame, type CatProfileTab } from '@/components/cat/CatProfileFrame';
import { LoadingState } from '@/components/common/LoadingState';
import { ErrorState } from '@/components/common/ErrorState';
import { EmptyState } from '@/components/common/EmptyState';
import { useCat } from '@/lib/hooks/useCat';
import { useCatVaccinationStatus } from '@/lib/hooks/useCatVaccinationStatus';
import { ApiNotFoundError } from '@/lib/api/client';

/**
 * `app/_components/cat/CatProfileDataShell.tsx` — HO-CAT-002 §8.2 rules 4 and 5, ARCHITECTURE
 * gate second pass. Lives under `app/` (a leading-underscore folder, which Next.js excludes from
 * routing — `app/_components/` is a plain module directory, not a route segment) rather than
 * under `components/`, because it **fetches**: it owns `useCat`/`useCatVaccinationStatus` and the
 * loading/404/error branches, which is composition-layer work per §8.2's layer table
 * ("app/ route segments, layouts, page composition — thin" is the layer allowed to wire a hook's
 * result into a presentational component; "components/ presentational + form components, no data
 * fetching" is not).
 *
 * Shared by both `[catId]/layout.tsx` Server Components (staff and portal) — each just awaits
 * `params` and renders this with its own `basePath`/`tabs`, so no child page re-fetches the
 * profile just to show its own slice of it, and neither layout repeats the loading/404/error
 * branches. `components/cat/CatProfileFrame.tsx` is the purely-presentational half this renders
 * once the query has data — see that file's doc comment for the rest of the split's reasoning.
 */
export interface CatProfileDataShellProps {
  catId: string;
  basePath: string;
  tabs: CatProfileTab[];
  children: React.ReactNode;
}

export function CatProfileDataShell({ catId, basePath, tabs, children }: CatProfileDataShellProps) {
  const catQuery = useCat(catId);
  const vaccinationStatusQuery = useCatVaccinationStatus(catId);

  if (catQuery.isLoading) {
    return <LoadingState variant="card" label="Loading cat profile…" />;
  }

  if (catQuery.error instanceof ApiNotFoundError) {
    return (
      <EmptyState
        title="This cat couldn't be found."
        description="It may have been removed, or the link may be wrong."
      />
    );
  }

  // HO-ACC-002 ARCHITECTURE gate fourth pass, HIGH, integrator-authorised edit (recorded in
  // HO-ACC-002 §1 Amended and HO-CAT-002 §1 Amended) -- `isError` alone used to swap this whole
  // shell (and every cat form nested under it: edit, weights, vaccinations, ...) out for
  // ErrorState, discarding an unsaved draft, even when TanStack Query still holds perfectly good,
  // if stale, `data` (its own documented refetch-failure behaviour: a failed background refetch
  // keeps the last-known-good data alongside `isError: true`). The realistic trigger this closes:
  // a staff member's idle lock takes over on a focus event, and a concurrent refetch of THIS SAME
  // cat query (independently triggered by that same focus event) gets its own 401 before the lock
  // has finished taking hold. Only actually show the error screen when there is nothing else to
  // show.
  if (catQuery.isError && !catQuery.data) {
    return (
      <ErrorState
        title="We couldn't load this cat's profile just now."
        onRetry={() => catQuery.refetch()}
      />
    );
  }

  if (!catQuery.data) return null;

  return (
    <CatProfileFrame
      cat={catQuery.data}
      vaccinationStatus={vaccinationStatusQuery.data?.overallStatus}
      basePath={basePath}
      tabs={tabs}
    >
      {children}
    </CatProfileFrame>
  );
}
