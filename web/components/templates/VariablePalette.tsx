'use client';

import { Badge } from '@/components/ui/badge';
import { CHANNEL_LABELS, placeholderToken } from '@/lib/validation/templateContent';
import type {
  NotificationTemplateVariable,
  TemplateChannel,
} from '@/lib/api/admin-notification-templates';

/**
 * `components/templates/VariablePalette.tsx` — ⭐ HO-NTF-002 §11.3, US-NTF-02. Lists the definition's
 * own variables and the standard ones: name, description, sample value, and a **Required** badge
 * (text) naming the channels whose body must contain it. Clicking a variable asks the editor to
 * insert `{{name}}` at the caret of the last focused field of the active tab — this component only
 * reports the click; the editor owns the form (`setValue`, caret restore). The client never
 * substitutes a value (ADR-FE-163).
 */
export function VariablePalette({
  variables,
  activeChannel,
  onInsert,
  disabled,
}: {
  variables: NotificationTemplateVariable[];
  activeChannel: TemplateChannel;
  onInsert: (variable: NotificationTemplateVariable) => void;
  disabled?: boolean;
}) {
  const own = variables.filter((variable) => !variable.isStandard);
  const standard = variables.filter((variable) => variable.isStandard);

  function renderGroup(title: string, group: NotificationTemplateVariable[]) {
    if (group.length === 0) return null;
    return (
      <section aria-label={title} className="flex flex-col gap-2">
        <h3 className="text-xs font-semibold uppercase tracking-wide text-ink-muted">{title}</h3>
        <ul className="m-0 flex list-none flex-col gap-2 p-0">
          {group.map((variable) => {
            const asLink = variable.kind === 'Url' && activeChannel === 'Email';
            return (
              <li
                key={variable.name}
                className="flex flex-col gap-1 rounded-[var(--radius-sm)] border border-border p-2"
              >
                <div className="flex flex-wrap items-center gap-2">
                  <button
                    type="button"
                    disabled={disabled}
                    onClick={() => onInsert(variable)}
                    aria-label={`Insert ${placeholderToken(variable)}${asLink ? ' as a link' : ''}`}
                    className="rounded-[var(--radius-sm)] bg-surface-sunken px-2 py-1 font-mono text-xs font-semibold text-ink hover:bg-accent-soft focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent disabled:opacity-50"
                  >
                    {placeholderToken(variable)}
                  </button>
                  {variable.requiredOn.length > 0 && (
                    <Badge variant="warning" className="whitespace-normal text-left">
                      Required on {variable.requiredOn.map((c) => CHANNEL_LABELS[c]).join(', ')}
                    </Badge>
                  )}
                </div>
                <p className="text-xs text-ink-muted">{variable.description}</p>
                <p className="break-all text-xs text-ink-muted">
                  Sample: <span className="text-ink">{variable.sample}</span>
                </p>
              </li>
            );
          })}
        </ul>
      </section>
    );
  }

  return (
    <div className="flex flex-col gap-4 rounded-[var(--radius-md)] border border-border bg-surface p-4">
      <h2 className="text-sm font-semibold text-ink">Placeholders</h2>
      <p className="text-xs text-ink-muted">
        Click one to add it where your cursor was in the {CHANNEL_LABELS[activeChannel]} message.
      </p>
      {renderGroup('This message', own)}
      {renderGroup('Available in every message', standard)}
    </div>
  );
}
