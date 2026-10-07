'use client';

import { PreviewChannelView } from './PreviewChannelView';
import { SampleValuesForm } from './SampleValuesForm';
import { CHANNEL_LABELS } from '@/lib/validation/templateContent';
import { ApiValidationError } from '@/lib/api/client';
import type {
  NotificationTemplateVariable,
  TemplateChannel,
  TemplatePreview,
} from '@/lib/api/admin-notification-templates';

/**
 * `components/templates/TemplatePreviewPanel.tsx` — ⭐ HO-NTF-002 §11.3, US-NTF-02 AC1/AC2. Shows the
 * SERVER's preview of the unsaved contents for the active channel. A preview `400` is shown inline
 * here, verbatim — the message that names the variable (`{{catname}} is not a variable of this
 * template…`) is the acceptance criterion — while the editor also puts it on the offending field;
 * the previous good preview stays visible and typing is never blocked (`keepPreviousData`).
 * `errorCode` is never rendered. Any other failure is a generic sentence, never the server's text.
 */
export function TemplatePreviewPanel({
  activeChannel,
  preview,
  error,
  isFetching,
  variables,
  sampleValues,
  onSampleChange,
}: {
  activeChannel: TemplateChannel;
  preview: TemplatePreview | undefined;
  error: unknown;
  isFetching: boolean;
  variables: NotificationTemplateVariable[];
  sampleValues: Record<string, string>;
  onSampleChange: (name: string, value: string) => void;
}) {
  const channelPreview = preview?.channels.find((c) => c.channel === activeChannel);
  const messages =
    error instanceof ApiValidationError
      ? error.response.errors.map((e) => e.errorMessage)
      : error
        ? ["The preview isn't available right now. Your changes are safe — keep editing."]
        : [];

  return (
    <section
      aria-label="Preview"
      className="flex flex-col gap-3 rounded-[var(--radius-md)] border border-border bg-surface p-4"
    >
      <div className="flex items-center justify-between gap-2">
        <h2 className="text-sm font-semibold text-ink">
          Preview — {CHANNEL_LABELS[activeChannel]}
        </h2>
        <span role="status" className="text-xs text-ink-muted">
          {isFetching ? 'Updating preview…' : ''}
        </span>
      </div>

      {messages.length > 0 && (
        <div
          role="status"
          data-testid="preview-error"
          className="flex flex-col gap-1 rounded-[var(--radius-sm)] border border-danger-ink bg-danger-bg p-3"
        >
          {messages.map((message) => (
            <p key={message} className="text-sm font-semibold text-danger-ink">
              {message}
            </p>
          ))}
          {channelPreview && (
            <p className="text-xs text-danger-ink">Showing the last preview that worked.</p>
          )}
        </div>
      )}

      {channelPreview ? (
        <PreviewChannelView preview={channelPreview} />
      ) : (
        messages.length === 0 && <p className="text-sm text-ink-muted">Preparing the preview…</p>
      )}

      <SampleValuesForm variables={variables} values={sampleValues} onChange={onSampleChange} />
    </section>
  );
}
