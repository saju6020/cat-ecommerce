import * as React from 'react';
import { cva, type VariantProps } from 'class-variance-authority';
import { cn } from '@/lib/utils';

/**
 * shadcn/ui-style Badge — copied in and owned (HO-CAT-002 §3.1, §9; named as a permitted
 * dependency by HO-ACC-002 §10 alongside Table/Alert). Styled to match the `.badge`/`.b-active`/
 * `.b-draft`/`.b-arch` rules in docs/design/shop/StaffProductList.dc.html.
 *
 * **UI-6 — colour is never the only signal.** This component supplies colour and shape only; the
 * caller is responsible for passing an icon alongside the text (HighRiskBadge,
 * VaccinationStatusBadge and every other status badge do). A `Badge` with text but no icon is
 * still fine for a purely informational count (e.g. NTF's unread badge) — it is a *status* badge
 * (current/draft/overdue/…) that must never be colour-only.
 */
const badgeVariants = cva(
  'inline-flex items-center gap-1.5 whitespace-nowrap rounded-full px-2.5 py-1 text-[11.5px] font-semibold',
  {
    variants: {
      variant: {
        neutral: 'bg-surface-sunken text-ink-muted',
        success: 'bg-accent-soft text-accent-ink',
        warning: 'bg-warning-bg text-warning-ink',
        danger: 'bg-danger-bg text-danger-ink',
      },
    },
    defaultVariants: {
      variant: 'neutral',
    },
  },
);

export interface BadgeProps
  extends React.HTMLAttributes<HTMLSpanElement>, VariantProps<typeof badgeVariants> {
  /** A small (12–14px) icon, decorative — pair with visible text, never render icon-only. */
  icon?: React.ReactNode;
}

export function Badge({ className, variant, icon, children, ...props }: BadgeProps) {
  return (
    <span className={cn(badgeVariants({ variant }), className)} {...props}>
      {icon}
      {children}
    </span>
  );
}
