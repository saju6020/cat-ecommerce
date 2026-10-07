import { render } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { TemplateEditor } from '@/components/templates/TemplateEditor';
import {
  useNotificationTemplate,
  useReloadNotificationTemplate,
  useResetNotificationTemplate,
  useSaveNotificationTemplate,
} from '@/lib/hooks/useNotificationTemplates';

/** Stand-in for app/(staff)/staff/admin/notification-templates/[key]/page.tsx — real hooks, real
 *  editor, MSW behind both. */
function EditorHarness({ templateKey, canManage }: { templateKey: string; canManage: boolean }) {
  const { data } = useNotificationTemplate(templateKey);
  const save = useSaveNotificationTemplate(templateKey);
  const reset = useResetNotificationTemplate(templateKey);
  const reload = useReloadNotificationTemplate(templateKey);
  if (!data) return <p>Loading template</p>;
  return (
    <TemplateEditor
      template={data}
      canManage={canManage}
      timeZoneId="Asia/Dhaka"
      onSave={(request) => save.mutateAsync(request)}
      onReset={(version) => reset.mutateAsync(version)}
      onReload={reload}
    />
  );
}

export function renderEditor(templateKey: string, canManage = true) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <EditorHarness templateKey={templateKey} canManage={canManage} />
    </QueryClientProvider>,
  );
}
