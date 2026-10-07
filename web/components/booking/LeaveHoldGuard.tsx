'use client';

import { useEffect, useRef, useState } from 'react';
import { useRouter } from 'next/navigation';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';

/**
 * `components/booking/LeaveHoldGuard.tsx` — HO-APT-002 UI-3, U-X2, OQ-3. While a slot is held
 * (`when`), an in-app link click is intercepted and confirmed with its consequence — leaving
 * releases the held time — then replayed with `router.push` (the same capture-phase technique as
 * `UnsavedChangesGuard`, ADR-FE-013; a native `beforeunload` covers closing the tab). Releasing
 * itself is the container's job (`onLeave`), on confirmation, on unmount and on `pagehide`; the
 * server's expiry is the guarantee.
 */
export function LeaveHoldGuard({
  when,
  consequence,
  onLeave,
}: {
  when: boolean;
  consequence: string;
  onLeave: () => void;
}) {
  const router = useRouter();
  const whenRef = useRef(when);
  const [pendingHref, setPendingHref] = useState<string | null>(null);

  useEffect(() => {
    whenRef.current = when;
  }, [when]);

  useEffect(() => {
    function onClickCapture(event: MouseEvent) {
      if (!whenRef.current || event.defaultPrevented || event.button !== 0) return;
      if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
      const anchor = (event.target as HTMLElement)?.closest?.(
        'a[href]',
      ) as HTMLAnchorElement | null;
      if (!anchor || (anchor.target && anchor.target !== '_self')) return;
      if (anchor.origin !== window.location.origin) return;
      if (anchor.getAttribute('download') !== null) return;
      event.preventDefault();
      event.stopImmediatePropagation();
      setPendingHref(anchor.getAttribute('href'));
    }
    function onBeforeUnload(event: BeforeUnloadEvent) {
      if (!whenRef.current) return;
      event.preventDefault();
      event.returnValue = '';
    }
    document.addEventListener('click', onClickCapture, true);
    window.addEventListener('beforeunload', onBeforeUnload);
    return () => {
      document.removeEventListener('click', onClickCapture, true);
      window.removeEventListener('beforeunload', onBeforeUnload);
    };
  }, []);

  return (
    <ConfirmDialog
      open={pendingHref !== null}
      onOpenChange={(open) => {
        if (!open) setPendingHref(null);
      }}
      title="Leave and release this time?"
      consequence={consequence}
      confirmLabel="Leave"
      cancelLabel="Stay"
      variant="destructive"
      onConfirm={() => {
        const href = pendingHref;
        setPendingHref(null);
        whenRef.current = false;
        onLeave();
        if (href) router.push(href);
      }}
    />
  );
}
