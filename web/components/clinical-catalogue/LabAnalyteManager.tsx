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
import { MeasurementInput } from '@/components/visit/MeasurementInput';
import { ApiForbiddenError } from '@/lib/api/client';
import { describeClinicalError, mapClinicalFormErrors } from '@/lib/api/errors';
import {
  useLabAnalytes,
  useSaveLabAnalyte,
  useSetLabAnalyteActive,
} from '@/lib/hooks/useClinicalCatalogue';
import { useFilterUrlUpdate } from '@/lib/hooks/useFilterUrlUpdate';
import { useStaffSession } from '@/lib/hooks/useSession';
import { DIAGNOSTIC_KINDS, diagnosticKindLabel, formatRange } from '@/lib/format/clinical';
import type { LabAnalyte } from '@/lib/api/clinical-catalogue';

/**
 * ⭐ `LabAnalyteManager` — HO-EMR-002 §11.8 (US-CLN-14). The lab-analyte catalogue: search, *Active only*,
 * paging, and `LabAnalyteForm` in a dialog — kind (number or text result), the diagnostic kind, the
 * unit, a reference range with **low ≤ high** (refused on the field before and after the server), and
 * the normal text for a text result. The range shows its unit everywhere (UI-8).
 */
const PAGE_SIZE = 20;
const FIELD_MAP = {
  Code: 'code',
  Name: 'name',
  Unit: 'unit',
  ReferenceLow: 'low',
  ReferenceHigh: 'high',
  NormalText: 'normalText',
};

