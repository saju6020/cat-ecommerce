import { Clock, Lock } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { formatInZone } from '@/lib/format/time';
import type { NotificationTemplateSummary } from '@/lib/api/admin-notification-templates';

export const SECRET_NOTICE =
  'Contains a password-reset link. It is never copied to the inbox and cannot be resent.';

/**
 * `components/templates/TemplateHeader.tsx` — HO-NTF-002 §11.3. Name, description, category, urgency
 * (*"Ignores quiet hours"* for urgent templates), whether the wording is customised, and — for a
 * secret-bearing template — the notice that the message never reaches the inbox and cannot be
 * resent (HO-NTF-001 §8.10). Status is always text, never colour alone.
 */
export function TemplateHeader({
  template,
  timeZoneId,
}: {
  template: NotificationTemplateSummary;
  timeZoneId: string;
}) {
  return (
    <header className="flex flex-col gap-2">
      <h1 className="font-display text-xl font-semibold text-ink">{template.displayName}</h1>
      <p className="text-sm text-ink-muted">{template.description}</p>
      <p className="font-mono text-xs text-ink-muted">{template.key}</p>
      <div className="flex flex-wrap items-center gap-2">
        <Badge variant="neutral">{template.category}</Badge>
        <Badge variant={template.isCustomised ? 'success' : 'neutral'}>
          {template.isCustomised ? 'Customised' : 'Default'}
        </Badge>
        {template.urgency === 'Urgent' && (
          <Badge variant="warning" icon={<Clock size={12} aria-hidden="true" />}>
            Ignores quiet hours
          </Badge>
        )}
        {template.isCustomised && template.lastEditedOn && (
          <span className="text-xs text-ink-muted">
            Last edited {formatInZone(template.lastEditedOn, timeZoneId)}
          </span>
        )}
      </div>
      {template.containsSecret && (
        <p
          data-testid="secret-notice"
          className="flex items-start gap-2 rounded-[var(--radius-sm)] bg-warning-bg px-3 py-2 text-sm text-warning-ink"
        >
          <Lock size={16} aria-hidden="true" className="mt-0.5 flex-none" />
          {SECRET_NOTICE}
        </p>
      )}
    </header>
  );
}
