'use client';

import {
  forwardRef,
  useCallback,
  useEffect,
  useImperativeHandle,
  useRef,
  useState,
  type PointerEvent as ReactPointerEvent,
} from 'react';
import { Eraser, Undo2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import type { SignatureStrokes } from '@/lib/api/consents';

/**
 * ⭐ `SignaturePad` — HO-EMR-002 §11.6, ADR-FE-054 (US-CLN-21). An IN-REPO canvas: no signature library.
 * Pointer events (mouse, pen, finger; `touch-action: none` so drawing never scrolls the page) capture
 * STROKES as normalised `[x, y]` points in `[0, 1]` rounded to 4 decimals — the data the server stores
 * (ADR-132: strokes, never an image). The strokes live in a REF and are read only when the form
 * submits (`getStrokes()`); nothing is serialised or stored before that. *Undo stroke* removes the last
 * stroke, *Clear* all of them. A stroke needs at least two points; the pad refuses new points past
 * the server's limits (200 strokes / 5,000 points). The canvas is not keyboard-drawable, so the form
 * offers *Upload a signed form* as the non-drawing alternative (named in `aria-describedby`).
 */
export const MAX_STROKES = 200;
export const MAX_POINTS = 5_000;
export const MIN_TOTAL_POINTS = 10;
const HEIGHT = 176;

export interface SignaturePadHandle {
  getStrokes: () => SignatureStrokes;
  clear: () => void;
}

export interface SignatureSummary {
  strokes: number;
  points: number;
  /** True once there are enough points for the server to accept it (≥ 10). */
  isEnough: boolean;
}

const round4 = (n: number) => Math.round(Math.min(1, Math.max(0, n)) * 10_000) / 10_000;

export const SignaturePad = forwardRef<
  SignaturePadHandle,
  { describedBy?: string; onChange?: (summary: SignatureSummary) => void }
>(function SignaturePad({ describedBy, onChange }, ref) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const strokesRef = useRef<SignatureStrokes>([]);
  const activeRef = useRef<number[][] | null>(null);
  const [summary, setSummary] = useState<SignatureSummary>({
    strokes: 0,
    points: 0,
    isEnough: false,
  });
  const [limitHit, setLimitHit] = useState(false);

  const totalPoints = () =>
    strokesRef.current.reduce((n, s) => n + s.length, 0) + (activeRef.current?.length ?? 0);

  const publish = useCallback(() => {
    const points = strokesRef.current.reduce((n, s) => n + s.length, 0);
    const next = {
      strokes: strokesRef.current.length,
      points,
      isEnough: points >= MIN_TOTAL_POINTS,
    };
    setSummary(next);
    onChange?.(next);
  }, [onChange]);

  const redraw = useCallback(() => {
    const canvas = canvasRef.current;
    const context = canvas?.getContext?.('2d');
    if (!canvas || !context) return;
    const { width, height } = canvas.getBoundingClientRect();
    const ratio = window.devicePixelRatio || 1;
    canvas.width = Math.max(1, Math.floor(width * ratio));
    canvas.height = Math.max(1, Math.floor(height * ratio));
    context.setTransform(ratio, 0, 0, ratio, 0, 0);
    context.clearRect(0, 0, width, height);
    context.lineWidth = 2;
    context.lineCap = 'round';
    context.lineJoin = 'round';
    context.strokeStyle = '#1c1a17';
    for (const stroke of [
      ...strokesRef.current,
      ...(activeRef.current ? [activeRef.current] : []),
    ]) {
      context.beginPath();
      stroke.forEach(([x, y], index) => {
        const px = x! * width;
        const py = y! * height;
        if (index === 0) context.moveTo(px, py);
        else context.lineTo(px, py);
      });
      context.stroke();
    }
  }, []);

  useEffect(() => {
    redraw();
    const canvas = canvasRef.current;
    if (!canvas || typeof ResizeObserver === 'undefined') return;
    const observer = new ResizeObserver(() => redraw());
    observer.observe(canvas);
    return () => observer.disconnect();
  }, [redraw]);

  function point(event: ReactPointerEvent<HTMLCanvasElement>): number[] {
    const rect = event.currentTarget.getBoundingClientRect();
    const width = rect.width || 1;
    const height = rect.height || 1;
    return [
      round4((event.clientX - rect.left) / width),
      round4((event.clientY - rect.top) / height),
    ];
  }

  function onPointerDown(event: ReactPointerEvent<HTMLCanvasElement>) {
    if (strokesRef.current.length >= MAX_STROKES || totalPoints() >= MAX_POINTS) {
      setLimitHit(true);
      return;
    }
    event.currentTarget.setPointerCapture?.(event.pointerId);
    activeRef.current = [point(event)];
    setLimitHit(false);
  }

  function onPointerMove(event: ReactPointerEvent<HTMLCanvasElement>) {
    const active = activeRef.current;
    if (!active) return;
    if (totalPoints() >= MAX_POINTS) {
      setLimitHit(true);
      return;
    }
    active.push(point(event));
    redraw();
  }

  function endStroke() {
    const active = activeRef.current;
    activeRef.current = null;
    if (active && active.length >= 2) {
      strokesRef.current.push(active);
      publish();
    }
    redraw();
  }

  useImperativeHandle(
    ref,
    () => ({
      getStrokes: () => strokesRef.current.map((stroke) => stroke.map(([x, y]) => [x!, y!])),
      clear: () => {
        strokesRef.current = [];
        activeRef.current = null;
        setLimitHit(false);
        publish();
        redraw();
      },
    }),
    [publish, redraw],
  );

  return (
    <div className="flex flex-col gap-2">
      <div className="relative w-full overflow-hidden rounded-[var(--radius-sm)] border border-border-strong bg-surface">
        <canvas
          ref={canvasRef}
          role="img"
          aria-label="Signature area. Draw your signature here with a finger, pen or mouse."
          aria-describedby={describedBy}
          onPointerDown={onPointerDown}
          onPointerMove={onPointerMove}
          onPointerUp={endStroke}
          onPointerCancel={endStroke}
          onPointerLeave={endStroke}
          style={{ touchAction: 'none', height: HEIGHT }}
          className="block w-full cursor-crosshair"
        />
        <div
          aria-hidden="true"
          className="pointer-events-none absolute inset-x-4 bottom-8 border-t border-ink-faint"
        />
        <p
          aria-hidden="true"
          className="pointer-events-none absolute bottom-2 left-4 text-xs text-ink-muted"
        >
          Sign here
        </p>
      </div>
      <div className="flex flex-wrap items-center gap-2">
        <Button
          variant="secondary"
          size="sm"
          disabled={summary.strokes === 0}
          onClick={() => {
            strokesRef.current.pop();
            publish();
            redraw();
          }}
        >
          <Undo2 size={14} aria-hidden="true" /> Undo stroke
        </Button>
        <Button
          variant="secondary"
          size="sm"
          disabled={summary.strokes === 0}
          onClick={() => {
            strokesRef.current = [];
            activeRef.current = null;
            setLimitHit(false);
            publish();
            redraw();
          }}
        >
          <Eraser size={14} aria-hidden="true" /> Clear
        </Button>
        <p role="status" className="text-sm text-ink-muted">
          {summary.strokes === 0
            ? 'Nothing signed yet.'
            : summary.isEnough
              ? 'Signature captured.'
              : 'The signature is too short — keep going or clear it.'}
        </p>
      </div>
      {limitHit && (
        <p role="alert" className="text-xs font-semibold text-warning-ink">
          That is as long as a signature can be. Undo a stroke or clear it to continue.
        </p>
      )}
    </div>
  );
});
