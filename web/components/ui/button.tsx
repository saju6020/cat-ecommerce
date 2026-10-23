import * as React from 'react';
import { cva, type VariantProps } from 'class-variance-authority';
import { cn } from '@/lib/utils';

/**
 * shadcn/ui-style Button — copied in and owned (HO-CAT-002 §3.1, §9). Consumed by the staff
 * shell's nav-collapse control; every area is expected to reuse this rather than hand-roll a
 * second button.
 */
const buttonVariants = cva(
  'inline-flex items-center justify-center gap-2 whitespace-nowrap ' +
    'font-semibold transition-colors disabled:pointer-events-none disabled:opacity-50 ' +
    'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent',
  {
    variants: {
      variant: {
        primary: 'bg-accent text-white hover:bg-accent-ink',
        secondary: 'border border-border-strong bg-surface text-ink hover:bg-surface-sunken',
        ghost: 'text-ink hover:bg-surface-sunken',
        destructive: 'bg-danger-ink text-white hover:opacity-90',
        // Hearth customer pills (ADR-FE-173) - only meaningful inside [data-theme="customer"].
        pill: 'bg-accent text-white hover:bg-accent-ink',
        pillSecondary:
          'border border-border-strong bg-surface text-accent-ink hover:bg-accent-tint',
      },
      size: {
        sm: 'h-8 rounded-[var(--radius-sm)] px-3 text-sm',
        md: 'h-10 rounded-[var(--radius-sm)] px-4 text-sm',
        icon: 'h-8 w-8 rounded-[var(--radius-sm)] p-0 text-sm',
        // Customer pill sizes: 44px minimum; `pillLg` is the 54px main action.
        pillMd: 'min-h-11 rounded-pill px-[22px] text-sm',
        pillLg: 'min-h-[54px] rounded-pill px-8 text-base',
      },
    },
    defaultVariants: {
      variant: 'primary',
      size: 'md',
    },
  },
);

// Exported (added by HO-CAT-002) for the one case a real `<button>` is wrong: a navigation
// action that must be an `<a>`/`next/link` `<Link>` for semantics and keyboard/middle-click
// behaviour, but should still look like a button — `<Link className={buttonVariants({variant})}>`
// rather than nesting a `<button>` inside an `<a>`, which `asChild` would otherwise require.
export { buttonVariants };

export interface ButtonProps
  extends React.ButtonHTMLAttributes<HTMLButtonElement>, VariantProps<typeof buttonVariants> {}

export const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  ({ className, variant, size, type = 'button', ...props }, ref) => {
    return (
      <button
        ref={ref}
        type={type}
        className={cn(buttonVariants({ variant, size }), className)}
        {...props}
      />
    );
  },
);
Button.displayName = 'Button';
