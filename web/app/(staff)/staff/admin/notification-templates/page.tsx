'use client';

import { TemplateTable } from '@/components/templates/TemplateTable';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { useNotificationTemplates } from '@/lib/hooks/useNotificationTemplates';
import { ApiForbiddenError } from '@/lib/api/client';
import { viewerTimeZone } from '@/components/inbox/relativeTime';

/** `/staff/admin/notification-templates` — US-NTF-02. `NotificationTemplates.View`; a server `403`
 *  renders `PermissionDeniedState` (the nav entry is presentation only). */
export default function NotificationTemplatesPage() {
  const { data, isLoading, isError, error, refetch } = useNotificationTemplates();

  if (error instanceof ApiForbiddenError) return <PermissionDeniedState />;

  return (
    <div className="flex flex-col gap-4">
      <h1 className="font-display text-xl font-semibold text-ink">Message templates</h1>
      <p className="text-sm text-ink-muted">
        The wording of the text messages, emails and in-app messages the system sends. Changing a
        template affects messages sent from then on.
      </p>
      <TemplateTable
        templates={data}
        isLoading={isLoading}
        isError={isError}
        onRetry={() => refetch()}
        timeZoneId={viewerTimeZone()}
      />
    </div>
  );
}
