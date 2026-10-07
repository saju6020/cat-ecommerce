GATE: SECURITY
HANDOVER: HO-NTF-002
COMMITS: cffaa79..1a5965d (head 1a5965d, branch ho/NTF-002: 83a4f10, 275cf07, d7ce065, 86c0098, 778fae8, 260e31b, 66e8535, 1a5965d)
DATE: 2026-10-04
STATUS: PASS

There are no BLOCKER or HIGH findings. There are 3 LOW findings. Under the owner rule (2026-09-27) they do not block the gate. They belong in docs/backlog/NTF.md, which this gate has not edited.

FINDINGS
- [LOW] web/eslint.config.mjs:139-158 — The srcDoc lint rule is narrower than its comment says ("the one HTML rendering path ... anywhere else it is a lint error").
  - Verified live with `eslint --stdin` (components/x/Probe.tsx):
    - `<iframe srcDoc={h} />` is caught.
    - `<iframe {...{ ["src" + "Doc"]: h }} />` is not caught. It is a computed spread key, and it also type-checks.
    - `iframe.setAttribute("srcdoc", h)` is not caught.
  - Lowercase JSX `srcdoc` is not caught by lint, but `tsc` rejects it.
  - Nothing requires `sandbox` on any other `<iframe>`.
  - All of these are deliberate evasions, and no code in the branch uses them. The rule is defence in depth, not the control, so this is LOW.
  - Fix:
    - Ban the `<iframe>` JSX element outright outside SandboxedEmailPreview.tsx (`JSXOpeningElement[name.name='iframe']`).
    - Add `CallExpression[callee.property.name='setAttribute'][arguments.0.value=/^srcdoc$/i]`.
- [LOW] web/components/notifications/SandboxedEmailPreview.tsx:17-20 — `sandbox=""` does block script, forms, popups, same-origin access and top-window navigation, and that is verified.
  - `referrerPolicy="no-referrer"` does nothing for a srcdoc frame. It governs only the iframe's own initial fetch, and a srcdoc frame has none.
  - Two things remain:
    - A link in the previewed HTML still navigates the iframe itself when clicked.
    - Remote subresources (`<img src="https://...">`) still load.
  - Those requests carry the editor page's origin under the browser default policy. The site sets no Referrer-Policy or CSP (next.config.ts has no headers), and the srcdoc document inherits that.
  - Effect: whoever controls an image host named in a template learns that the admin previewed it, plus the admin's IP and the site origin. Only Administrators can write template content (`NotificationTemplates.Manage`), so the attacker would already be an admin. LOW.
  - Fix: prepend `<meta http-equiv="Content-Security-Policy" content="default-src 'none'; img-src data:; style-src 'unsafe-inline'">` and `<meta name="referrer" content="no-referrer">` to the srcDoc string. Alternatively, have the preview endpoint neutralise remote URLs. Correct the component comment.
- [LOW] web/components/notifications/NotificationLogFilters.tsx:16, web/lib/api/notifications-log.ts:168 — The destination search term (a full email address or mobile number) is kept in two places:
  - in the page URL (`useUrlFilterState`, UI-4);
  - in the GET query string to `/api/admin/notifications`.
  - It therefore lands in browser history and in any reverse-proxy or API access log.
  - The API shape is HO-NTF-001's contract (GET with `destination=`). The front end can only choose not to persist it in the page URL.
  - Fix:
    - Keep `destination` out of the URL-synced fields, or store only the selected `recipientId`, which is already the preferred path through the customer lookup.
    - Longer term, the backend could accept the search as a POST body.

CHECKED — NO FINDING
- Email preview: `htmlBody` reaches SandboxedEmailPreview only from the server preview response (TemplatePreviewPanel / PreviewChannelView).
  - There is no `dangerouslySetInnerHTML` or `innerHTML` anywhere in app/, components/ or lib/ (grep).
  - The sandbox is the empty string, so the frame gets no allow-scripts, allow-top-navigation, allow-popups or allow-same-origin.
- Plain-text rendering: inbox title and body (InboxItem.tsx), log subject, title and body (NotificationContent.tsx), and provider responses are React text children with `whitespace-pre-wrap`. Email HTML is never rendered in the log or the inbox.
- isSafeInternalPath (web/lib/routing/isSafeInternalPath.ts) rejects every bypass tried:
  - `//evil` and `/\evil`;
  - any backslash;
  - C0 controls and DEL, which covers the tab and newline that the WHATWG parser strips;
  - `javascript:` and any scheme, since the path must start with `/`;
  - leading whitespace.
  - Encoded `/%2F/evil` and `/%5Cevil` stay same-origin paths, because the URL parser does not decode them into an authority.
  - Non-ASCII whitespace is not stripped by URL parsing.
  - The View link renders only when the check passes.
- Unsubscribe page (app/(auth)/unsubscribe/page.tsx):
  - The token is read from the `#token=` fragment, which matches NotificationLinkBuilder.cs:28 on master. Its alphabet is base64url, so URLSearchParams decoding is safe.
  - `history.replaceState` strips the token before first render. The page renders null until the effect runs.
  - Nothing is sent on load. `useUnsubscribe` is a mutation fired only from the button.
  - The token goes only in the POST body. It is never put in the query string, storage or the query cache, and is never logged (client.ts has no console calls).
  - There is no analytics or telemetry in the app.
  - The "Manage" link carries no token.
- Permissions, cross-checked with master controllers:
  - Templates: AdminNotificationTemplatesController has View on GET and preview, and Manage on PUT and reset.
    - The FE maps Manage to Administrator, matching ActionRoles.Notifications.cs.
    - For non-Administrators the editor is read-only: no Save or Reset, fields readOnly, palette disabled.
  - Log: AdminNotificationsController has NotificationLog.View on list, summary and detail, and NotificationLog.Resend on resend.
    - The Resend UI follows the server's `canResend`, which is not permission-aware. View and Resend are granted to the same roles (Reception, Administrator), so no gate is shown that the server lacks, and the server still enforces Resend.
  - Customer filter: it uses HO-ACC-002 `GET /api/staff/customers` (Customers.View: Reception, Nurse, Vet, Administrator), which is a superset of the log roles.
  - A 403 renders PermissionDeniedState. Nav entries are presentation only.
- Customer data:
  - The list shows `destinationMasked` only.
  - The full destination appears only on the staff detail view, as the server returns it under NotificationLog.View.
  - Redacted bodies show only a reason.
  - The resend wording for another channel ("{name}'s email address on file") reveals no destination that the role cannot already see.
- Optimistic concurrency (useTemplateWrites.ts, useNotificationTemplates.ts):
  - `expectedContentVersion` is the version the form was loaded from. It advances only on save, reset or an explicit Reload, never on a background refetch.
  - The server checks it on save and on reset (ResetNotificationTemplateCommandHandler.cs:25).
  - A 409 shows Reload and keeps the typed values.
  - If the post-write re-read fails, the stale version simply produces a 409 on the next save, which is safe.
- Mocks: MSW starts only when `NODE_ENV === 'development' && NEXT_PUBLIC_USE_MOCKS === 'true'` (app/providers.tsx:112), through a dynamic import.
  - No new non-test, non-mock module imports `@/mocks`.
  - The uncommitted web/public/mockServiceWorker.js was left untouched.

SCOPE CHECK
- files in allowed scope: 91/91. The scope is §13 plus the out-of-scope files that §1 Amended (2026-10-04) itemises, each with a reason: accounts mock, NotificationPreferenceMatrix props, portal-header slot, nav/areas/notifications.ts, unit test, adminNavItems entry.
- files outside allowed scope: none
