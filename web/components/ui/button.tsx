import * as React from 'react';
import { cva, type VariantProps } from 'class-variance-authority';
import { cn } from '@/lib/utils';

/**
 * shadcn/ui-style Button — copied in and owned (HO-CAT-002 §3.1, §9). Consumed by the staff
 * shell's nav-collapse control; every area is expected to reuse this rather than hand-roll a
 * second button.
 */
const buttonVariants = cva(
  'inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-[var(--radius-sm)] ' +
    'text-sm font-semibold transition-colors disabled:pointer-events-none disabled:opacity-50 ' +
    'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent',
  {
    variants: {
      variant: {
        primary: 'bg-accent text-white hover:bg-accent-ink',
        secondary: 'border border-border-strong bg-surface text-ink hover:bg-surface-sunken',
        ghost: 'text-ink hover:bg-surface-sunken',
        destructive: 'bg-danger-ink text-white hover:opacity-90',
      },
      size: {
        sm: 'h-8 px-3',
        md: 'h-10 px-4',
        icon: 'h-8 w-8 p-0',
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
