'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { ContentEditor } from '@/components/content/ContentEditor';
import { useCreateContentItem } from '@/lib/hooks/useAdminContent';
import type { ContentKind } from '@/lib/api/admin-content';

/** `/staff/admin/content/new` — US-ADM-07. */
export default function NewContentPage() {
  const router = useRouter();
  const [kind, setKind] = useState<ContentKind>('Page');
  const createMutation = useCreateContentItem();

  return (
    <div className="flex flex-col gap-4">
      <h1 className="font-display text-xl font-semibold text-ink">New content</h1>

      <div className="flex flex-col gap-1.5">
        <label htmlFor="new-content-kind" className="text-sm font-semibold text-ink">
          Kind
        </label>
        <select
          id="new-content-kind"
          value={kind}
          onChange={(e) => setKind(e.target.value as ContentKind)}
          className="w-fit rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
        >
          <option value="Page">Page</option>
          <option value="Faq">FAQ</option>
          <option value="Banner">Banner</option>
        </select>
      </div>

      <ContentEditor
        kind={kind}
        isPending={createMutation.isPending}
        onCreate={async (values) => {
          const result = await createMutation.mutateAsync({
            kind,
            slug: values.slug,
            title: values.title,
            draftBody: values.draftBody,
            placement: values.placement ?? null,
            linkUrl: values.linkUrl || null,
            visibleFrom: values.visibleFrom || null,
            visibleTo: values.visibleTo || null,
            sortOrder: values.sortOrder ?? null,
          });
          router.push(`/staff/admin/content/${result.contentId}`);
        }}
      />
    </div>
  );
}
