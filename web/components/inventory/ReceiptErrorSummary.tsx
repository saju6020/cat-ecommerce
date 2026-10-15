import type { DistributedErrors } from './lineErrors';
import { RECEIPT_ID_PREFIX } from './ReceiptLineCard';

/**
 * `components/inventory/ReceiptErrorSummary.tsx` — HO-IMP-002 §11.3, U-C6. After a failed post (or a
 * local check) every problem is listed once, each a link that moves focus to its own field — so a
 * long receipt with four problems is four clicks, not a hunt. The text is the server's plain
 * language; no error code is rendered.
 */
export function ReceiptErrorSummary({
  errors,
  idPrefix = RECEIPT_ID_PREFIX,
  title = 'This receipt can’t be posted yet.',
}: {
  errors: DistributedErrors;
  /** The id prefix of the form whose fields these are (`receipt`, `transfer`, …). */
  idPrefix?: string;
  title?: string;
}) {
  const items: Array<{ target: string; text: string }> = [];
  for (const [field, message] of Object.entries(errors.fields)) items.push({ target: field, text: message });
  for (const [index, fields] of Object.entries(errors.lines)) {
    for (const [field, message] of Object.entries(fields)) {
      items.push({ target: `line-${index}-${field}`, text: `Line ${parseInt(index, 10) + 1}: ${message}` });
    }
  }
  if (items.length === 0 && errors.general.length === 0) return null;
  return (
    <div role="alert" className="rounded-[var(--radius-md)] border border-danger-ink bg-danger-bg p-3 text-sm text-danger-ink">
      <p className="font-semibold">{title}</p>
      <ul className="mt-1 list-disc pl-5">
        {errors.general.map((message) => (
          <li key={message}>{message}</li>
        ))}
        {items.map((item) => (
          <li key={item.target}>
            <a
              href={`#${idPrefix}-${item.target}`}
              className="font-semibold underline"
              onClick={(event) => {
                event.preventDefault();
                const element = document.getElementById(`${idPrefix}-${item.target}`);
                element?.scrollIntoView?.({ block: 'center' });
                element?.focus({ preventScroll: true });
              }}
            >
              {item.text}
            </a>
          </li>
        ))}
      </ul>
    </div>
  );
}
