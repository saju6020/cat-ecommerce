'use client';

import { useMemo, useState } from 'react';
import { Button } from '@/components/ui/button';
import { RetryNotice } from '@/components/auth/RetryNotice';
import { UnsavedChangesGuard } from './UnsavedChangesGuard';
import { describeApiError } from '@/lib/api/errors';
import { ApiRateLimitedError, ApiValidationError } from '@/lib/api/client';
import type {
  NotificationPreferenceCell,
  NotificationPreferenceChange,
  NotificationPreferences,
} from '@/lib/api/account';

function cellKey(category: string, channel: string) {
  return `${category}\u0000${channel}`;
}

/**
 * `components/account/NotificationPreferenceMatrix.tsx` — ⭐ US-ACC-06, HO-ACC-002 §11.7/§8.6. A
 * real `<table>` with row and column headers; `isAlwaysOn` cells are disabled checkboxes reading
 * "Always on" text (never colour alone, UI-6); explicit **Save changes** sends only the cells that
 * actually changed (ADR-FE-008 — never autosave).
 *
 * ARCH gate HIGH 3 (first pass) — takes `onSave`/`isPending`/`rateLimitError` as props; the page
 * (`app/(portal)/portal/account/notifications/page.tsx`) wires `useChangeNotificationPreferences()`.
 */
export interface NotificationPreferenceMatrixProps {
  preferences: NotificationPreferences;
  onSave: (changes: NotificationPreferenceChange[]) => Promise<unknown>;
  isPending: boolean;
  rateLimitError: ApiRateLimitedError | null;
  /** HO-NTF-002 §11.2 — a field rendered above the table (the preferred-channel radio group) that
   *  saves in the SAME explicit Save (ADR-FE-008). The page owns its value and sends it in `onSave`. */
  beforeTable?: React.ReactNode;
  /** True when that field differs from its saved value: counts as dirty (UnsavedChangesGuard) and as
   *  "something to save" even when no matrix cell changed. */
  extraDirty?: boolean;
  /** Called first with a server `400`; return `true` when every violation was shown on the extra
   *  field (the generic message below is then suppressed). */
  onServerError?: (error: ApiValidationError) => boolean;
}

export function NotificationPreferenceMatrix({
  preferences,
  onSave,
  isPending,
  rateLimitError,
  beforeTable,
  extraDirty = false,
  onServerError,
}: NotificationPreferenceMatrixProps) {
  const [localGrants, setLocalGrants] = useState<Map<string, boolean>>(
    () =>
      new Map(
        preferences.cells.map((cell) => [cellKey(cell.category, cell.channel), cell.isGranted]),
      ),
  );
  const [savedMessage, setSavedMessage] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const { channels, categories, cellByKey } = useMemo(() => {
    const channelOrder: string[] = [];
    const categoryOrder: string[] = [];
    const map = new Map<string, NotificationPreferenceCell>();
    for (const cell of preferences.cells) {
      if (!channelOrder.includes(cell.channel)) channelOrder.push(cell.channel);
      if (!categoryOrder.includes(cell.category)) categoryOrder.push(cell.category);
      map.set(cellKey(cell.category, cell.channel), cell);
    }
    return { channels: channelOrder, categories: categoryOrder, cellByKey: map };
  }, [preferences.cells]);

  function toggle(category: string, channel: string) {
    const key = cellKey(category, channel);
    setLocalGrants((current) => {
      const next = new Map(current);
      next.set(key, !current.get(key));
      return next;
    });
    setSavedMessage(null);
  }

  async function handleSave() {
    setSavedMessage(null);
    setErrorMessage(null);
    const changes = preferences.cells
      .filter(
        (cell) =>
          cell.isEditable &&
          localGrants.get(cellKey(cell.category, cell.channel)) !== cell.isGranted,
      )
      .map((cell) => ({
        channel: cell.channel,
        category: cell.category,
        isGranted: localGrants.get(cellKey(cell.category, cell.channel))!,
      }));

    if (changes.length === 0 && !extraDirty) {
      setSavedMessage('Nothing to save — no preferences were changed.');
      return;
    }

    try {
      await onSave(changes);
      setSavedMessage('Your notification preferences have been saved.');
    } catch (error) {
      if (error instanceof ApiValidationError && onServerError?.(error)) return;
      if (!(error instanceof ApiRateLimitedError)) setErrorMessage(describeApiError(error));
    }
  }

  const isDirty =
    extraDirty ||
    preferences.cells.some(
      (cell) => localGrants.get(cellKey(cell.category, cell.channel)) !== cell.isGranted,
    );

  return (
    <div className="flex flex-col gap-4">
      <UnsavedChangesGuard when={isDirty} />
      {errorMessage && (
        <p role="alert" aria-live="polite" className="text-sm font-semibold text-danger-ink">
          {errorMessage}
        </p>
      )}
      {rateLimitError && <RetryNotice retryAfterSeconds={rateLimitError.retryAfterSeconds} />}
      {savedMessage && (
        <p role="status" aria-live="polite" className="text-sm font-semibold text-ink">
          {savedMessage}
        </p>
      )}

      {beforeTable}

      <table className="w-full border-collapse text-sm">
        <caption className="sr-only">Notification preferences by category and channel</caption>
        <thead>
          <tr>
            <th scope="col" className="border-b border-border p-2 text-left font-semibold text-ink">
              Category
            </th>
            {channels.map((channel) => (
              <th
                key={channel}
                scope="col"
                className="border-b border-border p-2 text-left font-semibold text-ink"
              >
                {channel}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {categories.map((category) => (
            <tr key={category}>
              <th scope="row" className="border-b border-border p-2 text-left font-medium text-ink">
                {category}
              </th>
              {channels.map((channel) => {
                const cell = cellByKey.get(cellKey(category, channel));
                if (!cell) return <td key={channel} className="border-b border-border p-2" />;
                const checked = localGrants.get(cellKey(category, channel)) ?? cell.isGranted;
                const inputId = `pref-${category}-${channel}`;
                return (
                  <td key={channel} className="border-b border-border p-2">
                    <label htmlFor={inputId} className="flex items-center gap-2">
                      <input
                        id={inputId}
                        type="checkbox"
                        checked={checked}
                        disabled={cell.isAlwaysOn}
                        onChange={() => toggle(category, channel)}
                      />
                      {cell.isAlwaysOn ? (
                        <span className="text-xs font-semibold text-ink-muted">Always on</span>
                      ) : (
                        <span className="sr-only">
                          {category} by {channel}
                        </span>
                      )}
                    </label>
                  </td>
                );
              })}
            </tr>
          ))}
        </tbody>
      </table>

      <Button onClick={handleSave} disabled={isPending} aria-busy={isPending} className="w-fit">
        {isPending ? 'Saving…' : 'Save changes'}
      </Button>
    </div>
  );
}
