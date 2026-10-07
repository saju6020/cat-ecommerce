import type { UseFormReturn } from 'react-hook-form';
import { fieldId } from './TemplateFieldsPanel';
import {
  snippetFor,
  spliceText,
  type TemplateField,
  type TemplateFormValues,
} from '@/lib/validation/templateContent';
import type {
  NotificationTemplateVariable,
  TemplateChannel,
} from '@/lib/api/admin-notification-templates';

/**
 * `components/templates/insertPlaceholder.ts` — HO-NTF-002 §11.3 (`VariablePalette` click). Inserts
 * `{{name}}` — or, for a `Url` variable in the email body, `[Link text]({{name}})` — at the caret of
 * `field` in tab `index`, through react-hook-form's `setValue` (so the form is dirty and the preview
 * follows), then puts focus back in the field with the caret just after the inserted token. The
 * caret is read from the element itself: it survives the field losing focus to the palette button.
 * Nothing is substituted — the client only inserts tokens (ADR-FE-163).
 */
export function insertPlaceholder(
  form: UseFormReturn<TemplateFormValues>,
  index: number,
  channel: TemplateChannel,
  field: TemplateField,
  variable: NotificationTemplateVariable,
): void {
  const element = document.getElementById(fieldId(index, field)) as
    HTMLInputElement | HTMLTextAreaElement | null;
  if (!element || element.readOnly) return;
  const start = element.selectionStart ?? element.value.length;
  const end = element.selectionEnd ?? start;
  const snippet = snippetFor(variable, channel, field);
  form.setValue(`contents.${index}.${field}`, spliceText(element.value, start, end, snippet.text), {
    shouldDirty: true,
  });
  element.focus();
  element.setSelectionRange(start + snippet.selectStart, start + snippet.selectEnd);
}
