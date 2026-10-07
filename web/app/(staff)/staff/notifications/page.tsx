'use client';

import { Suspense, useEffect, useMemo, useRef, useState } from 'react';
import { usePathname, useRouter, useSearchParams } from 'next/navigation';
import { NotificationQueueSummary } from '@/components/notifications/NotificationQueueSummary';
import {
  LOG_FILTER_FIELDS,
  NotificationLogFilters,
  type LogFilterField,
} from '@/components/notifications/NotificationLogFilters';
import { NotificationLogTable } from '@/components/notifications/NotificationLogTable';
import { EmptyState } from '@/components/common/EmptyState';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { LoadingState } from '@/components/common/LoadingState';
import { useUrlFilterState } from '@/lib/hooks/useUrlFilterState';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { useCustomerLookup } from '@/lib/hooks/useCustomerLookup';
import { useNotificationLog, useNotificationSummary } from '@/lib/hooks/useNotificationLog';
import { useNotificationTemplates } from '@/lib/hooks/useNotificationTemplates';
import { useStaffCustomer } from '@/lib/hooks/useStaffCustomers';
import { ApiForbiddenError } from '@/lib/api/client';
import {
  defaultNotificationRange,
  NOTIFICATION_LOG_PAGE_SIZE,
  isNotificationRangeTooLong,
  notificationRangeDays,
} from '@/lib/api/notifications-log';

// Free-text fields debounce their URL write; the rest write at once (see useUrlFilterState).
const DEBOUNCED_FIELDS = ['templateKey'] as const;
// How long the destination term waits after the last keystroke before it is sent in a request.
const DESTINATION_DEBOUNCE_MS = 300;
const DATE_FIELDS = ['from', 'to'] as const;

/**
 * `/staff/notifications` — US-NTF-06. The queue summary (refetched every 30 s) with the
 * provider-not-connected warning, the filters (all in the URL, so Back restores them — UI-4) and the
 * paged log. A server `403` renders `PermissionDeniedState` — the nav entry is presentation only
 * (E-E2). The date range is required and defaults to the last 7 days (OQ-4); a range over 93 days
 * is refused in the filter bar and never requested. Clearing a date shows a "choose a date range"
 * prompt and keeps the empty param in the URL (`from=`), so a reload shows the same thing (N2). The
 * destination search term is held in component state only, never the URL (N10).
 */
function NotificationLogContent() {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const timeZoneId = useBranchTimeZone();

  const defaults = useMemo(() => defaultNotificationRange(), []);
  const { values, setValue, flush } = useUrlFilterState(
    LOG_FILTER_FIELDS,
    defaults,
    DEBOUNCED_FIELDS,
  );
  const [destinationInput, setDestinationInput] = useState('');
  const [destination, setDestination] = useState('');
  const pageNumber = Math.max(1, Number(searchParams.get('page') ?? '1') || 1);

  const rangeOk =
    !!values.from &&
    !!values.to &&
    notificationRangeDays(values.from, values.to) >= 1 &&
    !isNotificationRangeTooLong(values.from, values.to);

  const log = useNotificationLog(
    {
      from: values.from,
      to: values.to,
      recipientId: values.recipientId || undefined,
      destination: destination || undefined,
      channel: values.channel || undefined,
      templateKey: values.templateKey || undefined,
      status: values.status || undefined,
      pageNumber,
      pageSize: NOTIFICATION_LOG_PAGE_SIZE,
    },
    rangeOk,
  );
  const summary = useNotificationSummary();
  const templates = useNotificationTemplates();
  const lookup = useCustomerLookup();
  const recipient = useStaffCustomer(values.recipientId);

  // The destination term is applied ~300 ms after the last keystroke (or at once on blur / Enter).
  useEffect(() => {
    const timer = setTimeout(() => setDestination(destinationInput), DESTINATION_DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [destinationInput]);

  // A new search starts from page 1, like every other filter edit.
  const firstDestination = useRef(true);
  useEffect(() => {
    if (firstDestination.current) {
      firstDestination.current = false;
      return;
    }
    if (pageNumber > 1) router.replace(urlFor(values, 1));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [destination]);

  if (log.error instanceof ApiForbiddenError || summary.error instanceof ApiForbiddenError) {
    return <PermissionDeniedState />;
  }

  /** The URL for the given filter values, built from local state (never the possibly-stale
   *  `searchParams`). `from`/`to` are always written, even when empty. */
  function urlFor(next: typeof values, page: number): string {
    const params = new URLSearchParams();
    for (const name of LOG_FILTER_FIELDS) {
      const value = next[name];
      if (value || (DATE_FIELDS as readonly string[]).includes(name)) params.set(name, value);
    }
    if (page > 1) params.set('page', String(page));
    const query = params.toString();
    return query ? `${pathname}?${query}` : pathname;
  }

  function goToPage(page: number) {
    // A filter edit still inside its debounce window would be lost by this navigation: write it
    // first (N12). `urlFor` then builds the page URL from the same fresh local values.
    for (const name of DEBOUNCED_FIELDS) flush(name);
    router.push(urlFor(values, page));
  }

  function changeFilter(name: LogFilterField, value: string) {
    if (name === 'destination') {
      setDestinationInput(value);
      return;
    }
    setValue(name, value);
    if (value === '' && (DATE_FIELDS as readonly string[]).includes(name)) {
      // `useUrlFilterState` drops an empty param, so a reload would silently restore the default
      // range. Keep the cleared date in the URL as `from=` / `to=` instead (N2): replace the entry
      // the hook just wrote, after writing anything of its own still pending.
      for (const pending of DEBOUNCED_FIELDS) flush(pending);
      router.replace(urlFor({ ...values, [name]: '' }, 1));
    }
  }

  function commitFilter(name: LogFilterField) {
    if (name === 'destination') setDestination(destinationInput);
    else flush(name);
  }

  return (
    <div className="flex flex-col gap-4">
      <h1 className="font-display text-xl font-semibold text-ink">Messages</h1>
      <NotificationQueueSummary
        summary={summary.data}
        isLoading={summary.isLoading}
        isError={summary.isError}
        onRetry={() => summary.refetch()}
      />
      <NotificationLogFilters
        values={{ ...values, destination: destinationInput }}
        onChange={changeFilter}
        onCommit={commitFilter}
        templates={templates.data?.map((t) => ({ key: t.key, displayName: t.displayName }))}
        recipientLabel={recipient.data?.fullName ?? null}
        onCustomerSearch={(term) => lookup.mutateAsync(term)}
        onCustomerSelect={(customer) => setValue('recipientId', customer.id)}
      />
      {rangeOk ? (
        <NotificationLogTable
          items={log.data?.items}
          isLoading={log.isLoading}
          isError={log.isError}
          isFetching={log.isFetching}
          onRetry={() => log.refetch()}
          timeZoneId={timeZoneId}
          pageNumber={pageNumber}
          pageSize={NOTIFICATION_LOG_PAGE_SIZE}
          totalCount={log.data?.totalCount ?? 0}
          onPageChange={goToPage}
        />
      ) : !values.from || !values.to ? (
        <EmptyState
          title="Choose a date range"
          description="Pick both a From and a To date to see messages."
        />
      ) : null}
    </div>
  );
}

export default function NotificationLogPage() {
  return (
    <Suspense fallback={<LoadingState variant="table" rows={6} label="Loading messages…" />}>
      <NotificationLogContent />
    </Suspense>
  );
}
