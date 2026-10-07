import { useEffect, useMemo, useRef } from 'react';
import type { Path, UseFormReturn } from 'react-hook-form';
import { useDebouncedValue } from '@/lib/hooks/useDebouncedValue';
import { PREVIEW_DEBOUNCE_MS, useTemplatePreview } from '@/lib/hooks/useNotificationTemplates';
import { ApiValidationError } from '@/lib/api/client';
import { parseTemplateContentsProperty } from '@/lib/api/errors';
import { toRequestContents, type TemplateFormValues } from '@/lib/validation/templateContent';
import type { PreviewTemplateRequest } from '@/lib/api/admin-notification-templates';

/**
 * `components/templates/useLiveTemplatePreview.ts` — HO-NTF-002 §8.3, ADR-FE-163. Binds the editor's
 * form values (and the sample values) to the server-rendered preview: the request is serialised, the
 * string is debounced 600 ms (a string, so identity changes do not restart the timer), and the
 * query — keyed on that request, `keepPreviousData`, abortable — runs once the person pauses.
 *
 * A preview `400` is also written onto the offending field (`Contents[i].Body` -> `contents.i.body`)
 * as an error of type `preview`, WITHOUT activating a tab or moving focus: typing is never blocked.
 * Those preview errors are cleared again when the next preview arrives.
 */
export function useLiveTemplatePreview(
  templateKey: string,
  form: UseFormReturn<TemplateFormValues>,
  sampleValues: Record<string, string>,
) {
  const { setError, clearErrors, getFieldState } = form;
  const values = form.watch() as TemplateFormValues;
  const requestJson = JSON.stringify({ contents: toRequestContents(values), sampleValues });
  const debouncedJson = useDebouncedValue(requestJson, PREVIEW_DEBOUNCE_MS);
  const request = useMemo(
    () => JSON.parse(debouncedJson) as PreviewTemplateRequest,
    [debouncedJson],
  );
  const preview = useTemplatePreview(templateKey, request, true);
  const markedFields = useRef<Path<TemplateFormValues>[]>([]);

  useEffect(() => {
    for (const name of markedFields.current) {
      if (getFieldState(name).error?.type === 'preview') clearErrors(name);
    }
    markedFields.current = [];
    if (!(preview.error instanceof ApiValidationError)) return;
    for (const violation of preview.error.response.errors) {
      const parsed = parseTemplateContentsProperty(violation.propertyName);
      if (!parsed) continue;
      const name = `contents.${parsed.index}.${parsed.field}` as Path<TemplateFormValues>;
      setError(name, { type: 'preview', message: violation.errorMessage });
      markedFields.current.push(name);
    }
  }, [preview.error, getFieldState, setError, clearErrors]);

  return preview;
}
