'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { Button } from '@/components/ui/button';
import { useStaffSession } from '@/lib/hooks/useSession';
import { useDeviceSettings, useStoredSnapshot } from '@/lib/hooks/useOfflineSnapshot';
import { formatInZone, timeOnlyInZone } from '@/lib/format/time';
import { disableOfflineCopy, enableOfflineCopy, wipeOfflineSnapshots } from '@/lib/offline/snapshot';
import { isOfflineStoreAvailable } from '@/lib/offline/db';

/**
 * `components/offline/OfflineDeviceSettings.tsx` — HO-XCT-002 §11.4, ADR-FE-141. The device opt-in
 * ("Keep an offline copy of today's lists on this device") in plain words: what is stored, where,
 * who can open it, for how long, and when it is cleared. Off unless someone turns it on (OQ-1).
 */
export function OfflineDeviceSettings() {
  const { settings, loaded } = useDeviceSettings();
  const { record } = useStoredSnapshot();
  const session = useStaffSession();
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');

  const staffAccountId = session.data?.staffAccountId ?? null;
  // Read after mount: this page is prerendered, where IndexedDB does not exist, so reading it during
  // render would differ between the server HTML and the first client render (a hydration mismatch).
  const [available, setAvailable] = useState(true);
  useEffect(() => setAvailable(isOfflineStoreAvailable()), []);

  async function toggle(next: boolean) {
    setBusy(true);
    try {
      if (next) {
        if (!staffAccountId) return;
        await enableOfflineCopy(staffAccountId);
        setMessage('Offline copy turned on. The first copy is saved in a moment.');
      } else {
        await disableOfflineCopy('opt-out');
        setMessage('Offline copy turned off and cleared from this device.');
      }
    } finally {
      setBusy(false);
    }
  }

  async function clearNow() {
    setBusy(true);
    try {
      await wipeOfflineSnapshots('cleared-by-user');
      setMessage('The saved copy was removed from this device.');
    } finally {
      setBusy(false);
    }
  }

  const zone = record?.body.branch.timeZoneId ?? 'Asia/Dhaka';

  return (
    <div className="flex max-w-2xl flex-col gap-5">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h1 className="font-display text-xl font-semibold text-ink">Offline copy on this device</h1>
        <Link href="/staff/front-desk" className="text-sm font-semibold text-accent-ink hover:underline">
          Back to the front desk
        </Link>
      </div>
      <p className="text-sm text-ink-muted">
        If the internet drops, this device can still show today&rsquo;s appointments and boarding
        tasks — read-only — from a copy it keeps. It is off unless someone turns it on, because the
        copy contains customers&rsquo; names.
      </p>

      {!available && (
        <p role="alert" className="rounded-[var(--radius-md)] border border-border bg-surface p-3 text-sm text-ink">
          This browser can&rsquo;t keep an offline copy (storage is unavailable, for example in a
          private window).
        </p>
      )}

      <div className="flex items-start gap-3">
        <input
          id="offline-enabled"
          type="checkbox"
          className="mt-1 h-4 w-4"
          checked={settings.offlineEnabled}
          disabled={!loaded || busy || !available || (!settings.offlineEnabled && !staffAccountId)}
          aria-describedby="offline-enabled-help"
          onChange={(event) => void toggle(event.target.checked)}
        />
        <div>
          <label htmlFor="offline-enabled" className="text-sm font-semibold text-ink">
            Keep an offline copy of today&rsquo;s lists on this device
          </label>
          <p id="offline-enabled-help" className="text-sm text-ink-muted">
            {settings.offlineEnabled && settings.enabledAt
              ? `On · turned on ${formatInZone(settings.enabledAt, zone)}.`
              : staffAccountId
                ? 'Off.'
                : 'Sign in to turn this on.'}
          </p>
        </div>
      </div>

      {settings.offlineEnabled && (
        <p className="text-sm text-ink-muted">
          {record
            ? `Last saved at ${timeOnlyInZone(record.fetchedAt, zone)} (${zone}) for ${record.body.branch.name}. It refreshes about every minute while you're online.`
            : 'Nothing saved yet. A copy is saved when someone is signed in and the device is online.'}
        </p>
      )}

      <section aria-labelledby="offline-kept-title" className="rounded-[var(--radius-md)] border border-border bg-surface p-4">
        <h2 id="offline-kept-title" className="text-[15px] font-semibold text-ink">
          What&rsquo;s kept, and for how long
        </h2>
        <dl className="mt-2 grid grid-cols-[auto_1fr] gap-x-4 gap-y-2 text-sm">
          <dt className="font-semibold text-ink">What</dt>
          <dd className="text-ink-muted">
            Today&rsquo;s appointments (time, cat, customer name, service, vet, room,
            status, handling notes for high-risk cats) and today&rsquo;s boarding tasks, for the
            branch you&rsquo;re working in. Nothing else — no medical records, no passwords.
          </dd>
          <dt className="font-semibold text-ink">Where</dt>
          <dd className="text-ink-muted">In this browser on this device only. It is never sent anywhere else.</dd>
          <dt className="font-semibold text-ink">Who can open it</dt>
          <dd className="text-ink-muted">Only the person who saved it, and it locks after a period without use.</dd>
          <dt className="font-semibold text-ink">How long</dt>
          <dd className="text-ink-muted">Until early the next morning, after which it is out of date and won&rsquo;t open.</dd>
          <dt className="font-semibold text-ink">Cleared automatically</dt>
          <dd className="text-ink-muted">
            When you sign out, when someone else signs in on this device, when the copy expires, and
            when this switch is turned off.
          </dd>
        </dl>
      </section>

      <section aria-labelledby="offline-clear-title" className="flex flex-col gap-2">
        <h2 id="offline-clear-title" className="text-[15px] font-semibold text-ink">
          Clear this device
        </h2>
        <p className="text-sm text-ink-muted">
          Removes the saved copy from this device straight away. The switch stays as it is, so a
          fresh copy is saved the next time the device is online and someone is signed in.
        </p>
        <div>
          <Button variant="secondary" size="sm" disabled={busy || !record} onClick={() => void clearNow()}>
            Clear this device now
          </Button>
        </div>
      </section>

      <p role="status" aria-live="polite" className="text-sm text-ink">
        {message}
      </p>
    </div>
  );
}
