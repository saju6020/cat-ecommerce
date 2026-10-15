import { cn } from '@/lib/utils';

/**
 * `ClinicalText` — HO-EMR-002 ADR-FE-058. Clinical text is PLAIN TEXT: rendered through React's text
 * escaping with `white-space: pre-wrap`, never parsed as markdown or HTML, so `<script>` and `**bold**`
 * appear literally (U-X2). Notes are untrusted input displayed to other staff and to owners.
 */
export function ClinicalText({
  children,
  className,
  empty = 'None recorded',
}: {
  children: string | null | undefined;
  className?: string;
  /** Written out when there is nothing — "None recorded" is never left blank. */
  empty?: string;
}) {
  const text = children?.trim() ? children : null;
  return (
    <p
      className={cn(
        'whitespace-pre-wrap break-words text-sm text-ink',
        !text && 'text-ink-muted',
        className,
      )}
    >
      {text ?? empty}
    </p>
  );
}
