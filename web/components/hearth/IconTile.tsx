import * as React from 'react';
import { cn } from '@/lib/utils';

export interface IconTileProps extends React.HTMLAttributes<HTMLSpanElement> {
  /** `warm` = warm-soft with a warm-ink icon; `teal` = accent-tint with a teal icon. */
  tone?: 'warm' | 'teal';
  /** `disc` is round (40-64px); `tile` is the 14px-radius square. */
  shape?: 'disc' | 'tile';
  size?: 'sm' | 'md' | 'lg';
}

const SIZES = { sm: 'size-10', md: 'size-11', lg: 'size-16' } as const;

/** A decorative icon container (stroke icons only: no emoji, no paw prints). */
export function IconTile({
  tone = 'warm',
  shape = 'disc',
  size = 'md',
  className,
  ...props
}: IconTileProps) {
  return (
    <span
      aria-hidden="true"
      className={cn(
        'grid flex-none place-items-center',
        SIZES[size],
        shape === 'disc' ? 'rounded-pill' : 'rounded-[14px]',
        tone === 'warm' ? 'bg-warm-soft text-warm-ink' : 'bg-accent-tint text-accent',
        className,
      )}
      {...props}
    />
  );
}
