'use client';

import { useState } from 'react';
import { useSearchParams } from 'next/navigation';
import { Ban, CircleCheck, Lock } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { toast } from '@/components/ui/use-toast';
import { DataTable, type DataTableColumn } from '@/components/common/DataTable';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { CatalogueDialog } from './CatalogueDialog';
import { CatalogueField } from './CatalogueField';
import { ApiForbiddenError } from '@/lib/api/client';
import { describeClinicalError, mapClinicalFormErrors } from '@/lib/api/errors';
import {
  useDiagnosisCodes,
  useSaveDiagnosisCode,
  useSetDiagnosisCodeActive,
} from '@/lib/hooks/useClinicalCatalogue';
import { useFilterUrlUpdate } from '@/lib/hooks/useFilterUrlUpdate';
import { useStaffSession } from '@/lib/hooks/useSession';
import type { DiagnosisCode } from '@/lib/api/clinical-catalogue';

/**
 * ⭐ `DiagnosisCodeManager` — HO-EMR-002 §11.8. The diagnosis-code catalogue: search, an *Active only*
 * filter and paging (all in the URL, UI-4), an editor in a dialog (`DiagnosisCodeForm`) and
 * activate / deactivate. The code is UPPER-CASE and LOCKED once created — to change it, deactivate the
 * code and add a new one (a built-in code can't be changed at all). Reads need View (nurse, vet,
 * administrator); only Vet and Administrator see the write controls (the server still decides).
 */
const PAGE_SIZE = 20;
const FIELD_MAP = {
  Code: 'code',
  Name: 'name',
  Category: 'category',
  ExternalSystem: 'externalSystem',
  ExternalCode: 'externalCode',
};

