'use client';

import { useMemo, useRef, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { ChannelTabs } from './ChannelTabs';
import { TemplateFieldsPanel } from './TemplateFieldsPanel';
import { TemplateHeader } from './TemplateHeader';
import { TemplatePreviewPanel } from './TemplatePreviewPanel';
import { VariablePalette } from './VariablePalette';
import { insertPlaceholder } from './insertPlaceholder';
import { useLiveTemplatePreview } from './useLiveTemplatePreview';
import { useTemplateWrites } from './useTemplateWrites';
import { TEMPLATE_CONFLICT_MESSAGE } from '@/lib/api/errors';
import {
  buildTemplateSchema,
  sortChannels,
  toFormValues,
  type TemplateField,
  type TemplateFormValues,
} from '@/lib/validation/templateContent';
import type {
  NotificationTemplateDetail,
  SaveNotificationTemplateRequest,
} from '@/lib/api/admin-notification-templates';

export interface TemplateEditorProps {
  template: NotificationTemplateDetail;
  /** Presentation only (`NotificationTemplates.Manage`): the server still decides every write. */
  canManage: boolean;
  timeZoneId: string;
  onSave: (request: SaveNotificationTemplateRequest) => Promise<NotificationTemplateDetail>;
  onReset: (expectedContentVersion: number) => Promise<NotificationTemplateDetail>;
  onReload: () => Promise<NotificationTemplateDetail>;
}

/**
 * `components/templates/TemplateEditor.tsx` — ⭐ HO-NTF-002 §11.3, US-NTF-02. Composes the form
 * (react-hook-form + zod length rules), the channel tabs, the placeholder palette and the live
 * preview. It never renders a template itself: the preview comes from the server (ADR-FE-163),
 * debounced 600 ms (`useLiveTemplatePreview`). Values survive every error (NFR-USA-5); a `409`
 * offers **Reload** (`useTemplateWrites`); `UnsavedChangesGuard` protects against leaving. Below
 * 1024 px the preview stacks UNDER the fields (UI-1).
 */
export function TemplateEditor({
  template,
  canManage,
  timeZoneId,
  onSave,
  onReset,
  onReload,
}: TemplateEditorProps) {
  const channels = useMemo(() => sortChannels(template.channels), [template.channels]);
  const schema = useMemo(() => buildTemplateSchema(template.limits), [template.limits]);
  const form = useForm<TemplateFormValues>({
    resolver: zodResolver(schema),
    defaultValues: toFormValues(template.contents, template.channels),
  });
  const [activeChannel, setActiveChannel] = useState(channels[0]!);
  const [sampleValues, setSampleValues] = useState<Record<string, string>>({});
  const lastFocused = useRef<{ index: number; field: TemplateField } | null>(null);

  const preview = useLiveTemplatePreview(template.key, form, sampleValues);
  const writes = useTemplateWrites({
    form,
    channels,
    initialVersion: template.contentVersion,
    onSave,
    onReset,
    onReload,
    setActiveChannel,
  });

  const { formState } = form;
  const values = form.watch() as TemplateFormValues;
  const tabBadges = Object.fromEntries(
    channels.flatMap((channel, index) =>
      formState.errors.contents?.[index] ? [[channel, 'Needs attention']] : [],
    ),
  );

  return (
    <div className="flex flex-col gap-6">
      <UnsavedChangesGuard when={canManage && formState.isDirty} />
      <TemplateHeader template={template} timeZoneId={timeZoneId} />
      {!canManage && (
        <p className="rounded-[var(--radius-sm)] bg-surface-sunken px-3 py-2 text-sm text-ink-muted">
          You can view this template but not change it.
        </p>
      )}

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-[minmax(0,3fr)_minmax(0,2fr)]">
        <form
          noValidate
          onSubmit={form.handleSubmit(() => writes.setConfirm('save'))}
          className="flex min-w-0 flex-col gap-4"
          aria-label={`Edit ${template.displayName}`}
        >
          {writes.conflict && (
            <div
              role="alert"
              className="flex flex-col gap-2 rounded-[var(--radius-sm)] border border-danger-ink bg-danger-bg p-3"
            >
              <p className="text-sm font-semibold text-danger-ink">{TEMPLATE_CONFLICT_MESSAGE}</p>
              <Button
                type="button"
                variant="secondary"
                size="sm"
                className="w-fit"
                disabled={writes.reloading}
                onClick={writes.reload}
              >
                {writes.reloading ? 'Reloading…' : 'Reload'}
              </Button>
            </div>
          )}
          {writes.rootError && (
            <p role="alert" className="text-sm font-semibold text-danger-ink">
              {writes.rootError}
            </p>
          )}
          {writes.notice && (
            <p role="status" className="text-sm font-semibold text-accent-ink">
              {writes.notice}
            </p>
          )}

          <ChannelTabs
            idPrefix="tpl"
            channels={channels}
            active={activeChannel}
            onChange={setActiveChannel}
            tabBadges={tabBadges}
            renderPanel={(channel) => {
              const index = channels.indexOf(channel);
              return (
                <TemplateFieldsPanel
                  index={index}
                  channel={channel}
                  register={form.register}
                  errors={formState.errors}
                  values={values.contents?.[index]}
                  limits={template.limits.find((l) => l.channel === channel)}
                  readOnly={!canManage}
                  onFieldFocus={(i, field) => (lastFocused.current = { index: i, field })}
                />
              );
            }}
          />

          <VariablePalette
            variables={template.variables}
            activeChannel={activeChannel}
            disabled={!canManage}
            onInsert={(variable) => {
              const index = channels.indexOf(activeChannel);
              const last = lastFocused.current;
              const field = last && last.index === index ? last.field : 'body';
              insertPlaceholder(form, index, activeChannel, field, variable);
            }}
          />

          {canManage && (
            <div className="flex flex-wrap gap-3">
              <Button type="submit">Save changes</Button>
              {template.isCustomised && (
                <Button
                  type="button"
                  variant="secondary"
                  onClick={() => writes.setConfirm('reset')}
                >
                  Reset to default
                </Button>
              )}
            </div>
          )}
        </form>

        <div className="flex min-w-0 flex-col gap-4 lg:sticky lg:top-4 lg:self-start">
          <TemplatePreviewPanel
            activeChannel={activeChannel}
            preview={preview.data}
            error={preview.error}
            isFetching={preview.isFetching}
            variables={template.variables}
            sampleValues={sampleValues}
            onSampleChange={(name, value) => setSampleValues((s) => ({ ...s, [name]: value }))}
          />
        </div>
      </div>

      <ConfirmDialog
        open={writes.confirm === 'save'}
        onOpenChange={(open) => !open && writes.setConfirm(null)}
        title={`Save changes to ${template.displayName}?`}
        consequence="Messages sent from now on will use this wording. Messages already sent keep theirs."
        confirmLabel="Save changes"
        pendingLabel="Saving…"
        onConfirm={writes.save}
      />
      <ConfirmDialog
        open={writes.confirm === 'reset'}
        onOpenChange={(open) => !open && writes.setConfirm(null)}
        title={`Reset ${template.displayName} to the default wording?`}
        consequence={`Messages sent from now on will use the shipped wording instead of your changes. Messages already sent keep theirs.${formState.isDirty ? ' Your unsaved edits will be discarded.' : ''}`}
        confirmLabel="Reset to default"
        variant="destructive"
        pendingLabel="Resetting…"
        onConfirm={writes.reset}
      />
    </div>
  );
}
