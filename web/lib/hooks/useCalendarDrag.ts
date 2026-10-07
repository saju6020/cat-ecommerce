'use client';

import { useCallback, useEffect, useRef, useState } from 'react';

export interface DragTarget {
  resourceId: string;
  dateIso: string;
  /** Clamped, grid-snapped minutes after local midnight. */
  minutes: number;
}

export interface DragState {
  appointmentId: string;
  target: DragTarget | null;
}

const DRAG_THRESHOLD_PX = 5;

/**
 * `lib/hooks/useCalendarDrag.ts` — HO-APT-002 §11.6, ADR-FE-026: pointer-event drag for the
 * calendar grid (no drag-and-drop library). `begin` is called from a block's `onPointerDown`; after
 * a small movement threshold the drag starts and follows the pointer, resolving the column under it
 * (`[data-calendar-column]` with `data-resource-id` / `data-date`) and snapping the vertical offset
 * to the slot grid through `snap`. Releasing over a column calls `onDrop`; releasing elsewhere, or
 * pressing Escape, cancels. `consumeClick` lets the block ignore the click that ends a drag. Every
 * drag has a keyboard alternative (the Move… dialog) — this hook is only the pointer path.
 */
export function useCalendarDrag({
  snap,
  onDrop,
}: {
  snap: (offsetY: number) => number;
  onDrop: (appointmentId: string, target: DragTarget) => void;
}) {
  const [drag, setDrag] = useState<DragState | null>(null);
  const draggedRef = useRef(false);
  const onDropRef = useRef(onDrop);
  const snapRef = useRef(snap);
  useEffect(() => {
    onDropRef.current = onDrop;
    snapRef.current = snap;
  });

  const begin = useCallback((event: React.PointerEvent, appointmentId: string) => {
    if (event.button !== 0) return;
    const startX = event.clientX;
    const startY = event.clientY;
    let started = false;
    let target: DragTarget | null = null;

    const locate = (x: number, y: number): DragTarget | null => {
      const column = document
        .elementsFromPoint(x, y)
        .find(
          (el): el is HTMLElement => el instanceof HTMLElement && 'calendarColumn' in el.dataset,
        );
      if (!column) return null;
      const rect = column.getBoundingClientRect();
      return {
        resourceId: column.dataset.resourceId ?? '',
        dateIso: column.dataset.date ?? '',
        minutes: snapRef.current(y - rect.top),
      };
    };

    const move = (e: PointerEvent) => {
      if (!started && Math.hypot(e.clientX - startX, e.clientY - startY) < DRAG_THRESHOLD_PX)
        return;
      started = true;
      target = locate(e.clientX, e.clientY);
      setDrag({ appointmentId, target });
    };
    const finish = (commit: boolean) => {
      window.removeEventListener('pointermove', move);
      window.removeEventListener('pointerup', up);
      window.removeEventListener('keydown', key);
      if (started) {
        draggedRef.current = true;
        setTimeout(() => {
          draggedRef.current = false;
        }, 0);
        if (commit && target) onDropRef.current(appointmentId, target);
      }
      setDrag(null);
    };
    const up = () => finish(true);
    const key = (e: KeyboardEvent) => {
      if (e.key === 'Escape') finish(false);
    };
    window.addEventListener('pointermove', move);
    window.addEventListener('pointerup', up);
    window.addEventListener('keydown', key);
  }, []);

  /** True once, right after a drag ended — the click that follows must not open the detail. */
  const consumeClick = useCallback(() => draggedRef.current, []);

  return { drag, begin, consumeClick };
}
