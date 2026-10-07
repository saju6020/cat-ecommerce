import { CatProfileDataShell } from '@/app/_components/cat/CatProfileDataShell';
import type { CatProfileTab } from '@/components/cat/CatProfileFrame';

/**
 * Portal `[catId]/layout.tsx` — not named in HO-CAT-002 §8.3's route table (which lists only
 * `page.tsx` and `medical-history/page.tsx` under portal's `[catId]/`), added here so UI-7's
 * guarantee holds on the portal's two cat screens the same way the staff layout guarantees it on
 * staff's ten. A Server Component composing the same `CatProfileDataShell` the staff layout uses
 * (§8.2 rules 4 and 5; §4.2 — portal screens are presentation variants, not a second
 * implementation).
 */
const TABS: CatProfileTab[] = [
  { href: '', label: 'Profile' },
  { href: '/medical-history', label: 'Medical history' },
];

export default async function PortalCatLayout({
  children,
  params,
}: {
  children: React.ReactNode;
  params: Promise<{ catId: string }>;
}) {
  const { catId } = await params;

  return (
    <CatProfileDataShell catId={catId} basePath={`/portal/cats/${catId}`} tabs={TABS}>
      {children}
    </CatProfileDataShell>
  );
}
