'use client';

import * as React from 'react';
import { ShieldAlert } from 'lucide-react';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  useDialogReturnFocus,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';

/**
 * ⭐ `WitnessDialog` — HO-PHM-002 §8.6 / §11.4, ADR-FE-063 (and HO-PHM-001 ADR-146). A controlled drug
 * needs a second member of staff, who signs in HERE with their OWN identifier and password on this same
 * device. The witness must be a vet or a nurse and not the person dispensing (owner decision
 * 2026-10-10) — the server decides; this dialog says it.
 *
 *  - A modal on `components/ui/dialog.tsx`: focus is trapped, Escape cancels, focus returns to the
 *    button that opened it.
 *  - Both fields are `autocomplete="off"` so the dispenser's saved password is never filled in for the
 *    witness.
 *  - The credentials live in this component's local state ONLY — never in a query key, the URL,
 *    storage or a log. They are cleared on submit, on cancel, on close, on unmount and whenever the
 *    caller reports an `error` (the password first; focus returns to the identifier).
 *  - There is no override and no "skip witness" control: only Confirm or Cancel (BR-7).
 *  - A failure shows the server's one generic message (it never says which half was wrong).
 *
 * Reused by HO-SRG-002's anaesthesia log.
 */
export interface WitnessCredentials {
  identifier: string;
  password: string;
}

export function WitnessDialog({
  open,
  onSubmit,
  onCancel,
  witnessRoles,
  busy,
  error,
  confirmLabel = 'Confirm and dispense',
  title = 'Controlled drug — witness needed',
}: {
  open: boolean;
  onSubmit: (credentials: WitnessCredentials) => void;
  onCancel: () => void;
  /** Role names the witness may hold ("Vet", "Nurse") — written out for the person at the desk. */
  witnessRoles: string[];
  busy: boolean;
  /** The server's message for the last attempt; a NEW value clears the password and refocuses. */
  error?: string | null;
  confirmLabel?: string;
  title?: string;
}) {
  const [identifier, setIdentifier] = React.useState('');
  const [password, setPassword] = React.useState('');
  const identifierRef = React.useRef<HTMLInputElement>(null);
  const returnFocusRef = useDialogReturnFocus(open);
  const roleText =
    witnessRoles.length > 0
      ? witnessRoles.map((role) => role.toLowerCase()).join(' or ')
      : 'vet or nurse';

  // Closed (for any reason) or unmounted: nothing is kept.
  React.useEffect(() => {
    if (!open) {
      setIdentifier('');
      setPassword('');
    }
  }, [open]);
  React.useEffect(() => {
    return () => {
      setIdentifier('');
      setPassword('');
    };
  }, []);
  // A refusal: the password is cleared and focus goes back to the identifier — also when the SAME
  // message comes back twice (the check is on the request finishing, not on the text changing).
  const wasBusy = React.useRef(false);
  React.useEffect(() => {
    if (wasBusy.current && !busy && error) {
      setPassword('');
      identifierRef.current?.focus();
    }
    wasBusy.current = busy;
  }, [busy, error]);
  React.useEffect(() => {
    if (error) {
      setPassword('');
      identifierRef.current?.focus();
    }
  }, [error]);

  function submit(event: React.FormEvent) {
    event.preventDefault();
    if (busy || !identifier.trim() || !password) return;
    const credentials = { identifier: identifier.trim(), password };
    setPassword('');
    onSubmit(credentials);
  }

  return (
    <Dialog open={open} onOpenChange={(next) => !next && !busy && onCancel()}>
      <DialogContent hideClose returnFocusRef={returnFocusRef}>
        <form onSubmit={submit} autoComplete="off" noValidate>
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <ShieldAlert size={18} aria-hidden="true" />
              {title}
            </DialogTitle>
            <DialogDescription>
              Controlled drug — a second member of staff must witness. Witness: enter your own
              sign-in details. The witness is a {roleText} and must not be the person dispensing.
            </DialogDescription>
          </DialogHeader>
          <div className="flex flex-col gap-3">
            <div className="flex flex-col gap-1.5">
              <label htmlFor="witness-identifier" className="text-sm font-semibold text-ink">
                Witness mobile number or email
              </label>
              <input
                ref={identifierRef}
                id="witness-identifier"
                name="witness-identifier"
                type="text"
                autoComplete="off"
                autoCapitalize="off"
                spellCheck={false}
                data-1p-ignore
                data-lpignore="true"
                value={identifier}
                onChange={(event) => setIdentifier(event.target.value)}
                className="w-full rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
              />
            </div>
            <div className="flex flex-col gap-1.5">
              <label htmlFor="witness-password" className="text-sm font-semibold text-ink">
                Witness password
              </label>
              <input
                id="witness-password"
                name="witness-password"
                type="password"
                autoComplete="off"
                data-1p-ignore
                data-lpignore="true"
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                className="w-full rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
              />
            </div>
            {error && (
              <p role="alert" className="text-sm font-semibold text-danger-ink">
                {error}
              </p>
            )}
          </div>
          <DialogFooter>
            <Button variant="secondary" onClick={onCancel} disabled={busy}>
              Cancel
            </Button>
            <Button
              type="submit"
              disabled={busy || !identifier.trim() || !password}
              aria-busy={busy}
            >
              {busy ? 'Checking…' : confirmLabel}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
