import { CatProfileDataShell } from '@/app/_components/cat/CatProfileDataShell';
import type { CatProfileTab } from '@/components/cat/CatProfileFrame';

/**
 * `app/(staff)/staff/cats/[catId]/layout.tsx` — HO-CAT-002 §8.3. A Server Component (§8.2 rule
 * 5 — `"use client"` at the smallest boundary that needs it, not on a layout): it only awaits
 * `params` and composes `CatProfileDataShell` (a composition-layer wrapper under `app/`, not
 * `components/` — §8.2 rule 4), which owns every hook, the loading/404/error branches, the
 * header and the tab nav. See that file's doc comment for why UI-7 depends on the shell existing
 * here at all, not on this file being a client component itself.
 */
const TABS: CatProfileTab[] = [
  { href: '', label: 'Overview' },
  { href: '/edit', label: 'Edit' },
  { href: '/identification', label: 'Identification' },
  { href: '/medical', label: 'Medical' },
  { href: '/weights', label: 'Weights' },
  { href: '/vaccinations', label: 'Vaccinations' },
  { href: '/vaccination-status', label: 'Vaccination status' },
  { href: '/parasite-control', label: 'Parasite control' },
  { href: '/photos', label: 'Photos' },
  { href: '/cage-card', label: 'Cage card' },
];

export default async function CatLayout({
  children,
  params,
}: {
  children: React.ReactNode;
  params: Promise<{ catId: string }>;
}) {
  const { catId } = await params;

  return (
    <CatProfileDataShell catId={catId} basePath={`/staff/cats/${catId}`} tabs={TABS}>
      {children}
    </CatProfileDataShell>
  );
}
