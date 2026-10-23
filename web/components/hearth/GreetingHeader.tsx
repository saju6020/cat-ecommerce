import * as React from 'react';
import { cn } from '@/lib/utils';

/** The page greeting: a display-font headline and a muted line under it (checkout steps, My orders). */
export function GreetingHeader({
  title,
  children,
  className,
}: {
  title: React.ReactNode;
  children?: React.ReactNode;
  className?: string;
}) {
  return (
    <header className={cn('flex flex-col gap-2', className)}>
      <h1 className="m-0 font-display text-[32px] font-semibold leading-[1.1] text-ink md:text-[40px]">
        {title}
      </h1>
      {children && <p className="m-0 text-[15px] text-ink-muted">{children}</p>}
    </header>
  );
}
