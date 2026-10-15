'use client';

import { Suspense, use, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { Plus } from 'lucide-react';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { Button } from '@/components/ui/button';
import { CatPrescriptionsTable } from '@/components/prescriptions/CatPrescriptionsTable';
import { VisitPickerDialog } from '@/components/prescriptions/VisitPickerDialog';
import { ApiForbiddenError } from '@/lib/api/client';
import type { PrescriptionScope } from '@/lib/api/prescriptions';
import { useCat } from '@/lib/hooks/useCat';
import { useCatPrescriptions } from '@/lib/hooks/usePrescriptions';
import { useStaffRoles } from '@/lib/hooks/useStaffRoles';

const PAGE_SIZE = 20;

/** `/staff/cats/[catId]/prescriptions` — US-PHM-01, 07: the cat's prescriptions, under the cat's clinical header. */
function PrescriptionsTab({ catId }: { catId: string }) {
  const router = useRouter();
  const params = useSearchParams();
  const roles = useStaffRoles();
  const cat = useCat(catId);
  const [picking, setPicking] = useState(false);
  const scope: PrescriptionScope = params.get('scope') === 'all' ? 'all' : 'active';
  const page = Math.max(1, Number(params.get('page')) || 1);
  const query = useCatPrescriptions(catId, scope, page, PAGE_SIZE);

  function go(next: { scope?: PrescriptionScope; page?: number }) {
    const search = new URLSearchParams(params.toString());
    if (next.scope) search.set('scope', next.scope);
    if (next.page) search.set('page', String(next.page));
    if (next.scope) search.delete('page');
    router.replace(`/staff/cats/${catId}/prescriptions?${search.toString()}`, { scroll: false });
  }

  if (query.error instanceof ApiForbiddenError) return <PermissionDeniedState />;
  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 className="text-lg font-bold text-ink">Prescriptions</h2>
        {roles.has('Vet') && (
          <Button onClick={() => setPicking(true)}>
            <Plus size={14} aria-hidden="true" />
            New prescription
          </Button>
        )}
      </div>
      <CatPrescriptionsTable
        catId={catId}
        scope={scope}
        onScopeChange={(next) => go({ scope: next })}
        rows={query.data?.items}
        isLoading={query.isLoading}
        isError={query.isError}
        onRetry={() => void query.refetch()}
        pager={{
          pageNumber: page,
          pageSize: PAGE_SIZE,
          totalCount: query.data?.totalCount ?? 0,
          onPageChange: (next) => go({ page: next }),
        }}
      />
      <VisitPickerDialog
        open={picking}
        onOpenChange={setPicking}
        catId={catId}
        catName={cat.data?.name ?? 'this cat'}
        purpose="prescription"
        onChoose={(visitId) =>
          router.push(
            `/staff/cats/${catId}/prescriptions/new?visitId=${encodeURIComponent(visitId)}`,
          )
        }
      />
    </div>
  );
}

export default function CatPrescriptionsPage({ params }: { params: Promise<{ catId: string }> }) {
  const { catId } = use(params);
  return (
    <Suspense fallback={null}>
      <PrescriptionsTab catId={catId} />
    </Suspense>
  );
}
