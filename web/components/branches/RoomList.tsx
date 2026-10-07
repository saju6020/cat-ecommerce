'use client';

import { useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { EmptyState } from '@/components/common/EmptyState';
import { LoadingState } from '@/components/common/LoadingState';
import { ErrorState } from '@/components/common/ErrorState';
import { RoomForm } from './RoomForm';
import type { Room } from '@/lib/api/admin-branches';

/** `components/branches/RoomList.tsx` — HO-ADM-002 §11.2, US-ADM-01. */
export interface RoomListProps {
  rooms: Room[] | undefined;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
  onSave: (
    room: Room,
    values: { name: string; kind: Room['kind']; isActive: boolean },
  ) => Promise<unknown>;
  isSaving: boolean;
}

export function RoomList({ rooms, isLoading, isError, onRetry, onSave, isSaving }: RoomListProps) {
  const [editingId, setEditingId] = useState<string | null>(null);

  if (isLoading) return <LoadingState variant="table" rows={3} />;
  if (isError) return <ErrorState onRetry={onRetry} />;
  if (!rooms || rooms.length === 0) return <EmptyState title="No rooms yet" />;

  return (
    <ul className="flex flex-col gap-2">
      {rooms.map((room) => (
        <li key={room.id} className="rounded-[var(--radius-md)] border border-border p-3">
          {editingId === room.id ? (
            <RoomForm
              room={room}
              isPending={isSaving}
              onSave={(values) => onSave(room, values)}
              onDone={() => setEditingId(null)}
            />
          ) : (
            <div className="flex flex-wrap items-center justify-between gap-2">
              <div>
                <p className="text-sm font-semibold text-ink">{room.name}</p>
                <p className="text-sm text-ink-muted">{room.kind}</p>
              </div>
              <div className="flex items-center gap-2">
                <Badge variant={room.isActive ? 'success' : 'neutral'}>
                  {room.isActive ? 'Active' : 'Inactive'}
                </Badge>
                <Button variant="secondary" size="sm" onClick={() => setEditingId(room.id)}>
                  Edit
                </Button>
              </div>
            </div>
          )}
        </li>
      ))}
    </ul>
  );
}
