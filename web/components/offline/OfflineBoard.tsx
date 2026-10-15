'use client';

import { useEffect, useRef, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { useQueryClient } from '@tanstack/react-query';
import { Lock, WifiOff } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { LoadingState } from '@/components/common/LoadingState';
import { useConnectivity } from '@/lib/hooks/useConnectivity';
import { useIdleTimeout } from '@/lib/hooks/useIdleTimeout';
import { useDeviceSettings, useKnownStaffAccountId } from '@/lib/hooks/useOfflineSnapshot';
import { formatBranchDateLong } from '@/lib/format/appointment';
import { formatInZone, timeOnlyInZone } from '@/lib/format/time';
import { startActivityRecording } from '@/lib/offline/activity';
import { idleElapsedMs, isBoardLocked, lockOfflineBoard } from '@/lib/offline/device';
import { describeOmittedSection, type OmittedSectionName } from '@/lib/offline/labels';
import { isSnapshotUsable, readLatestSnapshot, type SnapshotRecord } from '@/lib/offline/snapshot';
import { OfflineAppointmentsList } from './OfflineAppointmentsList';
import { OfflineBoardingTasksList } from './OfflineBoardingTasksList';
import { OfflineBoardRefusal } from './OfflineBoardRefusal';

const FRONT_DESK = '/staff/front-desk';
/** While the board is open it keeps asking whether the server is back. */
export const BOARD_RECHECK_MS = 5_000;

/** Only same-site staff paths may be returned to (never an arbitrary `?from=`). */
export function safeReturnPath(from: string | null): string {
  return from && from.startsWith('/staff/') && !from.startsWith('//') && !from.startsWith('/staff/offline')
    ? from
    : FRONT_DESK;
}

function minutesAgo(iso: string): string {
  const minutes = Math.max(0, Math.round((Date.now() - Date.parse(iso)) / 60_000));
  return minutes <= 1 ? 'just now' : `${minutes} minutes ago`;
}

/**
 * `components/offline/OfflineBoard.tsx` — HO-XCT-002 §11.4, AC-19, ADR-FE-144/145. The offline list.
 * It reads the device copy from IndexedDB ONCE and fetches no data, but it is only for a server that
 * cannot be reached, and it never trusts a cold load:
 *
 *  - on mount it pings once. If the server answers it goes straight back to the staff app (the
 *    session guard decides), and it keeps asking every few seconds while open;
 *  - it is locked when it opens if the device has been idle longer than \`idleTimeoutMinutes\` (or
 *    has no recorded activity), or if it was locked before: the lock is persisted in the device
 *    store and only a successful sync for that person lifts it. A reload does not unlock it;
 *  - it refuses a copy that is missing, out of date, or saved by another person than the one known;
 *  - it contains no action button.
 */
export function OfflineBoard() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const from = useSearchParams().get('from');
  const { recoveredAt, forceCheck } = useConnectivity();
  const knownStaffId = useKnownStaffAccountId();
  const { settings, loaded: deviceLoaded } = useDeviceSettings();
  const [record, setRecord] = useState<SnapshotRecord | undefined>(undefined);
  const [recordLoaded, setRecordLoaded] = useState(false);
  const [connection, setConnection] = useState<'checking' | 'offline'>('checking');
  const [, setTick] = useState(0);

  useEffect(() => {
    let cancelled = false;
    void readLatestSnapshot().then((found) => {
      if (cancelled) return;
      setRecord(found);
      setRecordLoaded(true);
    });
    return () => {
      cancelled = true;
    };
  }, []);

  // The board is for an unreachable server only: ask once now, then every few seconds.
  useEffect(() => {
    let cancelled = false;
    const leave = () => router.replace(safeReturnPath(from));
    void forceCheck().then((outcome) => {
      if (cancelled) return;
      if (outcome !== 'unreachable') leave();
      else setConnection('offline');
    });
    const id = setInterval(() => {
      void forceCheck().then((outcome) => {
        if (!cancelled && outcome !== 'unreachable') leave();
      });
    }, BOARD_RECHECK_MS);
    return () => {
      cancelled = true;
      clearInterval(id);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // "N minutes ago" is plain text, not a live region, so a one-minute tick is not announced.
  useEffect(() => {
    const id = setInterval(() => setTick((n) => n + 1), 60_000);
    return () => clearInterval(id);
  }, []);

  const mismatch = record && knownStaffId !== null && record.staffAccountId !== knownStaffId;
  const usable = record !== undefined && isSnapshotUsable(record) && !mismatch && settings.offlineEnabled;
  const idleMinutes = record?.body.idleTimeoutMinutes ?? 30;

  // Whether the board opens locked is decided ONCE, from the device store, when it has loaded.
  const decided = useRef<{ locked: boolean; idleMs: number } | null>(null);
  if (decided.current === null && deviceLoaded && recordLoaded && record) {
    decided.current = {
      locked: isBoardLocked(settings, idleMinutes),
      idleMs: idleElapsedMs(settings),
    };
  }
  const [idleFired, setIdleFired] = useState(false);
  const locked =
    usable && (decided.current?.locked === true || settings.lockedForStaffAccountId !== null || idleFired);

  const onIdle = () => setIdleFired(true);
  useIdleTimeout(idleMinutes, usable && !locked && connection === 'offline', decided.current?.idleMs ?? 0, onIdle);

  // Persist the lock: it survives a reload and only a sync for this person lifts it.
  useEffect(() => {
    if (locked && record) void lockOfflineBoard(record.staffAccountId);
  }, [locked, record]);

  // Using the board is activity on the device, so a short outage does not lock it.
  useEffect(() => {
    if (!usable || locked || connection !== 'offline') return;
    return startActivityRecording();
  }, [usable, locked, connection]);

  // A recovery that happens AFTER this board opened. One from an earlier outage in the same page
  // load is not news (CODE_REVIEW HIGH): the value at mount is the baseline.
  const seenRecovery = useRef<number | null>(recoveredAt);
  const [announcement, setAnnouncement] = useState('');
  useEffect(() => {
    if (recoveredAt === null || seenRecovery.current === recoveredAt) return;
    seenRecovery.current = recoveredAt;
    void queryClient.invalidateQueries();
    setAnnouncement('Connection is back. Taking you back to the staff app…');
    router.replace(safeReturnPath(from));
  }, [recoveredAt, from, router, queryClient]);

  if (!recordLoaded || !deviceLoaded || connection === 'checking') {
    return <LoadingState variant="card" rows={4} label="Checking the connection…" />;
  }

  const live = (
    <p role="status" aria-live="polite" className="sr-only">
      {announcement}
    </p>
  );

  if (!record || !settings.offlineEnabled) {
    return (
      <>
        {live}
        <OfflineBoardRefusal kind="none" onReconnect={async () => void (await forceCheck())} />
      </>
    );
  }
  if (mismatch) {
    return (
      <>
        {live}
        <OfflineBoardRefusal kind="other-person" onReconnect={async () => void (await forceCheck())} />
      </>
    );
  }
  if (!usable) {
    return (
      <>
        {live}
        <OfflineBoardRefusal
          kind="expired"
          savedDate={formatBranchDateLong(record.body.localDate)}
          onReconnect={async () => void (await forceCheck())}
        />
      </>
    );
  }

  const { body } = record;
  const zone = body.branch.timeZoneId;
  const chip = (
    <Badge variant="neutral" icon={<WifiOff size={12} aria-hidden="true" />}>
      Offline · read-only
    </Badge>
  );

  if (locked) {
    return (
      <div className="flex flex-col gap-4">
        {live}
        <header className="flex flex-wrap items-center gap-3">
          <h1 className="font-display text-xl font-semibold text-ink">
            {body.branch.name} · {formatBranchDateLong(body.localDate)}
          </h1>
          {chip}
        </header>
        <section
          aria-labelledby="offline-locked-title"
          className="flex flex-col gap-2 rounded-[var(--radius-md)] border border-border bg-surface p-6"
        >
          <h2 id="offline-locked-title" className="flex items-center gap-2 text-[15px] font-semibold text-ink">
            <Lock size={16} aria-hidden="true" />
            Locked. Sign in again when the connection returns.
          </h2>
          <p className="text-sm text-ink-muted">
            Today&rsquo;s list was hidden after {idleMinutes} minutes without use, to protect patient
            and customer details on a screen nobody is watching. It can&rsquo;t be unlocked while
            offline, because signing in needs the server. This page keeps checking the connection;
            when it&rsquo;s back you&rsquo;ll be taken to the staff app, which asks you to sign in.
          </p>
        </section>
      </div>
    );
  }

  const omitted = (name: OmittedSectionName) =>
    body.omittedSections.find((section) => section.section === name);
  const appointmentsOmitted = omitted('appointments');
  const boardingOmitted = omitted('boardingTasks');

  return (
    <div className="flex flex-col gap-6">
      {live}
      <header className="flex flex-col gap-2">
        <div className="flex flex-wrap items-center gap-3">
          <h1 className="font-display text-xl font-semibold text-ink">
            {body.branch.name} · {formatBranchDateLong(body.localDate)}
          </h1>
          {chip}
        </div>
        <p className="text-sm text-ink-muted">
          Saved at {timeOnlyInZone(record.fetchedAt, zone)} ({zone}) · {minutesAgo(record.fetchedAt)}
        </p>
        <p className="text-sm text-ink">
          You&rsquo;re offline. This list was last updated at {timeOnlyInZone(record.fetchedAt, zone)}.
          Changes are switched off until the connection returns. This page checks the connection by
          itself and takes you back to the staff app as soon as it&rsquo;s back.
        </p>
      </header>

      {appointmentsOmitted ? (
        <p className="rounded-[var(--radius-md)] border border-border bg-surface p-4 text-sm text-ink">
          {describeOmittedSection('appointments', appointmentsOmitted.reason)}
        </p>
      ) : (
        <OfflineAppointmentsList
          items={body.sections.appointments?.items ?? []}
          timeZoneId={zone}
        />
      )}

      {boardingOmitted ? (
        <p className="rounded-[var(--radius-md)] border border-border bg-surface p-4 text-sm text-ink">
          {describeOmittedSection('boardingTasks', boardingOmitted.reason)}
        </p>
      ) : (
        <OfflineBoardingTasksList
          items={body.sections.boardingTasks?.items ?? []}
          timeZoneId={zone}
        />
      )}

      <p className="text-sm text-ink-muted">
        Read-only copy for this device. Nothing on this page can be changed. It locks after{' '}
        {idleMinutes} minutes without use, and the copy is wiped at sign-out and expires at{' '}
        {formatInZone(body.validUntil, zone)}.
      </p>
    </div>
  );
}
