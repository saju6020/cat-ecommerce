'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { CircleCheck, TriangleAlert, WifiOff } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { useConnectivity } from '@/lib/hooks/useConnectivity';
import { useStoredSnapshot } from '@/lib/hooks/useOfflineSnapshot';
import { useStalenessLevel, type StalenessLevel } from '@/lib/hooks/useStalenessLevel';
import { isSnapshotUsable } from '@/lib/offline/snapshot';
import { timeOnlyInZone } from '@/lib/format/time';

const RECOVERED_VISIBLE_MS = 5_000;
const DEFAULT_ZONE = 'Asia/Dhaka';

/** The age sentence, announced when the copy crosses 15 and 60 minutes (never per tick). */
export function stalenessNote(level: StalenessLevel): string {
  if (level === 'over60') return ' — over an hour ago, so check anything recent with a colleague';
  if (level === 'over15') return ' — more than 15 minutes ago';
  return '';
}

/**
 * `components/offline/OfflineBanner.tsx` — HO-XCT-002 §11.3, §8.6. One banner in the `(staff)`
 * layout, the first thing inside `<main>`, so a screen-reader user meets it before the data. A
 * polite `role="status"`, text first (the colour only repeats what the words already say), sticky,
 * and never covering a control. Four states: offline, degraded (the server answers with errors, so
 * the offline list is NOT offered), a 5-second "back online", and nothing at all.
 */
export function OfflineBanner() {
  const { status, recoveredAt, forceCheck } = useConnectivity();
  const { record } = useStoredSnapshot();
  const pathname = usePathname();
  const [checking, setChecking] = useState(false);
  const [recoveredVisible, setRecoveredVisible] = useState(false);

  const savedAtMs = record ? Date.parse(record.fetchedAt) : null;
  const staleness = useStalenessLevel(status === 'offline' ? savedAtMs : null);

  useEffect(() => {
    if (recoveredAt === null) return;
    setRecoveredVisible(true);
    const timeout = setTimeout(() => setRecoveredVisible(false), RECOVERED_VISIBLE_MS);
    return () => clearTimeout(timeout);
  }, [recoveredAt]);

  async function tryNow() {
    setChecking(true);
    try {
      await forceCheck();
    } finally {
      setChecking(false);
    }
  }

  let content: React.ReactNode = null;

  if (status === 'offline') {
    const usable = record !== undefined && isSnapshotUsable(record);
    const zone = record?.body.branch.timeZoneId ?? DEFAULT_ZONE;
    content = (
      <div className="flex flex-wrap items-center gap-x-4 gap-y-2 bg-ink px-4 py-3 text-sm text-white">
        <WifiOff size={18} className="flex-none" aria-hidden="true" />
        <p className="min-w-[220px] flex-1">
          <strong className="font-semibold">You&rsquo;re offline.</strong>{' '}
          {usable && record
            ? `Showing today's lists saved at ${timeOnlyInZone(record.fetchedAt, zone)} (${zone})${stalenessNote(staleness)}. Changes are switched off until the connection returns.`
            : "There's no saved copy of today's lists on this device, so only what's already on screen is available. Changes are switched off until the connection returns."}
        </p>
        <div className="flex flex-wrap gap-2">
          {usable && (
            <Link
              href={`/staff/offline?from=${encodeURIComponent(pathname)}`}
              className="inline-flex h-8 items-center rounded-[var(--radius-sm)] bg-white px-3 text-sm font-semibold text-ink no-underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white"
            >
              Open today&rsquo;s offline list
            </Link>
          )}
          <Button
            variant="secondary"
            size="sm"
            onClick={() => void tryNow()}
            disabled={checking}
            aria-busy={checking}
          >
            {checking ? 'Checking…' : 'Try now'}
          </Button>
        </div>
      </div>
    );
  } else if (status === 'degraded') {
    content = (
      <div className="flex items-center gap-3 bg-warning-bg px-4 py-3 text-sm text-warning-ink">
        <TriangleAlert size={18} className="flex-none" aria-hidden="true" />
        <p>
          <strong className="font-semibold">The server is having trouble.</strong> Some actions may
          fail. If one does, what you typed stays on the page — try it again in a minute.
        </p>
      </div>
    );
  } else if (recoveredVisible) {
    content = (
      <div className="flex items-center gap-3 bg-accent-soft px-4 py-3 text-sm text-accent-ink">
        <CircleCheck size={18} className="flex-none" aria-hidden="true" />
        <p>
          <strong className="font-semibold">Back online</strong> — updated just now.
        </p>
      </div>
    );
  }

  // The live region is always in the DOM (a region inserted together with its text is not
  // reliably announced); only its content changes.
  return (
    <div
      role="status"
      aria-live="polite"
      data-testid="connectivity-banner"
      // Spans <main>'s own padding (px-6 py-7) so the bar runs edge to edge; collapsed when empty.
      className={content ? 'sticky top-0 z-30 -mx-6 -mt-7 mb-4' : undefined}
    >
      {content}
    </div>
  );
}
