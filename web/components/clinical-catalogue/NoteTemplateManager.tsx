'use client';

import { useState } from 'react';
import { useSearchParams } from 'next/navigation';
import { Ban, CircleCheck } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { toast } from '@/components/ui/use-toast';
import { DataTable, type DataTableColumn } from '@/components/common/DataTable';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { CatalogueDialog } from './CatalogueDialog';
import { CatalogueField } from './CatalogueField';
import { ApiForbiddenError } from '@/lib/api/client';
import { describeClinicalError, mapClinicalFormErrors } from '@/lib/api/errors';
import { useAdminServices } from '@/lib/hooks/useAdminCatalogue';
import {
  useNoteTemplates,
  useSaveNoteTemplate,
  useSetNoteTemplateActive,
} from '@/lib/hooks/useClinicalCatalogue';
import { useFilterUrlUpdate } from '@/lib/hooks/useFilterUrlUpdate';
import { useStaffSession } from '@/lib/hooks/useSession';
import { MAX_DISCHARGE_INSTRUCTIONS, MAX_SOAP_FIELD } from '@/lib/validation/clinical';
import type { NoteTemplate } from '@/lib/api/clinical-catalogue';

/**
 * ⭐ `NoteTemplateManager` — HO-EMR-002 §11.8 (US-CLN-20). One ACTIVE template per service, versioned.
 * `NoteTemplateForm`: the service (when adding, only services WITHOUT an active template are offered),
 * a name and the five text fields; editing shows the template version, which the server bumps — the
 * applied version is recorded on each note, so changing a template never rewrites an old note.
 */
const PAGE_SIZE = 20;
const FIELD_MAP = {
  ServiceId: 'serviceId',
  Name: 'name',
  Subjective: 'subjective',
  Objective: 'objective',
  Assessment: 'assessment',
  Plan: 'plan',
  DischargeInstructions: 'dischargeInstructions',
};

export function NoteTemplateManager() {
  const params = useSearchParams();
  const setParam = useFilterUrlUpdate();
  const roles = useStaffSession().data?.roleIds ?? [];
  const canManage = roles.includes('Vet') || roles.includes('Administrator');
  const page = Math.max(1, Number(params.get('page')) || 1);
  const activeOnly = params.get('active') === '1';
  const query = useNoteTemplates({ activeOnly, pageNumber: page, pageSize: PAGE_SIZE });
  const setActive = useSetNoteTemplateActive();
  const [editing, setEditing] = useState<NoteTemplate | 'new' | null>(null);

  if (query.error instanceof ApiForbiddenError) return <PermissionDeniedState />;

  const columns: Array<DataTableColumn<NoteTemplate>> = [
    {
      id: 'service',
      header: 'Service',
      cell: (row) => (
        <button
          type="button"
          className="font-semibold text-accent-ink underline underline-offset-2 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          onClick={() => setEditing(row)}
          aria-label={`Edit the ${row.serviceName} template`}
        >
          {row.serviceName}
        </button>
      ),
    },
    { id: 'name', header: 'Template', cell: (row) => row.name },
    { id: 'version', header: 'Version', cell: (row) => `Version ${row.templateVersion}` },
    {
      id: 'status',
      header: 'Status',
      cell: (row) =>
        row.isActive ? (
          <Badge variant="success" icon={<CircleCheck size={12} aria-hidden="true" />}>
            Active
          </Badge>
        ) : (
          <Badge variant="neutral" icon={<Ban size={12} aria-hidden="true" />}>
            Retired
          </Badge>
        ),
    },
    ...(canManage
      ? [
          {
            id: 'actions',
            header: 'Actions',
            cell: (row: NoteTemplate) => (
              <Button
                variant="ghost"
                size="sm"
                disabled={setActive.isPending}
                aria-label={`${row.isActive ? 'Retire' : 'Reactivate'} the ${row.serviceName} template`}
                onClick={() =>
                  setActive.mutate(
                    { id: row.id, isActive: !row.isActive },
                    {
                      onError: (error) =>
                        toast({ title: 'Not changed', description: describeClinicalError(error) }),
                    },
                  )
                }
              >
                {row.isActive ? 'Retire' : 'Reactivate'}
              </Button>
            ),
          },
        ]
      : []),
  ];

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="font-display text-2xl font-semibold text-ink">Note templates</h1>
        {canManage && <Button onClick={() => setEditing('new')}>Add a template</Button>}
      </div>
      <label className="inline-flex items-center gap-2 text-sm font-semibold text-ink">
        <input
          type="checkbox"
          checked={activeOnly}
          onChange={(e) => setParam('active', e.target.checked ? '1' : '')}
        />
        Active templates only
      </label>
      <DataTable
        caption="Note templates"
        columns={columns}
        rows={query.data?.items}
        getRowId={(row) => row.id}
        isLoading={query.isLoading}
        isError={query.isError}
        onRetry={() => void query.refetch()}
        pagination={{ totalCount: query.data?.totalCount ?? 0, pageSize: PAGE_SIZE }}
        emptyState={{
          title: 'No note templates yet.',
          description: 'A template pre-fills a new consultation note for its service.',
        }}
      />
      {editing && (
        <NoteTemplateForm
          key={editing === 'new' ? 'new' : editing.id}
          existing={editing === 'new' ? null : editing}
          onClose={() => setEditing(null)}
        />
      )}
    </div>
  );
}

