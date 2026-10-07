'use client';

import { use } from 'react';
import { useBranchRooms, useCreateRoom, useUpdateRoom } from '@/lib/hooks/useAdminBranches';
import { RoomForm } from '@/components/branches/RoomForm';
import { RoomList } from '@/components/branches/RoomList';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { ApiForbiddenError } from '@/lib/api/client';

/** `/staff/admin/branches/[branchId]/rooms` — Rooms tab. */
export default function BranchRoomsPage({ params }: { params: Promise<{ branchId: string }> }) {
  const { branchId } = use(params);
  const rooms = useBranchRooms(branchId);
  const createMutation = useCreateRoom(branchId);
  const updateMutation = useUpdateRoom(branchId);

  if (rooms.error instanceof ApiForbiddenError) return <AccessRefusedState error={rooms.error} />;

  return (
    <div className="flex flex-col gap-6">
      <h2 className="font-display text-lg font-semibold text-ink">Rooms</h2>
      <RoomForm
        isPending={createMutation.isPending}
        onSave={(values) => createMutation.mutateAsync(values)}
      />
      <RoomList
        rooms={rooms.data}
        isLoading={rooms.isLoading}
        isError={rooms.isError}
        onRetry={() => rooms.refetch()}
        isSaving={updateMutation.isPending}
        onSave={(room, values) => updateMutation.mutateAsync({ roomId: room.id, request: values })}
      />
    </div>
  );
}
