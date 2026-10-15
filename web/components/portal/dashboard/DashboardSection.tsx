import Link from 'next/link';
import type { ReactNode } from 'react';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import type { PortalSection } from '@/lib/api/portal';

/**
 * `components/portal/dashboard/DashboardSection.tsx` — HO-POR-002 CODE_REVIEW focus: the ONE
 * tile shell, used by every tile, so the three states can never be told apart wrongly.
 *
 *  - `section` undefined  -> the backend did not deliver this section yet: "Not available yet."
 *    No retry (nothing to retry), never "empty", never an error (HO-POR-001 §1 Amended 2026-10-09);
 *  - `Unavailable`        -> "couldn't load this" with Try again; never an empty list or a zero
 *    (ADR-FE-012, ADR-FE-120);
 *  - `Ready`              -> `children(data)`.
 *
 * A tile whose section key does not exist for the whole product (stays, subscriptions: owner skipped
 * BRD and SUB) is simply not rendered by the page — it does not use this component.
 */
export interface DashboardSectionProps<T> {
  id: string;
  title: string;
  section: PortalSection<T> | undefined;
  /** Header link to the page the owning area built. Omit when that area has no single page. */
  link?: { href: string; label: string };
  onRetry: () => void;
  retrying?: boolean;
  /** What "couldn't load" means for this tile, e.g. "your balances". */
  unavailableSubject: string;
  className?: string;
  children: (data: T) => ReactNode;
}

export function DashboardSection<T>({
  id,
  title,
  section,
  link,
  onRetry,
  retrying = false,
  unavailableSubject,
  className,
  children,
}: DashboardSectionProps<T>) {
  const headingId = `tile-${id}`;
  return (
    <section
      aria-labelledby={headingId}
      data-testid={`tile-${id}`}
      className={cn(
        'min-w-0 rounded-[var(--radius-md)] border border-border bg-surface',
        className,
      )}
    >
      <div className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-1 border-b border-border px-[18px] pb-3 pt-3.5">
        <h2 id={headingId} className="font-display text-lg font-semibold text-ink">
          {title}
        </h2>
        {link && (
          <Link href={link.href} className="text-[13px] font-semibold text-accent-ink">
            {link.label} <span aria-hidden="true">&rarr;</span>
          </Link>
        )}
      </div>
      <div className="px-[18px] pb-2.5 pt-0.5">
        {section === undefined ? (
          <p className="py-3.5 text-[13.5px] text-ink-muted">
            Not available yet. This will appear here once it is ready.
          </p>
        ) : section.state === 'Unavailable' || section.data === null ? (
          <div
            role="status"
            data-testid={`tile-${id}-unavailable`}
            className="my-3 flex flex-col items-start gap-2 rounded-[var(--radius-sm)] border border-dashed border-border-strong bg-surface-sunken px-3.5 py-3 text-[13.5px] leading-relaxed text-ink"
          >
            <p>
              <span className="font-semibold">We couldn’t load {unavailableSubject} just now.</span>{' '}
              This does not mean there is nothing here.
            </p>
            <Button variant="secondary" size="sm" onClick={onRetry} disabled={retrying}>
              {retrying ? 'Trying again…' : 'Try again'}
            </Button>
          </div>
        ) : (
          children(section.data)
        )}
      </div>
    </section>
  );
}

/** A tile's plain "nothing here" line — only ever rendered for a `Ready` section. */
export function TileEmpty({ children }: { children: ReactNode }) {
  return <p className="py-3.5 text-[13.5px] leading-relaxed text-ink-muted">{children}</p>;
}

/** One row inside a tile: text on the left, status and action on the right. */
export function TileRow({ main, side }: { main: ReactNode; side?: ReactNode }) {
  return (
    <li className="flex flex-wrap items-start gap-x-3.5 gap-y-2 border-b border-border py-3 last:border-b-0">
      <div className="min-w-0 flex-[1_1_220px]">{main}</div>
      {side && <div className="ml-auto flex flex-col items-end gap-1.5 text-right">{side}</div>}
    </li>
  );
}