export function LabAnalyteManager() {
  const params = useSearchParams();
  const setParam = useFilterUrlUpdate();
  const roles = useStaffSession().data?.roleIds ?? [];
  const canManage = roles.includes('Vet') || roles.includes('Administrator');
  const page = Math.max(1, Number(params.get('page')) || 1);
  const activeOnly = params.get('active') === '1';
  const query = useLabAnalytes({
    search: params.get('q') ?? undefined,
    activeOnly,
    pageNumber: page,
    pageSize: PAGE_SIZE,
  });
  const setActive = useSetLabAnalyteActive();
  const [editing, setEditing] = useState<LabAnalyte | 'new' | null>(null);

  if (query.error instanceof ApiForbiddenError) return <PermissionDeniedState />;

  const columns: Array<DataTableColumn<LabAnalyte>> = [
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
    { id: 'kind', header: 'Test kind', cell: (row) => diagnosticKindLabel(row.diagnosticKind) },
    {
      id: 'range',
      header: 'Reference range',
      cell: (row) =>
        row.valueKind === 'Numeric'
          ? (formatRange(row.referenceLow, row.referenceHigh, row.unit) ?? 'No range stated')
          : row.normalText
            ? `Normal: ${row.normalText}`
            : 'Text result',
    },
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
            cell: (row: LabAnalyte) => (
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
        <h1 className="font-display text-2xl font-semibold text-ink">Lab analytes</h1>
        {canManage && <Button onClick={() => setEditing('new')}>Add an analyte</Button>}
      </div>
      <label className="inline-flex items-center gap-2 text-sm font-semibold text-ink">
        <input
          type="checkbox"
          checked={activeOnly}
          onChange={(e) => setParam('active', e.target.checked ? '1' : '')}
        />
        Active analytes only
      </label>
      <DataTable
        caption="Lab analytes"
        columns={columns}
        rows={query.data?.items}
        getRowId={(row) => row.id}
        isLoading={query.isLoading}
        isError={query.isError}
        onRetry={() => void query.refetch()}
        search={{ label: 'Search lab analytes', placeholder: 'Code or name' }}
        pagination={{ totalCount: query.data?.totalCount ?? 0, pageSize: PAGE_SIZE }}
        emptyState={{
          title: 'No lab analytes match.',
          description: 'Try a different search, or add the analyte.',
        }}
      />
      {editing && (
        <LabAnalyteForm
          key={editing === 'new' ? 'new' : editing.id}
          existing={editing === 'new' ? null : editing}
          onClose={() => setEditing(null)}
        />
      )}
    </div>
  );
}

function LabAnalyteForm({
  existing,
  onClose,
}: {
  existing: LabAnalyte | null;
  onClose: () => void;
}) {
  const save = useSaveLabAnalyte(existing?.id ?? null);
  const [values, setValues] = useState({
    code: existing?.code ?? '',
    name: existing?.name ?? '',
    valueKind: existing?.valueKind ?? ('Numeric' as 'Numeric' | 'Qualitative'),
    diagnosticKind: existing?.diagnosticKind ?? 'BloodWork',
    unit: existing?.unit ?? '',
    low: existing?.referenceLow ?? '',
    high: existing?.referenceHigh ?? '',
    normalText: existing?.normalText ?? '',
  });
  const [errors, setErrors] = useState<{ fields: Record<string, string>; root: string | null }>({
    fields: {},
    root: null,
  });
  const set = (patch: Partial<typeof values>) => setValues((v) => ({ ...v, ...patch }));
  const numeric = values.valueKind === 'Numeric';
  const unit = values.unit.trim() || 'unit';

  async function submit() {
    const fields: Record<string, string> = {};
    if (!existing && !values.code.trim()) fields.code = 'Enter a code.';
    if (!values.name.trim()) fields.name = 'Enter a name.';
    if (numeric && !values.unit.trim()) fields.unit = 'Enter the unit this test is measured in.';
    if (
      numeric &&
      values.low !== '' &&
      values.high !== '' &&
      Number(values.low) > Number(values.high)
    )
      fields.low = 'The low value must not be higher than the high value.';
    if (Object.keys(fields).length > 0) return setErrors({ fields, root: null });
    try {
      await save.mutateAsync({
        ...(existing ? {} : { code: values.code.trim().toUpperCase() }),
        name: values.name.trim(),
        valueKind: values.valueKind,
        diagnosticKind: values.diagnosticKind,
        unit: numeric ? values.unit.trim() : undefined,
        referenceLow: numeric && values.low !== '' ? Number(values.low) : undefined,
        referenceHigh: numeric && values.high !== '' ? Number(values.high) : undefined,
        normalText: !numeric && values.normalText.trim() ? values.normalText.trim() : undefined,
      });
      toast({ title: existing ? 'Analyte saved' : 'Analyte added' });
      onClose();
    } catch (error) {
      setErrors(mapClinicalFormErrors(error, FIELD_MAP));
    }
  }

  return (
    <CatalogueDialog
      open
      onOpenChange={(open) => !open && onClose()}
      title={existing ? `Edit ${existing.code}` : 'Add a lab analyte'}
      formId="lab-analyte-form"
      pending={save.isPending}
      rootError={errors.root}
    >
      <form
        id="lab-analyte-form"
        noValidate
        className="flex flex-col gap-3"
        onSubmit={(e) => {
          e.preventDefault();
          void submit();
        }}
      >
        <CatalogueField
          id="la-code"
          label="Code"
          error={errors.fields.code}
          hint={existing ? 'The code is locked once created.' : undefined}
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
        <CatalogueField id="la-name" label="Name" error={errors.fields.name}>
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
        <div className="grid gap-3 min-[460px]:grid-cols-2">
          <CatalogueField id="la-kind" label="Test kind">
            {(p) => (
              <select
                {...p}
                value={values.diagnosticKind}
                onChange={(e) => set({ diagnosticKind: e.target.value })}
              >
                {DIAGNOSTIC_KINDS.map((k) => (
                  <option key={k} value={k}>
                    {diagnosticKindLabel(k)}
                  </option>
                ))}
              </select>
            )}
          </CatalogueField>
          <fieldset className="m-0 flex flex-col gap-1.5 border-0 p-0">
            <legend className="text-sm font-semibold text-ink">Result is</legend>
            <div className="flex flex-wrap gap-4">
              {(['Numeric', 'Qualitative'] as const).map((k) => (
                <label key={k} className="inline-flex items-center gap-1.5 text-sm text-ink">
                  <input
                    type="radio"
                    name="la-valuekind"
                    checked={values.valueKind === k}
                    onChange={() => set({ valueKind: k })}
                  />{' '}
                  {k === 'Numeric' ? 'A number' : 'Text'}
                </label>
              ))}
            </div>
          </fieldset>
        </div>
        {numeric ? (
          <>
            <CatalogueField id="la-unit" label="Unit" error={errors.fields.unit}>
              {(p) => (
                <input
                  {...p}
                  type="text"
                  maxLength={30}
                  value={values.unit}
                  onChange={(e) => set({ unit: e.target.value })}
                />
              )}
            </CatalogueField>
            <div className="grid gap-3 min-[460px]:grid-cols-2">
              <MeasurementInput
                id="la-low"
                label="Range low"
                unitName={unit}
                unitSymbol={unit}
                decimalPlaces={4}
                value={values.low}
                onChange={(low) => set({ low })}
                error={errors.fields.low}
              />
              <MeasurementInput
                id="la-high"
                label="Range high"
                unitName={unit}
                unitSymbol={unit}
                decimalPlaces={4}
                value={values.high}
                onChange={(high) => set({ high })}
                error={errors.fields.high}
              />
            </div>
          </>
        ) : (
          <CatalogueField
            id="la-normal"
            label="Normal result"
            error={errors.fields.normalText}
            hint="A text result equal to this is flagged Normal; anything else Abnormal."
          >
            {(p) => (
              <input
                {...p}
                type="text"
                maxLength={50}
                value={values.normalText}
                onChange={(e) => set({ normalText: e.target.value })}
              />
            )}
          </CatalogueField>
        )}
      </form>
    </CatalogueDialog>
  );
}
