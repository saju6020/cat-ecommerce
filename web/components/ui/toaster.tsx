'use client';

import {
  Toast,
  ToastClose,
  ToastDescription,
  ToastProvider,
  ToastTitle,
  ToastViewport,
} from '@/components/ui/toast';
import { useToast } from '@/components/ui/use-toast';

/**
 * The app's one toast surface (HO-CAT-002 §8.3's root-layout comment). Rendered once from
 * `app/providers.tsx`. Any component calls `toast({ title, description, variant })` from
 * `components/ui/use-toast.ts` — never render a second `<Toaster />` or build a second
 * notification surface.
 */
export function Toaster() {
  const { toasts, dismiss } = useToast();

  return (
    <ToastProvider swipeDirection="right">
      {toasts.map(({ id, title, description, action, variant, open }) => (
        <Toast
          key={id}
          variant={variant}
          open={open}
          onOpenChange={(next) => {
            if (!next) dismiss(id);
          }}
        >
          <div className="flex-1">
            {title && <ToastTitle>{title}</ToastTitle>}
            {description && <ToastDescription>{description}</ToastDescription>}
          </div>
          {action}
          <ToastClose />
        </Toast>
      ))}
      <ToastViewport />
    </ToastProvider>
  );
}
