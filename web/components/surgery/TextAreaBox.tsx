import { errorClass, hintClass, inputClass, labelClass } from './styles';

/**
 * A labelled text area with a visible character counter — the pattern every free-text surgery field
 * uses (NFR-USA-4: the limit is shown before it is hit). Text is plain; it is never rendered as HTML.
 */
export function TextAreaBox({
  id,
  label,
  value,
  onChange,
  max,
  min,
  rows = 3,
  error,
  hint,
  disabled,
}: {
  id: string;
  label: string;
  value: string;
  onChange: (value: string) => void;
  max: number;
  min?: number;
  rows?: number;
  error?: string;
  hint?: string;
  disabled?: boolean;
}) {
  const describedBy = [error ? `${id}-error` : null, `${id}-count`].filter(Boolean).join(' ');
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className={labelClass}>
        {label}
      </label>
      <textarea
        id={id}
        rows={rows}
        maxLength={max}
        disabled={disabled}
        className={inputClass}
        value={value}
        aria-invalid={error ? true : undefined}
        aria-describedby={describedBy}
        onChange={(event) => onChange(event.target.value)}
      />
      {hint && <p className={hintClass}>{hint}</p>}
      {error && (
        <p id={`${id}-error`} role="alert" className={errorClass}>
          {error}
        </p>
      )}
      <p id={`${id}-count`} className={hintClass}>
        {value.trim().length}/{max}
        {min ? ` · at least ${min} characters` : ''}
      </p>
    </div>
  );
}
