'use client';

import * as React from 'react';
import { Button, type ButtonProps } from '@/components/ui/button';
import { OFFLINE_CANT_SAVE_LABEL, useWriteEnabled } from '@/lib/hooks/useConnectivity';

/**
 * `components/common/WriteButton.tsx` — HO-XCT-002 §11.3, ADR-FE-143. The shared submit control for
 * an action that WRITES: while the server is unreachable it is disabled with the visible reason
 * "Offline — can't save" and `aria-describedby` pointing at it, so a control is never disabled with
 * no explanation. The form around it stays mounted, so nothing typed is lost.
 */
export const WriteButton = React.forwardRef<HTMLButtonElement, ButtonProps>(
  ({ disabled, children, className, ...props }, ref) => {
    const writeEnabled = useWriteEnabled();
    const reasonId = React.useId();
    return (
      <span className="inline-flex flex-wrap items-center gap-2">
        <Button
          ref={ref}
          className={className}
          {...props}
          disabled={disabled || !writeEnabled}
          aria-describedby={!writeEnabled ? reasonId : props['aria-describedby']}
        >
          {children}
        </Button>
        {!writeEnabled && (
          <span id={reasonId} className="text-sm text-ink-muted">
            {OFFLINE_CANT_SAVE_LABEL}
          </span>
        )}
      </span>
    );
  },
);
WriteButton.displayName = 'WriteButton';
