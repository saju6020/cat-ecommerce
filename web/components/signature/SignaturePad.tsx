'use client';

import { useEffect, useRef } from 'react';
import { Button } from '@/components/ui/button';
import { FormFieldRow } from '@/components/cat/FormFieldRow';
import { FieldError } from '@/components/boarding/shared/FormBits';
import type { SignatureDraft } from '@/lib/validation/boarding';

type Strokes = SignatureDraft['strokes'];

const WIDTH = 480;
const HEIGHT = 160;

function drawAll(canvas: HTMLCanvasElement, strokes: Strokes) {
  const ctx = canvas.getContext('2d');
  if (!ctx) return;
  ctx.clearRect(0, 0, canvas.width, canvas.height);
  ctx.lineWidth = 2.5;
  ctx.lineCap = 'round';
  ctx.lineJoin = 'round';
  ctx.strokeStyle = '#1c1a17';
  for (const stroke of strokes) {
    ctx.beginPath();
    stroke.forEach(([x, y], i) => (i === 0 ? ctx.moveTo(x, y) : ctx.lineTo(x, y)));
    if (stroke.length === 1) ctx.lineTo(stroke[0]![0] + 0.1, stroke[0]![1]);
    ctx.stroke();
  }
}

/**
 * `components/signature/SignaturePad.tsx` — ⭐ HO-BRD-002 §11.8, ADR-FE-103, UI-6. An in-repo
 * pointer-event canvas that records the pen's `[x, y]` integer points as JSON strokes (HO-BRD-001
 * ADR-236) — no image is produced, nothing is uploaded. A canvas alone is not accessible, so the
 * **typed-name alternative is always visible beside it**: the person chooses which one counts, and
 * both need the explicit confirmation. **Clear** wipes the drawing.
 */
export function SignaturePad({
  idPrefix,
  value,
  onChange,
  error,
  statement = 'I confirm this is my signature.',
}: {
  idPrefix: string;
  value: SignatureDraft;
  onChange: (next: SignatureDraft) => void;
  error?: string;
  statement?: string;
}) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const current = useRef<Strokes[number] | null>(null);
  const valueRef = useRef(value);
  valueRef.current = value;

  useEffect(() => {
    if (canvasRef.current) drawAll(canvasRef.current, value.strokes);
  }, [value.strokes]);

  function point(event: React.PointerEvent<HTMLCanvasElement>): [number, number] {
    const rect = event.currentTarget.getBoundingClientRect();
    const x = ((event.clientX - rect.left) / rect.width) * WIDTH;
    const y = ((event.clientY - rect.top) / rect.height) * HEIGHT;
    return [Math.round(Math.min(WIDTH, Math.max(0, x))), Math.round(Math.min(HEIGHT, Math.max(0, y)))];
  }

  function down(event: React.PointerEvent<HTMLCanvasElement>) {
    event.currentTarget.setPointerCapture?.(event.pointerId);
    current.current = [point(event)];
    onChange({ ...valueRef.current, method: 'Drawn', strokes: [...valueRef.current.strokes, current.current] });
  }
  function move(event: React.PointerEvent<HTMLCanvasElement>) {
    if (!current.current) return;
    current.current.push(point(event));
    if (canvasRef.current) drawAll(canvasRef.current, [...valueRef.current.strokes]);
  }
  function up() {
    if (!current.current) return;
    current.current = null;
    onChange({ ...valueRef.current, strokes: [...valueRef.current.strokes] });
  }

  const drawn = value.strokes.length > 0;
  return (
    <fieldset className="flex flex-col gap-3 rounded-[var(--radius-md)] border border-border p-4">
      <legend className="px-1 text-sm font-semibold text-ink">Owner’s signature</legend>
      <div className="grid gap-4 md:grid-cols-2">
        <div className="flex flex-col gap-2">
          <label className="flex items-center gap-2 text-sm font-semibold text-ink">
            <input
              type="radio"
              name={`${idPrefix}-method`}
              checked={value.method === 'Drawn'}
              onChange={() => onChange({ ...value, method: 'Drawn' })}
            />
            Sign here
          </label>
          <canvas
            ref={canvasRef}
            width={WIDTH}
            height={HEIGHT}
            role="img"
            aria-label={drawn ? 'Signature drawn' : 'Signature area, empty'}
            data-testid={`${idPrefix}-canvas`}
            className="w-full touch-none rounded-[var(--radius-sm)] border border-dashed border-border-strong bg-surface"
            onPointerDown={down}
            onPointerMove={move}
            onPointerUp={up}
            onPointerCancel={up}
          />
          <div>
            <Button variant="secondary" size="sm" onClick={() => onChange({ ...value, strokes: [] })} disabled={!drawn}>
              Clear
            </Button>
          </div>
        </div>
        <div className="flex flex-col gap-2">
          <label className="flex items-center gap-2 text-sm font-semibold text-ink">
            <input
              type="radio"
              name={`${idPrefix}-method`}
              checked={value.method === 'Typed'}
              onChange={() => onChange({ ...value, method: 'Typed' })}
            />
            Type your name instead
          </label>
          <p className="text-xs text-ink-muted">
            If drawing is difficult, typing the full name and confirming below counts as the signature.
          </p>
        </div>
      </div>
      <FormFieldRow
        id={`${idPrefix}-signer`}
        label="Name of the person signing"
        required
        value={value.signerName}
        onChange={(e) => onChange({ ...value, signerName: e.target.value })}
      />
      <label className="flex items-start gap-2 text-sm text-ink">
        <input
          type="checkbox"
          className="mt-0.5"
          checked={value.confirmed}
          onChange={(e) => onChange({ ...value, confirmed: e.target.checked })}
        />
        {statement}
      </label>
      <FieldError id={`${idPrefix}-error`} message={error} />
    </fieldset>
  );
}
