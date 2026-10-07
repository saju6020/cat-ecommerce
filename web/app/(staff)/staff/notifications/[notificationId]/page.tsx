'use client';

import { use } from 'react';
import Link from 'next/link';
import { NotificationDetail } from '@/components/notifications/NotificationDetail';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { useNotificationDetail, useResendNotification } from '@/lib/hooks/useNotificationLog';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';

/** `/staff/notifications/[notificationId]` — US-NTF-06: the full record, every delivery attempt,
 *  the fallback / resend chain and **Resend**. */
export default function NotificationDetailPage({
  params,
}: {
  params: Promise<{ notificationId: string }>;
}) {
  const { notificationId } = use(params);
  const timeZoneId = useBranchTimeZone();
  const { data, isLoading, isError, error, refetch } = useNotificationDetail(notificationId);
  const resend = useResendNotification(notificationId);

  if (isLoading) return <LoadingState variant="card" rows={6} label="Loading message…" />;
  if (error instanceof ApiForbiddenError) return <PermissionDeniedState />;
  if (error instanceof ApiNotFoundError) {
    return (
      <ErrorState
        title="Message not found"
        description="This message may be outside the retention period. Go back to the log and search again."
      />
    );
  }
  if (isError || !data) {
    return (
      <ErrorState description="We couldn't load this message just now." onRetry={() => refetch()} />
    );
  }

  return (
    <div className="flex flex-col gap-4">
      <Link
        href="/staff/notifications"
        className="w-fit text-sm font-semibold text-accent-ink underline underline-offset-2"
      >
        Back to messages
      </Link>
      <NotificationDetail
        detail={data}
        timeZoneId={timeZoneId}
        onResend={(channel) => resend.mutateAsync(channel)}
      />
    </div>
  );
}
