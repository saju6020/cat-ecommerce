'use client';

import { use, useState } from 'react';
import { useRouter } from 'next/navigation';
import { Plus } from 'lucide-react';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { Button } from '@/components/ui/button';
import { CatDietAuthorisationsTable } from '@/components/diet-authorisations/CatDietAuthorisationsTable';
import { VisitPickerDialog } from '@/components/prescriptions/VisitPickerDialog';
import { toast } from '@/components/ui/use-toast';
import { ApiForbiddenError } from '@/lib/api/client';
import { useCat } from '@/lib/hooks/useCat';
import {
  useCatDietAuthorisations,
  useExtendDiet,
  useRevokeDiet,
} from '@/lib/hooks/useDietAuthorisations';
import { useStaffRoles } from '@/lib/hooks/useStaffRoles';

/** `/staff/cats/[catId]/diet-authorisations` — US-PHM-08: the cat's authorisations, extend and revoke (vets). */
export default function CatDietPage({ params }: { params: Promise<{ catId: string }> }) {
  const { catId } = use(params);
  const router = useRouter();
  const roles = useStaffRoles();
  const cat = useCat(catId);
  const query = useCatDietAuthorisations(catId, 'all');
  const extend = useExtendDiet(catId);
  const revoke = useRevokeDiet(catId);
  const [picking, setPicking] = useState(false);
  const catName = cat.data?.name ?? 'this cat';
  if (query.error instanceof ApiForbiddenError) return <PermissionDeniedState />;
  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 className="text-lg font-bold text-ink">Diet authorisations</h2>
        {roles.has('Vet') && (
          <Button onClick={() => setPicking(true)}>
            <Plus size={14} aria-hidden="true" />
            Authorise a diet
          </Button>
        )}
      </div>
      <CatDietAuthorisationsTable
        catName={catName}
        rows={query.data}
        canIssue={roles.has('Vet')}
        isLoading={query.isLoading}
        isError={query.isError}
        onRetry={() => void query.refetch()}
        onExtend={async (id, validTo) => {
          await extend.mutateAsync({ id, validTo });
          toast({ title: 'Authorisation extended' });
        }}
        onRevoke={async (id, reason) => {
          await revoke.mutateAsync({ id, reason });
          toast({ title: 'Authorisation revoked' });
        }}
      />
      <VisitPickerDialog
        open={picking}
        onOpenChange={setPicking}
        catId={catId}
        catName={catName}
        purpose="diet authorisation"
        onChoose={(visitId) =>
          router.push(
            `/staff/cats/${catId}/diet-authorisations/new?visitId=${encodeURIComponent(visitId)}`,
          )
        }
      />
    </div>
  );
}
