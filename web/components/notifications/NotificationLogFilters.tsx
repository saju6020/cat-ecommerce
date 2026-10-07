'use client';

import { CustomerFilterField } from './CustomerFilterField';
import { channelTitle, statusLabel } from './statusText';
import {
  NOTIFICATION_LOG_MAX_RANGE_DAYS,
  NOTIFICATION_STATUSES,
  notificationRangeDays,
} from '@/lib/api/notifications-log';
import type { StaffCustomerSummary } from '@/lib/api/staff-customers';

/** The filters that live in the page URL. The destination search term (an email address or mobile
 *  number) is deliberately NOT one of them: it would end up in browser history and referrers
 *  (NTF backlog N10), so the page keeps it in component state. */
export const LOG_FILTER_FIELDS = [
  'from',
  'to',
  'recipientId',
  'channel',
  'templateKey',
  'status',
] as const;
export type LogFilterField = (typeof LOG_FILTER_FIELDS)[number] | 'destination';
export type LogFilterValues = Record<LogFilterField, string>;

const INPUT_CLASS =
  'w-full min-w-0 rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink';
const LABEL_CLASS = 'text-xs font-semibold text-ink-muted';
const CHANNELS = ['Sms', 'Email', 'InApp'] as const;

/**
 * `components/notifications/NotificationLogFilters.tsx` — ⭐ HO-NTF-002 §11.5, UI-4. Date range
 * (**required**, the page defaults it to the last 7 days; the 93-day maximum is stated before the
 * person submits and a longer range is refused here, in words, without a request), customer,
 * destination (free text — the server normalises it; kept out of the URL, N10), channel, template and status. Every value is
 * but the destination is owned by the page's URL state (`useUrlFilterState`), so a filtered log is
 * linkable and Back restores it. When the template list cannot be read (the role may lack
 * `NotificationTemplates.View`), the template filter falls back to a plain key field.
 */
export function NotificationLogFilters({
  values,
  onChange,
  onCommit,
  templates,
  recipientLabel,
  onCustomerSearch,
  onCustomerSelect,
}: {
  values: LogFilterValues;
  onChange: (name: LogFilterField, value: string) => void;
  onCommit?: (name: LogFilterField) => void;
  templates: Array<{ key: string; displayName: string }> | undefined;
  recipientLabel: string | null;
  onCustomerSearch: (term: string) => Promise<StaffCustomerSummary[]>;
  onCustomerSelect: (customer: StaffCustomerSummary) => void;
}) {
  const days = values.from && values.to ? notificationRangeDays(values.from, values.to) : 0;
  const tooLong = days > NOTIFICATION_LOG_MAX_RANGE_DAYS;
  const backwards = !!values.from && !!values.to && days < 1;

  const commitOnBlur = (name: LogFilterField) => ({
    onBlur: () => onCommit?.(name),
    onKeyDown: (event: React.KeyboardEvent) => {
      if (event.key === 'Enter') {
        event.preventDefault();
        onCommit?.(name);
      }
    },
  });

  return (
    <form
      role="search"
      aria-label="Filter the message log"
      onSubmit={(event) => event.preventDefault()}
      className="flex flex-col gap-3 rounded-[var(--radius-md)] border border-border bg-surface p-4"
    >
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <div className="flex flex-col gap-1.5">
          <label htmlFor="log-from" className={LABEL_CLASS}>
            From
          </label>
          <input
            id="log-from"
            type="date"
            required
            value={values.from}
            onChange={(e) => onChange('from', e.target.value)}
            className={INPUT_CLASS}
          />
        </div>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="log-to" className={LABEL_CLASS}>
            To
          </label>
          <input
            id="log-to"
            type="date"
            required
            value={values.to}
            onChange={(e) => onChange('to', e.target.value)}
            className={INPUT_CLASS}
          />
        </div>
        <CustomerFilterField
          selectedId={values.recipientId}
          selectedLabel={recipientLabel}
          onSelect={onCustomerSelect}
          onClear={() => onChange('recipientId', '')}
          onSearch={onCustomerSearch}
        />
        <div className="flex flex-col gap-1.5">
          <label htmlFor="log-destination" className={LABEL_CLASS}>
            Destination (mobile number or email)
          </label>
          <input
            id="log-destination"
            value={values.destination}
            onChange={(e) => onChange('destination', e.target.value)}
            {...commitOnBlur('destination')}
            className={INPUT_CLASS}
          />
        </div>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="log-channel" className={LABEL_CLASS}>
            Channel
          </label>
          <select
            id="log-channel"
            value={values.channel}
            onChange={(e) => onChange('channel', e.target.value)}
            className={INPUT_CLASS}
          >
            <option value="">All channels</option>
            {CHANNELS.map((channel) => (
              <option key={channel} value={channel}>
                {channelTitle(channel)}
              </option>
            ))}
          </select>
        </div>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="log-template" className={LABEL_CLASS}>
            {templates ? 'Template' : 'Template key'}
          </label>
          {templates ? (
            <select
              id="log-template"
              value={values.templateKey}
              onChange={(e) => onChange('templateKey', e.target.value)}
              className={INPUT_CLASS}
            >
              <option value="">All templates</option>
              {templates.map((template) => (
                <option key={template.key} value={template.key}>
                  {template.displayName}
                </option>
              ))}
            </select>
          ) : (
            <input
              id="log-template"
              value={values.templateKey}
              onChange={(e) => onChange('templateKey', e.target.value)}
              {...commitOnBlur('templateKey')}
              className={INPUT_CLASS}
            />
          )}
        </div>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="log-status" className={LABEL_CLASS}>
            Status
          </label>
          <select
            id="log-status"
            value={values.status}
            onChange={(e) => onChange('status', e.target.value)}
            className={INPUT_CLASS}
          >
            <option value="">All statuses</option>
            {NOTIFICATION_STATUSES.map((status) => (
              <option key={status} value={status}>
                {statusLabel(status)}
              </option>
            ))}
          </select>
        </div>
      </div>

      <p className="text-xs text-ink-muted">
        Choose a date range of up to {NOTIFICATION_LOG_MAX_RANGE_DAYS} days.
      </p>
      {tooLong && (
        <p role="alert" className="text-sm font-semibold text-danger-ink">
          The date range is {days} days — the log can show at most {NOTIFICATION_LOG_MAX_RANGE_DAYS}
          . Narrow it to see results.
        </p>
      )}
      {backwards && (
        <p role="alert" className="text-sm font-semibold text-danger-ink">
          The &ldquo;To&rdquo; date is before the &ldquo;From&rdquo; date. Swap them to see results.
        </p>
      )}
    </form>
  );
}
