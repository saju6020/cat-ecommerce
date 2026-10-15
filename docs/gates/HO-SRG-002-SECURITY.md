GATE: SECURITY
HANDOVER: HO-SRG-002
COMMITS: b95263fd 14098a39 a49cb937 9bbf3a17 1626a126 e8de5bf1 6ea95dc8 cfcd13ac df419408 (range 5a187b6b..df419408)
DATE: 2026-10-14
STATUS: PASS

FINDINGS
- [LOW] web/lib/api/surgeries.ts:356-443, web/lib/api/anaesthesia-log.ts:134-164, web/lib/api/surgical-reports.ts:57, web/lib/api/surgery-campaigns.ts:101-118, web/lib/api/my-surgeries.ts:50 — route-param ids (`surgeryId`, `entryId`, `followUpId`, campaign `id`) are put into API paths without `encodeURIComponent`. Most other `lib/api/*` modules encode them. Next decodes `%2F` and `%3F` in `[surgeryId]`, so a crafted link like `/staff/surgeries/..%2F..%2F<x>` makes the page send its credentialed same-origin GET to a different `/api/**` path, or adds an injected query string. The impact is limited: the request is a GET, CSRF still guards writes, every backend route has a `{id:guid}` constraint and does its own authorization, and a shape mismatch fails to render. This is defence in depth and a break from the codebase convention. Fix: wrap every path segment in `encodeURIComponent`. Backlog row proposed.
- [LOW] web/components/surgery/StartPanel.tsx:158-161 — `startSurgeryWithConsentOverride` returns `{ consentWasOverridden, overrideIgnored }`, but the UI always toasts "started with an emergency override". This is not a security bypass, because the server records what really happened. The vet could still be told an override was recorded when it was ignored (for example, consent arrived meanwhile), or the other way round. Fix: pick the toast from `consentWasOverridden` / `overrideIgnored`. Backlog row proposed.

VERIFIED (no finding)
- Witness credentials:
  - `useLogDrug` and `useCorrectEntry` are plain `useCallback` functions, not `useMutation`, so no `variables` are kept in the MutationCache. Vitals and milestones, which carry no credential, use mutations.
  - The credentials never go into a query key, the URL, storage or a log. There is no `console.*` in the diff.
  - The error messages are client-owned codes and text (`WitnessNotVerified` is generic).
  - WitnessDialog (HO-PHM-002, reused unchanged) clears the password on submit, on a refusal and on close, and clears both fields on close or unmount. It has no skip control.
  - Both fields use `autocomplete="off"` with the 1Password and LastPass ignore attributes, so the signed-in user's saved password is not autofilled.
  - DrugEntryDialog and CorrectEntryDialog close the witness dialog on any non-witness error, which clears the fields. The credentials exist only as call arguments.
- Consent override:
  - The override is a separate endpoint (`start-with-consent-override`). The plain start sends no body.
  - The backend has `[RequireAction(Surgery.OverrideConsent)]` plus a handler role check.
  - The UI's `roles.has('Vet')` only decides what is shown.
  - A 403 or a refusal is shown through `describeSurgeryError` (server violation text or `describeForbidden`), and the dialog returns to the reason step.
- Clinical data:
  - The staff surgery, anaesthesia and report endpoints, and the customer endpoints, send `Cache-Control: no-store`.
  - There is no persisted query client. Sign-out and the session guard run `queryClient.clear()`.
  - The report autosave keeps its draft in component state and refs only. There is no localStorage or sessionStorage.
  - The post-op sheet uses the same `useSurgery` query and shows a 403 as AccessRefusedState.
- Owner pages:
  - `CustomerSurgeriesController` scopes by `user.UserId`, and another customer's surgery is a 404.
  - The `MySurgeryDetail` contract and view carry no recovery status, clinical notes, anaesthesia data or staff fields.
  - The portal nav item has audience `portal` and no role gate, so it is always shown.
- bookFollowUpLink:
  - `followUpBookingPath` keeps only the `search` of a URL whose pathname is exactly `/book`. Otherwise it falls back to `/book?catId=<encoded>&category=FollowUp`.
  - The output always starts with `/book` and the origin is dropped, so there is no open redirect, no protocol-relative URL and no `javascript:` URL.
- XSS:
  - Report text, addenda, timeline entries, blocker messages and post-op instructions are rendered as React text nodes. There is no `dangerouslySetInnerHTML` or `innerHTML` in the diff.
  - Every `href` is a fixed prefix (`/staff/...`, `/portal/...`, `#entry-`, `tel:`) followed by server ids or values.
- Role-driven controls:
  - StartPanel, recovery and report controls combine `allowedTransitions` with `useStaffRoles()` to decide what is shown.
  - The backend enforces each action through `RequireAction` and the handler checks (HO-SRG-001, gated).
- The MSW mocks and e2e specs log no credentials.

SCOPE CHECK
- files in allowed scope: 123/123 (web/**, docs/handover/HO-SRG-002.md amendment, docs/test-cases/TC-HO-SRG-002.md)
- files outside allowed scope: none

PROPOSED BACKLOG ROWS (docs/backlog/SRG.md)
- LOW, SEC — the surgery API modules (`surgeries.ts`, `anaesthesia-log.ts`, `surgical-reports.ts`, `surgery-campaigns.ts`, `my-surgeries.ts`) do not `encodeURIComponent` path-segment ids. A crafted `[surgeryId]` with `%2F` or `%3F` changes the same-origin API path. Fix: encode every segment, matching the other `lib/api` modules.
- LOW, SEC/UX — the StartPanel override toast ignores `overrideIgnored` and `consentWasOverridden`. Fix: show what the server actually recorded.