const TEXT_FIELDS = [
  ['subjective', 'Subjective', MAX_SOAP_FIELD],
  ['objective', 'Objective', MAX_SOAP_FIELD],
  ['assessment', 'Assessment', MAX_SOAP_FIELD],
  ['plan', 'Plan', MAX_SOAP_FIELD],
  ['dischargeInstructions', 'Home-care instructions', MAX_DISCHARGE_INSTRUCTIONS],
] as const;

function NoteTemplateForm({
  existing,
  onClose,
}: {
  existing: NoteTemplate | null;
  onClose: () => void;
}) {
  const save = useSaveNoteTemplate(existing?.id ?? null);
  const services = useAdminServices();
  const active = useNoteTemplates({ activeOnly: true, pageSize: 100 }, !existing);
  const takenServiceIds = new Set((active.data?.items ?? []).map((t) => t.serviceId));
  const offered = (services.data ?? []).filter((s) => s.isActive && !takenServiceIds.has(s.id));
  const [values, setValues] = useState({
    serviceId: existing?.serviceId ?? '',
    name: existing?.name ?? '',
    subjective: existing?.subjective ?? '',
    objective: existing?.objective ?? '',
    assessment: existing?.assessment ?? '',
    plan: existing?.plan ?? '',
    dischargeInstructions: existing?.dischargeInstructions ?? '',
  });
  const [errors, setErrors] = useState<{ fields: Record<string, string>; root: string | null }>({
    fields: {},
    root: null,
  });
  const set = (patch: Partial<typeof values>) => setValues((v) => ({ ...v, ...patch }));

  async function submit() {
    const fields: Record<string, string> = {};
    if (!existing && !values.serviceId)
      fields.serviceId = 'Choose the service this template is for.';
    if (!values.name.trim()) fields.name = 'Enter a name.';
    if (Object.keys(fields).length > 0) return setErrors({ fields, root: null });
    try {
      await save.mutateAsync({
        ...(existing ? {} : { serviceId: values.serviceId }),
        name: values.name.trim(),
        subjective: values.subjective,
        objective: values.objective,
        assessment: values.assessment,
        plan: values.plan,
        dischargeInstructions: values.dischargeInstructions,
      });
      toast({ title: existing ? 'Template saved' : 'Template added' });
      onClose();
    } catch (error) {
      setErrors(mapClinicalFormErrors(error, FIELD_MAP));
    }
  }

  return (
    <CatalogueDialog
      open
      onOpenChange={(open) => !open && onClose()}
      title={existing ? `Edit the ${existing.serviceName} template` : 'Add a note template'}
      description={
        existing
          ? `Template version ${existing.templateVersion}. Saving creates the next version.`
          : 'Only services without an active template are offered.'
      }
      formId="note-template-form"
      pending={save.isPending}
      rootError={errors.root}
    >
      <form
        id="note-template-form"
        noValidate
        className="flex flex-col gap-3"
        onSubmit={(e) => {
          e.preventDefault();
          void submit();
        }}
      >
        {!existing && (
          <CatalogueField id="nt-service" label="Service" error={errors.fields.serviceId}>
            {(p) => (
              <select
                {...p}
                value={values.serviceId}
                onChange={(e) => set({ serviceId: e.target.value })}
              >
                <option value="">Choose a service…</option>
                {offered.map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.name}
                  </option>
                ))}
              </select>
            )}
          </CatalogueField>
        )}
        <CatalogueField id="nt-name" label="Template name" error={errors.fields.name}>
          {(p) => (
            <input
              {...p}
              type="text"
              maxLength={200}
              value={values.name}
              onChange={(e) => set({ name: e.target.value })}
            />
          )}
        </CatalogueField>
        {TEXT_FIELDS.map(([key, label, max]) => (
          <CatalogueField key={key} id={`nt-${key}`} label={label} error={errors.fields[key]}>
            {(p) => (
              <textarea
                {...p}
                rows={3}
                maxLength={max}
                value={values[key]}
                onChange={(e) => set({ [key]: e.target.value })}
              />
            )}
          </CatalogueField>
        ))}
      </form>
    </CatalogueDialog>
  );
}