export function DiagnosisCodeManager() {
  const params = useSearchParams();
  const setParam = useFilterUrlUpdate();
  const roles = useStaffSession().data?.roleIds ?? [];
  const canManage = roles.includes('Vet') || roles.includes('Administrator');
  const page = Math.max(1, Number(params.get('page')) || 1);
  const activeOnly = params.get('active') === '1';
  const query = useDiagnosisCodes({
    search: params.get('q') ?? undefined,
    activeOnly,
    pageNumber: page,
    pageSize: PAGE_SIZE,
  });
  const setActive = useSetDiagnosisCodeActive();
  const [editing, setEditing] = useState<DiagnosisCode | 'new' | null>(null);

  if (query.error instanceof ApiForbiddenError) return <PermissionDeniedState />;

  const columns: Array<DataTableColumn<DiagnosisCode>> = [
    {
      id: 'code',
      header: 'Code',
      cell: (row) => (
        <button
          type="button"
          className="font-semibold text-accent-ink underline underline-offset-2 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          onClick={() => setEditing(row)}
          aria-label={`Edit ${row.code}`}
        >
          {row.code}
        </button>
      ),
    },
    { id: 'name', header: 'Name', cell: (row) => row.name },
    { id: 'category', header: 'Category', cell: (row) => row.category ?? 'None recorded' },
    {
      id: 'external',
      header: 'External code',
      cell: (row) =>
        row.externalCode
          ? `${row.externalSystem ?? ''} ${row.externalCode}`.trim()
          : 'None recorded',
    },
    {
      id: 'status',
      header: 'Status',
      cell: (row) => (
        <span className="inline-flex flex-wrap items-center gap-1.5">
          {row.isActive ? (
            <Badge variant="success" icon={<CircleCheck size={12} aria-hidden="true" />}>
              Active
            </Badge>
          ) : (
            <Badge variant="neutral" icon={<Ban size={12} aria-hidden="true" />}>
              Retired
            </Badge>
          )}
          {row.isSystem && (
            <Badge variant="neutral" icon={<Lock size={12} aria-hidden="true" />}>
              Built in
            </Badge>
          )}
        </span>
      ),
    },
    ...(canManage
      ? [
          {
            id: 'actions',
            header: 'Actions',
            cell: (row: DiagnosisCode) =>
              row.isSystem ? null : (
                <Button
                  variant="ghost"
                  size="sm"
                  disabled={setActive.isPending}
                  aria-label={`${row.isActive ? 'Retire' : 'Reactivate'} ${row.code}`}
                  onClick={() =>
                    setActive.mutate(
                      { id: row.id, isActive: !row.isActive },
                      {
                        onError: (error) =>
                          toast({
                            title: 'Not changed',
                            description: describeClinicalError(error),
                          }),
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
        <h1 className="font-display text-2xl font-semibold text-ink">Diagnosis codes</h1>
        {canManage && <Button onClick={() => setEditing('new')}>Add a code</Button>}
      </div>
      <label className="inline-flex items-center gap-2 text-sm font-semibold text-ink">
        <input
          type="checkbox"
          checked={activeOnly}
          onChange={(e) => setParam('active', e.target.checked ? '1' : '')}
        />
        Active codes only
      </label>
      <DataTable
        caption="Diagnosis codes"
        columns={columns}
        rows={query.data?.items}
        getRowId={(row) => row.id}
        isLoading={query.isLoading}
        isError={query.isError}
        onRetry={() => void query.refetch()}
        search={{ label: 'Search diagnosis codes', placeholder: 'Code or name' }}
        pagination={{ totalCount: query.data?.totalCount ?? 0, pageSize: PAGE_SIZE }}
        emptyState={{
          title: 'No diagnosis codes match.',
          description: 'Try a different search, or add the code.',
        }}
      />
      {editing && (
        <DiagnosisCodeForm
          key={editing === 'new' ? 'new' : editing.id}
          existing={editing === 'new' ? null : editing}
          onClose={() => setEditing(null)}
        />
      )}
    </div>
  );
}

function DiagnosisCodeForm({
  existing,
  onClose,
}: {
  existing: DiagnosisCode | null;
  onClose: () => void;
}) {
  const save = useSaveDiagnosisCode(existing?.id ?? null);
  const [values, setValues] = useState({
    code: existing?.code ?? '',
    name: existing?.name ?? '',
    category: existing?.category ?? '',
    externalSystem: existing?.externalSystem ?? '',
    externalCode: existing?.externalCode ?? '',
  });
  const [errors, setErrors] = useState<{ fields: Record<string, string>; root: string | null }>({
    fields: {},
    root: null,
  });
  const readOnly = existing?.isSystem ?? false;
  const set = (patch: Partial<typeof values>) => setValues((v) => ({ ...v, ...patch }));

  async function submit() {
    const fields: Record<string, string> = {};
    if (!existing && !values.code.trim()) fields.code = 'Enter a code.';
    if (!values.name.trim()) fields.name = 'Enter a name.';
    if (Object.keys(fields).length > 0) return setErrors({ fields, root: null });
    try {
      await save.mutateAsync({
        ...(existing ? {} : { code: values.code.trim().toUpperCase() }),
        name: values.name.trim(),
        category: values.category.trim() || undefined,
        externalSystem: values.externalSystem.trim() || undefined,
        externalCode: values.externalCode.trim() || undefined,
      });
      toast({ title: existing ? 'Code saved' : 'Code added' });
      onClose();
    } catch (error) {
      setErrors(mapClinicalFormErrors(error, FIELD_MAP));
    }
  }

  return (
    <CatalogueDialog
      open
      onOpenChange={(open) => !open && onClose()}
      title={existing ? `Edit ${existing.code}` : 'Add a diagnosis code'}
      formId="diagnosis-code-form"
      pending={save.isPending}
      rootError={errors.root}
    >
      <form
        id="diagnosis-code-form"
        noValidate
        className="flex flex-col gap-3"
        onSubmit={(e) => {
          e.preventDefault();
          void submit();
        }}
      >
        {readOnly && (
          <p role="status" className="text-sm text-ink-muted">
            This is a built-in code and cannot be changed.
          </p>
        )}
        <CatalogueField
          id="dc-code"
          label="Code"
          error={errors.fields.code}
          hint={
            existing
              ? 'The code is locked once created. To replace it, retire this code and add a new one.'
              : 'Upper-case letters, digits and dashes, e.g. EAR-OTE.'
          }
        >
          {(p) => (
            <input
              {...p}
              type="text"
              maxLength={30}
              value={values.code}
              disabled={!!existing}
              onChange={(e) => set({ code: e.target.value.toUpperCase() })}
            />
          )}
        </CatalogueField>
        <CatalogueField id="dc-name" label="Name" error={errors.fields.name}>
          {(p) => (
            <input
              {...p}
              type="text"
              maxLength={200}
              value={values.name}
              disabled={readOnly}
              onChange={(e) => set({ name: e.target.value })}
            />
          )}
        </CatalogueField>
        <CatalogueField id="dc-category" label="Category (optional)" error={errors.fields.category}>
          {(p) => (
            <input
              {...p}
              type="text"
              maxLength={100}
              value={values.category}
              disabled={readOnly}
              onChange={(e) => set({ category: e.target.value })}
            />
          )}
        </CatalogueField>
        <div className="grid gap-3 min-[460px]:grid-cols-2">
          <CatalogueField
            id="dc-system"
            label="External system (optional)"
            error={errors.fields.externalSystem}
          >
            {(p) => (
              <input
                {...p}
                type="text"
                maxLength={50}
                value={values.externalSystem}
                disabled={readOnly}
                onChange={(e) => set({ externalSystem: e.target.value })}
              />
            )}
          </CatalogueField>
          <CatalogueField
            id="dc-external"
            label="External code (optional)"
            error={errors.fields.externalCode}
          >
            {(p) => (
              <input
                {...p}
                type="text"
                maxLength={50}
                value={values.externalCode}
                disabled={readOnly}
                onChange={(e) => set({ externalCode: e.target.value })}
              />
            )}
          </CatalogueField>
        </div>
      </form>
    </CatalogueDialog>
  );
}
