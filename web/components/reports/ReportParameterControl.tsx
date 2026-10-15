import type { ReportParameter } from '@/lib/api/reports';

const INPUT =
  'w-full rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink';

/**
 * `components/reports/ReportParameterControl.tsx` — HO-RPT-002 §11.3, ADR-FE-129. ONE control for
 * one catalogue parameter, chosen by its `type`: `Enum` is a `<select>` of `allowedValues`,
 * `Boolean` a checkbox, `Integer` a number input bounded by `min`/`max`, and `Guid` (no area picker
 * exists for a free id) or any type the server adds later is a plain text field — an unknown type
 * never crashes the page (U-C1). An empty `value` means "the report's default", which the control
 * shows as selected; the server fills the default in itself.
 */
export function ReportParameterControl({
  parameter,
  value,
  onChange,
  error,
  idPrefix,
}: {
  parameter: ReportParameter;
  value: string;
  onChange: (value: string) => void;
  error?: string;
  idPrefix: string;
}) {
  const id = `${idPrefix}-${parameter.name}`;
  const errorId = `${id}-error`;
  const shown = value === '' ? parameter.default : value;
  const described = error ? errorId : undefined;
  const hint =
    parameter.type === 'Integer' && (parameter.min != null || parameter.max != null)
      ? `${parameter.min != null ? `From ${parameter.min}` : ''}${parameter.min != null && parameter.max != null ? ' ' : ''}${parameter.max != null ? `up to ${parameter.max}` : ''}.`
      : null;

  let control: React.ReactNode;
  if (parameter.type === 'Boolean') {
    control = (
      <label htmlFor={id} className="flex items-center gap-2 text-sm font-semibold text-ink">
        <input
          id={id}
          type="checkbox"
          checked={shown === 'true'}
          aria-invalid={error ? true : undefined}
          aria-describedby={described}
          onChange={(event) => onChange(event.target.checked ? 'true' : 'false')}
        />
        {parameter.label}
      </label>
    );
  } else {
    let field: React.ReactNode;
    if (parameter.type === 'Enum') {
      field = (
        <select
          id={id}
          value={shown}
          aria-invalid={error ? true : undefined}
          aria-describedby={described}
          onChange={(event) => onChange(event.target.value)}
          className={INPUT}
        >
          {(parameter.allowedValues ?? []).map((allowed) => (
            <option key={allowed} value={allowed}>
              {allowed}
            </option>
          ))}
        </select>
      );
    } else if (parameter.type === 'Integer') {
      field = (
        <input
          id={id}
          type="number"
          inputMode="numeric"
          value={shown}
          min={parameter.min ?? undefined}
          max={parameter.max ?? undefined}
          aria-invalid={error ? true : undefined}
          aria-describedby={described}
          onChange={(event) => onChange(event.target.value)}
          className={INPUT}
        />
      );
    } else {
      field = (
        <input
          id={id}
          type="text"
          value={value}
          placeholder={parameter.default || undefined}
          aria-invalid={error ? true : undefined}
          aria-describedby={described}
          onChange={(event) => onChange(event.target.value)}
          className={INPUT}
        />
      );
    }
    control = (
      <>
        <label htmlFor={id} className="text-sm font-semibold text-ink">
          {parameter.label}
        </label>
        {field}
      </>
    );
  }

  return (
    <div className="flex min-w-0 flex-col gap-1">
      {control}
      {hint && <p className="m-0 text-xs text-ink-muted">{hint}</p>}
      {error && (
        <p id={errorId} className="m-0 text-xs font-semibold text-danger-ink">
          {error}
        </p>
      )}
    </div>
  );
}
