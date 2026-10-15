'use client';

import { LocationManager } from '@/components/inventory/LocationManager';
import { PageHeading } from '@/components/inventory/fields';
import { QueryBoundary } from '@/components/inventory/QueryBoundary';
import {
  useCreateLocation,
  useLocations,
  useSetLocationActivation,
  useUpdateLocation,
} from '@/lib/hooks/useInventoryLookup';
import { useStaffSession } from '@/lib/hooks/useSession';

/** `/staff/inventory/locations` — the branch's stock locations (administrator manages). */
export default function LocationsPage() {
  const query = useLocations();
  const create = useCreateLocation();
  const rename = useUpdateLocation();
  const activation = useSetLocationActivation();
  const session = useStaffSession();
  return (
    <div className="flex flex-col gap-4">
      <PageHeading title="Stock locations" description="Where stock is held at this branch, and which movements each kind of location takes." />
      <QueryBoundary query={query}>
        {(locations) => (
          <LocationManager
            locations={locations}
            canManage={(session.data?.roleIds ?? []).includes('Administrator')}
            onCreate={(request) => create.mutateAsync(request)}
            onRename={(id, name) => rename.mutateAsync({ id, name })}
            onSetActive={(id, isActive) => activation.mutateAsync({ id, isActive })}
          />
        )}
      </QueryBoundary>
    </div>
  );
}
