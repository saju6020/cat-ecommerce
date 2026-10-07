'use client';

import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { ReviewModerationQueue } from '@/components/staff-shop/ReviewModerationQueue';
import { ApiForbiddenError } from '@/lib/api/client';
import { useModerateReview, useModerationQueue } from '@/lib/hooks/useStaffShop';

/** `/staff/shop/reviews` — the moderation queue (US-SHP-09, `Reviews.Moderate`). */
export default function StaffReviewsPage() {
  const queue = useModerationQueue({});
  const moderate = useModerateReview();
  if (queue.error instanceof ApiForbiddenError) return <PermissionDeniedState />;
  if (queue.isError) return <ErrorState onRetry={() => queue.refetch()} />;
  if (!queue.data) return <LoadingState rows={4} />;
  return (
    <div className="flex flex-col gap-4">
      <h1 className="font-display text-2xl font-semibold text-ink">Review moderation</h1>
      <ReviewModerationQueue
        items={queue.data.items}
        onAct={(action) => moderate.mutateAsync(action)}
      />
    </div>
  );
}
