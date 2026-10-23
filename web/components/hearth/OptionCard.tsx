import * as React from 'react';
import { cn } from '@/lib/utils';
import { IconTile } from './IconTile';

export interface OptionCardProps {
  /**
   * Native input type. The input is the real, keyboard-operable control: it is transparent and covers the
   * whole card, so a click anywhere on the card lands on it (and a test or a screen reader finds it).
   */
  type?: 'radio' | 'checkbox';
  name?: string;
  checked: boolean;
  disabled?: boolean;
  onChange: () => void;
  /** A decorative icon (stroke SVG) shown in a tile. */
  icon?: React.ReactNode;
  title: React.ReactNode;
  /** Right-aligned beside the title, e.g. a delivery charge. */
  trailing?: React.ReactNode;
  children?: React.ReactNode;
  /** Why the option is disabled (text, with the warning icon). Referenced by `aria-describedby`. */
  why?: React.ReactNode;
  whyId?: string;
  className?: string;
}

/**
 * The Hearth selectable choice card (docs/design/orders/CheckoutFulfilment.dc.html `.opt`): a
 * rounded card with an icon tile, a title and description, and a round radio mark. The wrapping
 * `<label>` makes the whole card the target; a disabled option is sunken and states its reason in text.
 */
export function OptionCard({
  type = 'radio',
  name,
  checked,
  disabled,
  onChange,
  icon,
  title,
  trailing,
  children,
  why,
  whyId,
  className,
}: OptionCardProps) {
  return (
    <label
      className={cn(
        'relative flex items-start gap-4 rounded-card border bg-surface p-5 sm:px-6',
        'has-[:focus-visible]:outline-2 has-[:focus-visible]:outline-offset-2 has-[:focus-visible]:outline-accent',
        disabled
          ? 'cursor-not-allowed border-border bg-surface-sunken'
          : checked
            ? 'cursor-pointer border-accent ring-1 ring-accent'
            : 'cursor-pointer border-border hover:border-border-strong',
        className,
      )}
    >
      <input
        type={type}
        name={name}
        className="peer absolute inset-0 size-full cursor-pointer opacity-0 disabled:cursor-not-allowed"
        checked={checked}
        disabled={disabled}
        aria-describedby={why ? whyId : undefined}
        onChange={onChange}
      />
      {icon && (
        <IconTile
          shape="tile"
          tone="warm"
          className={cn('size-[52px] rounded-2xl', disabled && 'bg-surface text-ink-muted')}
        >
          {icon}
        </IconTile>
      )}
      <span className="flex min-w-0 flex-1 flex-col gap-1">
        <span className="flex items-baseline justify-between gap-3">
          <strong
            className={cn('text-base [overflow-wrap:anywhere]', disabled && 'text-ink-muted')}
          >
            {title}
          </strong>
          {trailing && <span className="whitespace-nowrap text-base font-bold">{trailing}</span>}
        </span>
        {children && (
          <span className="text-[13px] leading-relaxed text-ink-muted [overflow-wrap:anywhere]">
            {children}
          </span>
        )}
        {why && (
          <span
            id={whyId}
            className="flex items-start gap-1.5 text-[13px] font-semibold text-warning-ink"
          >
            {why}
          </span>
        )}
      </span>
      <span
        aria-hidden="true"
        className={cn(
          'mt-0.5 size-6 flex-none border-2 border-border-strong bg-surface',
          type === 'radio' ? 'rounded-pill' : 'rounded-md',
          checked && 'border-accent bg-accent shadow-[inset_0_0_0_4px_var(--surface)]',
        )}
      />
    </label>
  );
}
