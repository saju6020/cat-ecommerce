/**
 * `components/notifications/SandboxedEmailPreview.tsx` — HO-NTF-002 §8.4, §11.3, ADR-FE-160
 * (handover ADR-FE-020). **The only component in `web/` allowed to set `srcDoc`**; an ESLint
 * `no-restricted-syntax` rule fails `npm run lint` anywhere else.
 *
 * `sandbox=""` — the empty string, not a list: no scripts, forms, popups, same-origin access, top
 * navigation or pointer lock. The document is inert even if the server's renderer had a bug. The
 * `htmlBody` prop is passed ONLY from the preview endpoint's response (`TemplatePreviewPanel`) —
 * never from anything the person typed, and never from the notification log or the inbox, which
 * show plain text (ADR-FE-162).
 *
 * `referrerPolicy` on the `<iframe>` governs only the frame's own navigation request; it does nothing
 * for a `srcDoc` document, whose subresources (e.g. `<img src="https://tracker/...">`) would still
 * load and reveal the administrator's IP and origin. So `srcDoc` is prefixed with a CSP meta
 * (`default-src 'none'; img-src data:; style-src 'unsafe-inline'` — nothing fetched from the network)
 * and a no-referrer meta. (NTF backlog N9.)
 *
 * Fixed height with its own scroll, so a long email never pushes the editor off screen.
 */
export const PREVIEW_DOCUMENT_PREFIX =
  `<meta http-equiv="Content-Security-Policy" content="default-src 'none'; img-src data:; style-src 'unsafe-inline'">` +
  '<meta name="referrer" content="no-referrer">';

export function SandboxedEmailPreview({ htmlBody }: { htmlBody: string }) {
  return (
    <iframe
      sandbox=""
      srcDoc={PREVIEW_DOCUMENT_PREFIX + htmlBody}
      title="Email preview"
      referrerPolicy="no-referrer"
      className="h-96 w-full rounded-[var(--radius-sm)] border border-border bg-white"
    />
  );
}
