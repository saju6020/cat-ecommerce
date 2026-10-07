'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { CatProfileHeader } from './CatProfileHeader';
import { cn } from '@/lib/utils';
import type { CatProfile, VaccinationStatus } from '@/lib/api/cats';

/**
 * ⭐ `components/cat/CatProfileFrame.tsx` — HO-CAT-002 §8.2 rules 4 and 5. `CatProfileHeader` is
 * rendered **here**, not in each page, which is what actually guarantees UI-7 and US-CAT-04 AC1
 * (component test E3): every tab under the staff and portal `[catId]/layout.tsx`s gets the
 * header, allergy list and high-risk warning with no way for a future tab to forget them.
 *
 * Purely presentational — `cat` and `vaccinationStatus` are props, not fetched here (rule 4: "a
 * component that imports a hook to fetch its own data cannot be tested or reused"). Still a
 * Client Component (`usePathname`, for the active-tab underline) — rule 5 is about the
 * fetch/render split, not about avoiding `"use client"` altogether; a component that needs
 * browser-only navigation state still needs it, same as before.
 *
 * `app/_components/cat/CatProfileDataShell.tsx` is the composition-layer wrapper that actually
 * owns `useCat`/`useCatVaccinationStatus` and the loading/404/error branches, and renders this
 * component with the result — ARCHITECTURE gate's second pass on HO-CAT-002: the previous version
 * of this file lived here and called those two hooks itself, which satisfied rule 5 (not a
 * layout) but broke rule 4 (a `components/**` file fetching its own data). Renamed from
 * `CatProfileShell` at the same time, to signal the split.
 *
 * `print:hidden` on the header and tab nav — the cage-card tab (US-CAT-11) is the one screen
 * under either layout meant to be printed, and it is a small standalone card, not this header
 * plus the full tab nav. `CageCard.tsx` renders its own (print-only) high-risk warning for
 * exactly this reason — printing this header too would duplicate it. Harmless on the portal,
 * which has no print screen yet.
 */
export interface CatProfileTab {
  /** Relative to `basePath` — `''` for the landing tab, `/edit` etc. otherwise. */
  href: string;
  label: string;
}

export interface CatProfileFrameProps {
  cat: CatProfile;
  vaccinationStatus?: VaccinationStatus;
  /** e.g. `/staff/cats/${catId}` or `/portal/cats/${catId}` — each tab's href is `${basePath}${tab.href}`. */
  basePath: string;
  tabs: CatProfileTab[];
  children: React.ReactNode;
}

export function CatProfileFrame({
  cat,
  vaccinationStatus,
  basePath,
  tabs,
  children,
}: CatProfileFrameProps) {
  const pathname = usePathname();

  return (
    <div className="flex flex-col gap-6">
      <div className="print:hidden">
        <CatProfileHeader cat={cat} vaccinationStatus={vaccinationStatus} />
      </div>

      <nav aria-label="Cat sections" className="border-b border-border print:hidden">
        <ul className="flex flex-wrap gap-1 text-sm">
          {tabs.map((tab) => {
            const href = `${basePath}${tab.href}`;
            const isActive = pathname === href;
            return (
              <li key={tab.href}>
                <Link
                  href={href}
                  aria-current={isActive ? 'page' : undefined}
                  className={cn(
                    'inline-block border-b-2 px-3 py-2 font-semibold no-underline',
                    // Teal stays the colour for actions/navigation even on the warm portal layer
                    // (docs/design/cat-profile/README.md's design-system note) — `--warm` is for
                    // decorative fills (the portal avatar circle) only, never an action's colour.
                    isActive
                      ? 'border-accent text-accent-ink'
                      : 'border-transparent text-ink-muted hover:text-ink',
                  )}
                >
                  {tab.label}
                </Link>
              </li>
            );
          })}
        </ul>
      </nav>

      {children}
    </div>
  );
}
