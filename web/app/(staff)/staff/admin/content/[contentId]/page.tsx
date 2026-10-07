'use client';

import { use } from 'react';
import { ContentEditor } from '@/components/content/ContentEditor';
import {
  useAdminContentItem,
  useEditContentDraft,
  usePublishContentItem,
  useUnpublishContentItem,
} from '@/lib/hooks/useAdminContent';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { LoadingState } from '@/components/common/LoadingState';
import { ErrorState } from '@/components/common/ErrorState';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';

/** `/staff/admin/content/[contentId]` — US-ADM-07. */
export default function ContentDetailPage({ params }: { params: Promise<{ contentId: string }> }) {
  const { contentId } = use(params);
  const { data: item, isLoading, error } = useAdminContentItem(contentId);
  const editMutation = useEditContentDraft(contentId);
  const publishMutation = usePublishContentItem(contentId);
  const unpublishMutation = useUnpublishContentItem(contentId);

  if (error instanceof ApiForbiddenError) return <PermissionDeniedState />;
  if (error instanceof ApiNotFoundError) {
    return <ErrorState title="Not found" description="This content item may have been removed." />;
  }
  if (isLoading || !item) return <LoadingState rows={6} />;

  return (
    <div className="flex flex-col gap-4">
      <h1 className="font-display text-xl font-semibold text-ink">{item.title}</h1>
      <ContentEditor
        kind={item.kind}
        item={item}
        isPending={
          editMutation.isPending || publishMutation.isPending || unpublishMutation.isPending
        }
        onSaveDraft={(values) =>
          editMutation.mutateAsync({
            title: values.title,
            draftBody: values.draftBody,
            placement: values.placement ?? null,
            linkUrl: values.linkUrl || null,
            visibleFrom: values.visibleFrom || null,
            visibleTo: values.visibleTo || null,
            sortOrder: values.sortOrder ?? null,
          })
        }
        onPublish={() => publishMutation.mutateAsync()}
        onUnpublish={() => unpublishMutation.mutateAsync()}
      />
    </div>
  );
}
