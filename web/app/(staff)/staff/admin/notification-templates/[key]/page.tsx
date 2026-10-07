'use client';

import { use } from 'react';
import { TemplateEditor } from '@/components/templates/TemplateEditor';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { Button } from '@/components/ui/button';
import { LoadingState } from '@/components/common/LoadingState';
import { ErrorState } from '@/components/common/ErrorState';
import {
  useNotificationTemplate,
  useReloadNotificationTemplate,
  useResetNotificationTemplate,
  useSaveNotificationTemplate,
} from '@/lib/hooks/useNotificationTemplates';
import { useStaffSession } from '@/lib/hooks/useSession';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';
import { TEMPLATE_MANAGE_ROLES } from '@/lib/api/admin-notification-templates';
import { viewerTimeZone } from '@/components/inbox/relativeTime';

/** `/staff/admin/notification-templates/[key]` — US-NTF-02. */
export default function NotificationTemplateEditorPage({
  params,
}: {
  params: Promise<{ key: string }>;
}) {
  const { key: rawKey } = use(params);
  const key = decodeURIComponent(rawKey);
  const { data: session, isLoading: sessionLoading } = useStaffSession();
  const { data: template, isLoading, isError, error, refetch } = useNotificationTemplate(key);
  const save = useSaveNotificationTemplate(key);
  const reset = useResetNotificationTemplate(key);
  const reload = useReloadNotificationTemplate(key);

  // `canManage` comes from the session roles, so wait for the session too: otherwise the editor would
  // render read-only and then flip to editable once the session arrives (NTF backlog N4).
  if (isLoading || sessionLoading) {
    return <LoadingState variant="card" rows={6} label="Loading template…" />;
  }
  if (error instanceof ApiForbiddenError) return <PermissionDeniedState />;
  if (error instanceof ApiNotFoundError) {
    return (
      <ErrorState
        title="Template not found"
        description="This template does not exist. Go back to the list and choose one."
      />
    );
  }
  // Only a template that was NEVER loaded gets the full-page error. Once data exists the editor stays
  // mounted through a failed background refetch or a failed Reload — unmounting it would throw away
  // everything the administrator typed (NFR-USA-5, "nothing typed is touched").
  if (!template) {
    return (
      <ErrorState
        description="We couldn't load this template just now."
        onRetry={() => refetch()}
      />
    );
  }

  const roleIds = session?.roleIds ?? [];
  return (
    <div className="flex flex-col gap-4">
      {isError && (
        <div
          role="status"
          className="flex flex-wrap items-center gap-3 rounded-[var(--radius-sm)] border border-danger-ink bg-danger-bg p-3 text-sm font-semibold text-danger-ink"
        >
          <span>
            We couldn&rsquo;t refresh this template just now &mdash; your changes are kept.
          </span>
          <Button type="button" variant="secondary" size="sm" onClick={() => refetch()}>
            Retry
          </Button>
        </div>
      )}
      <TemplateEditor
        template={template}
        canManage={TEMPLATE_MANAGE_ROLES.some((role) => roleIds.includes(role))}
        timeZoneId={viewerTimeZone()}
        onSave={(request) => save.mutateAsync(request)}
        onReset={(version) => reset.mutateAsync(version)}
        onReload={reload}
      />
    </div>
  );
}
