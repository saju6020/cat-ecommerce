'use client';

import { Suspense } from 'react';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { Plus } from 'lucide-react';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { buttonVariants } from '@/components/ui/button';
import { FormularyTable, FORMULARY_PAGE_SIZE } from '@/components/formulary/FormularyTable';
import { PageHeading } from '@/components/inventory/fields';
import { ApiForbiddenError } from '@/lib/api/client';
import { useFormularyList } from '@/lib/hooks/useFormulary';
import { useStaffRoles } from '@/lib/hooks/useStaffRoles';

/** `/staff/admin/formulary` — US-PHM-04: the list. Anyone with Formulary.View reads it; only an administrator edits. */
function FormularyListContent() {
  const params = useSearchParams();
  const roles = useStaffRoles();
  const query = useFormularyList({
    search: params.get('q') ?? undefined,
    isControlled: params.get('controlled') ? params.get('controlled') === 'true' : undefined,
    isActive: params.get('active') ? params.get('active') === 'true' : undefined,
    pageNumber: Math.max(1, Number(params.get('page')) || 1),
    pageSize: FORMULARY_PAGE_SIZE,
  });
  if (query.error instanceof ApiForbiddenError) return <AccessRefusedState error={query.error} />;
  return (
    <div className="flex flex-col gap-4">
      <PageHeading
        title="Formulary"
        description="The medicines vets can prescribe. Strengths and ranges carry their units."
        actions={
          roles.has('Administrator') ? (
            <Link
              href="/staff/admin/formulary/new"
              className={buttonVariants({ variant: 'primary', size: 'md' })}
            >
              <Plus size={14} aria-hidden="true" />
              New medicine
            </Link>
          ) : undefined
        }
      />
      <FormularyTable
        rows={query.data?.items}
        totalCount={query.data?.totalCount ?? 0}
        isLoading={query.isLoading}
        isError={query.isError}
        onRetry={() => void query.refetch()}
      />
    </div>
  );
}

export default function FormularyPage() {
  return (
    <Suspense fallback={null}>
      <FormularyListContent />
    </Suspense>
  );
}
