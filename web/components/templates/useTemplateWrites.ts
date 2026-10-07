import { useEffect, useState } from 'react';
import type { UseFormReturn } from 'react-hook-form';
import { ApiConflictError, ApiValidationError } from '@/lib/api/client';
import { describeUnexpectedError, parseTemplateContentsProperty } from '@/lib/api/errors';
import { fieldId } from './TemplateFieldsPanel';
import {
  toFormValues,
  toRequestContents,
  type TemplateFormValues,
} from '@/lib/validation/templateContent';
import type {
  NotificationTemplateDetail,
  SaveNotificationTemplateRequest,
  TemplateChannel,
} from '@/lib/api/admin-notification-templates';

/**
 * `components/templates/useTemplateWrites.ts` — HO-NTF-002 §11.3. The editor's save / reset / reload
 * flows and how their failures are shown:
 *  - `409` -> the conflict banner with **Reload**; nothing typed is touched (NFR-USA-5);
 *  - `400` -> each violation goes onto its field (`Contents[i].Subject` ...), the tab holding the
 *    FIRST one is activated and that field focused; unmapped violations become one root message;
 *  - anything else -> a plain-language root message (`errorCode` is never rendered, NFR-USA-4).
 * `loadedVersion` is the version the form values were based on, sent as `expectedContentVersion` —
 * it moves only when the person saves, resets or explicitly reloads, never because a background
 * refetch happened to see someone else's edit.
 */
export function useTemplateWrites({
  form,
  channels,
  initialVersion,
  onSave,
  onReset,
  onReload,
  setActiveChannel,
}: {
  form: UseFormReturn<TemplateFormValues>;
  channels: TemplateChannel[];
  initialVersion: number;
  onSave: (request: SaveNotificationTemplateRequest) => Promise<NotificationTemplateDetail>;
  onReset: (expectedContentVersion: number) => Promise<NotificationTemplateDetail>;
  onReload: () => Promise<NotificationTemplateDetail>;
  setActiveChannel: (channel: TemplateChannel) => void;
}) {
  const [loadedVersion, setLoadedVersion] = useState(initialVersion);
  const [confirm, setConfirm] = useState<'save' | 'reset' | null>(null);
  const [rootError, setRootError] = useState<string | null>(null);
  const [conflict, setConflict] = useState(false);
  const [reloading, setReloading] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [focusTarget, setFocusTarget] = useState<string | null>(null);

  useEffect(() => {
    if (focusTarget) document.getElementById(focusTarget)?.focus();
  }, [focusTarget]);

  function adopt(fresh: NotificationTemplateDetail) {
    form.reset(toFormValues(fresh.contents, fresh.channels));
    setLoadedVersion(fresh.contentVersion);
    setConflict(false);
    setRootError(null);
  }

  function showError(error: unknown) {
    if (error instanceof ApiConflictError) {
      setConflict(true);
      return;
    }
    if (!(error instanceof ApiValidationError)) {
      setRootError(describeUnexpectedError(error));
      return;
    }
    const unmapped: string[] = [];
    let first: string | null = null;
    for (const violation of error.response.errors) {
      const parsed = parseTemplateContentsProperty(violation.propertyName);
      const channel = parsed ? channels[parsed.index] : undefined;
      if (!parsed || !channel) {
        unmapped.push(violation.errorMessage);
        continue;
      }
      form.setError(`contents.${parsed.index}.${parsed.field}`, {
        type: 'server',
        message: violation.errorMessage,
      });
      if (!first) {
        first = fieldId(parsed.index, parsed.field);
        setActiveChannel(channel);
      }
    }
    if (unmapped.length > 0) setRootError(unmapped.join(' '));
    setFocusTarget(null); // so the same field failing twice in a row still re-focuses
    if (first) setTimeout(() => setFocusTarget(first), 0);
  }

  async function run(action: () => Promise<NotificationTemplateDetail>, done: string) {
    setNotice(null);
    setRootError(null);
    try {
      adopt(await action());
      setNotice(done);
    } catch (error) {
      showError(error);
    }
    setConfirm(null);
  }

  return {
    confirm,
    setConfirm,
    rootError,
    conflict,
    notice,
    save: () =>
      run(
        () =>
          onSave({
            expectedContentVersion: loadedVersion,
            contents: toRequestContents(form.getValues()),
          }),
        'Saved. Messages sent from now on will use this wording.',
      ),
    reset: () => run(() => onReset(loadedVersion), 'Reset to the default wording.'),
    reloading,
    reload: async () => {
      setReloading(true);
      try {
        adopt(await onReload());
        setNotice('Reloaded the latest version.');
      } catch (error) {
        showError(error);
      } finally {
        setReloading(false);
      }
    },
  };
}
