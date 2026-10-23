'use client';

import Link from 'next/link';
import type { ReactNode } from 'react';
import { ArrowLeft } from 'lucide-react';
import { mainActionClass } from '@/components/hearth/buttons';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';

/**
 * What a checkout step passes down so it can sit beside the quote's order summary
 * (docs/design/orders/CheckoutFulfilment.dc.html): the step's own choices fill the left column and the
 * summary card, with the step's Continue block inside it, fills the right. The callback receives that
 * Continue block so the summary can place it.
 */
export type RenderSummary = (footer: ReactNode) => ReactNode;

/**
 * Left column + summary column. Without `renderSummary` (a step shown on its own) the Continue block
 * simply follows the choices.
 */
export function StepLayout({
  children,
  footer,
  renderSummary,
}: {
  children: ReactNode;
  footer: ReactNode;
  renderSummary?: RenderSummary;
}) {
  if (!renderSummary) {
    return (
      <div className="flex flex-col gap-[18px]">
        {children}
        {footer}
      </div>
    );
  }
  return (
    <div className="grid items-start gap-7 lg:grid-cols-[minmax(0,1.6fr)_minmax(0,1fr)]">
      <div className="flex min-w-0 flex-col gap-[18px]">{children}</div>
      {renderSummary(footer)}
    </div>
  );
}

/**
 * The Continue block of a step: the 54px teal pill (a muted pill with the reason underneath while the
 * step is incomplete) and an optional back link. Disabled always says why.
 */
export function StepFooter({
  id,
  label = 'Continue',
  onContinue,
  disabledReason,
  back,
  reassure,
}: {
  /** Prefix for the reason's element id (`<id>-why`). */
  id: string;
  label?: string;
  onContinue: () => void;
  disabledReason: string | null;
  back?: { label: string; href?: string; onClick?: () => void };
  reassure?: ReactNode;
}) {
  const whyId = `${id}-why`;
  return (
    <div className="flex flex-col gap-3">
      <Button
        variant="pill"
        size="pillLg"
        disabled={disabledReason !== null}
        aria-describedby={disabledReason ? whyId : undefined}
        onClick={onContinue}
        className={mainActionClass}
      >
        {label}
      </Button>
      {disabledReason && (
        <p id={whyId} className="m-0 text-center text-sm text-ink-muted">
          {disabledReason}
        </p>
      )}
      {back &&
        (back.href ? (
          <Link
            href={back.href}
            className={cn(
              'inline-flex min-h-11 items-center gap-1.5 self-center text-[13px] font-semibold text-accent-ink hover:text-accent',
            )}
          >
            <ArrowLeft size={14} aria-hidden="true" />
            {back.label}
          </Link>
        ) : (
          <button
            type="button"
            onClick={back.onClick}
            className="inline-flex min-h-11 items-center gap-1.5 self-center text-[13px] font-semibold text-accent-ink hover:text-accent"
          >
            <ArrowLeft size={14} aria-hidden="true" />
            {back.label}
          </button>
        ))}
      {reassure && (
        <p className="m-0 text-center text-xs leading-relaxed text-ink-muted">{reassure}</p>
      )}
    </div>
  );
}
