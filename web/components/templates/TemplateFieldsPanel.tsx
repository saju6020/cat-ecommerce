'use client';

import type { FieldErrors, UseFormRegister } from 'react-hook-form';
import type {
  TemplateContentFormValue,
  TemplateField,
  TemplateFormValues,
} from '@/lib/validation/templateContent';
import { fieldsFor } from '@/lib/validation/templateContent';
import type {
  NotificationTemplateChannelLimits,
  TemplateChannel,
} from '@/lib/api/admin-notification-templates';

export function fieldId(index: number, field: TemplateField) {
  return `tpl-${index}-${field}`;
}

const LABELS: Record<TemplateChannel, Partial<Record<TemplateField, string>>> = {
  Email: { subject: 'Email subject', body: 'Email body' },
  Sms: { body: 'SMS message' },
  InApp: { title: 'In-app title', body: 'In-app message', linkPath: 'In-app link (optional)' },
  Push: { title: 'Push title', body: 'Push message' },
};

const INPUT_CLASS =
  'w-full rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink aria-[invalid=true]:border-danger-ink read-only:bg-surface-sunken';

/**
 * `components/templates/TemplateFieldsPanel.tsx` — HO-NTF-002 §11.3. The fields of ONE channel tab:
 * email -> Subject + Body; SMS -> Body; in-app -> Title + Body + Link; push -> Title + Body (with the
 * "not sent in this version" note). Each field shows a live `n / max` counter from the definition's
 * `limits`, and its error (a client length rule, a server `400` or the preview's `400`, all through
 * react-hook-form) linked with `aria-describedby`. Values are uncontrolled (`register`), so typing
 * never re-mounts anything and an error never clears what was typed (NFR-USA-5).
 */
export function TemplateFieldsPanel({
  index,
  channel,
  register,
  errors,
  values,
  limits,
  readOnly,
  onFieldFocus,
}: {
  index: number;
  channel: TemplateChannel;
  register: UseFormRegister<TemplateFormValues>;
  errors: FieldErrors<TemplateFormValues>;
  values: TemplateContentFormValue | undefined;
  limits: NotificationTemplateChannelLimits | undefined;
  readOnly: boolean;
  onFieldFocus: (index: number, field: TemplateField) => void;
}) {
  const maxFor = (field: TemplateField): number | null =>
    field === 'subject'
      ? (limits?.subjectMax ?? null)
      : field === 'title'
        ? (limits?.titleMax ?? null)
        : field === 'body'
          ? (limits?.bodyMax ?? null)
          : null;

  return (
    <div className="flex flex-col gap-4 rounded-b-[var(--radius-md)] border border-border bg-surface p-4">
      {channel === 'Push' && (
        <p className="rounded-[var(--radius-sm)] bg-warning-bg px-3 py-2 text-sm text-warning-ink">
          Push messages are not sent in this version.
        </p>
      )}
      {fieldsFor(channel).map((field) => {
        const id = fieldId(index, field);
        const error = errors.contents?.[index]?.[field]?.message;
        const max = maxFor(field);
        const length = values?.[field]?.length ?? 0;
        const describedBy = [max ? `${id}-count` : null, error ? `${id}-error` : null]
          .filter(Boolean)
          .join(' ');
        const common = {
          id,
          readOnly,
          'aria-invalid': error ? true : undefined,
          'aria-describedby': describedBy || undefined,
          className: INPUT_CLASS,
          ...register(`contents.${index}.${field}` as const),
          onFocus: () => onFieldFocus(index, field),
        };
        return (
          <div key={field} className="flex flex-col gap-1.5">
            <label htmlFor={id} className="text-sm font-semibold text-ink">
              {LABELS[channel][field]}
            </label>
            {field === 'body' ? (
              <textarea {...common} rows={channel === 'Email' ? 10 : 5} />
            ) : (
              <input {...common} type="text" />
            )}
            <div className="flex flex-wrap justify-between gap-2">
              {error ? (
                <p id={`${id}-error`} className="text-sm font-semibold text-danger-ink">
                  {error}
                </p>
              ) : (
                <span />
              )}
              {max !== null && (
                <span
                  id={`${id}-count`}
                  className={
                    length > max
                      ? 'text-xs font-semibold text-danger-ink'
                      : 'text-xs text-ink-muted'
                  }
                >
                  {length} / {max} characters
                </span>
              )}
            </div>
          </div>
        );
      })}
    </div>
  );
}
