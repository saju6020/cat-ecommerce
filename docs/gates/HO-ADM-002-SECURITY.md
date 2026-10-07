GATE: SECURITY
HANDOVER: HO-ADM-002
COMMITS: f93b790 6f9da0a 5598fa5 14ea722 b7f18d9 338834f 994305c f2a8970 b7b244d ceb0019 62178ab 2299141 (range 08e873e..2299141)
DATE: 2026-09-28
STATUS: PASS

FINDINGS
- [MEDIUM] web/components/content/LimitedMarkup.tsx:24 — §8.5 says links must be https or site-relative. The client accepts any url that starts with `/`, so `[x](//evil.example)` renders as an `<a>`. The server (src/Shared/CatEcommerce.Shared/Common/Helpers/LimitedMarkup.cs:40) rejects a leading `//`, but it still lets through `/\evil.example` and `/<TAB>/evil.example`. Browsers resolve both of these to `https://evil.example/...` under the WHATWG URL rules (checked with Node's URL parser). Exploit: someone with content-authoring rights publishes a page, FAQ or policy containing `[Log in to your account](/\phish.example)`. On the public `/pages/[slug]`, `/faqs` and `/policies/[key]` pages, and in the portal, this renders as an internal-looking same-tab link (line 64 treats it as internal, so there is no target or rel) that goes off-site. The result is phishing or an open redirect, not script execution, because no javascript: or data: URL can start with `/`. Fix: on the client, allow a site-relative link only if it matches `^/(?![/\\])` and contains no ASCII tab, CR or LF, or resolve it with `new URL(url, location.origin)` and require the same origin. Apply the same rule in the server's LimitedMarkup.Validate (HO-ADM-001 follow-up). Add `//`, `/\` and `/\t/` cases to tests/components/content/limited-markup.test.tsx.
- [LOW] web/components/content/PolicyAcceptancePrompt.tsx:38 — `pending = data ?? []` means the prompt fails open. If `GET /api/customers/me/policy-acceptances/pending` errors (5xx or network), the portal is fully usable with no prompt. Nothing on the server blocks portal endpoints while an acceptance is pending, so for this period the client prompt is the only thing asking for acceptance. Fix: when the query errors, render the blocking dialog in an error or retry state, or accept this as a policy decision.
- [LOW] (note, server-side) CSV injection is handled by the server (AdminAuditEntriesController.cs:116-136 prefixes cells that start with a formula character, and the export sets `Cache-Control: no-store`). The client builds the export URL with URLSearchParams (web/lib/api/admin-audit.ts:32-45), so a filter value cannot inject extra query parameters. Nothing to change on the client.

Verified clean (no finding):
- There is no `dangerouslySetInnerHTML` or markdown/HTML parser anywhere in the diff. All admin-authored text (page, FAQ and policy bodies and titles, audit before/after values in DiffTable) goes through React text nodes. javascript:, data: and mailto: links fall back to literal text. External https links get `rel="noopener noreferrer"`. Banner `linkUrl` is not rendered on any customer or public surface yet.
- X-Branch-Id (web/lib/branch.ts, web/lib/api/client.ts:216-217): the value comes from the TanStack cache, is seeded from the server's `/api/staff/branches/mine`, is sent last so a caller's own headers cannot override it, and is not persisted anywhere. A user can set any value from DevTools, but HO-ADM-001 §8.3 enforces assignment on the server (`403 BranchNotAssigned`, I-B5). Sign-out and IdleLock call `queryClient.clear()`, so one staff member's branch does not carry over to the next.
- Policy prompt: the client never unlocks on its own. It closes only after the POST succeeds and the pending list, refetched from the server, comes back empty. The dialog cannot be dismissed, and only sign-out is reachable.
- Maintenance mode: only `/staff/admin/system` reads the status, and nothing in the staff or portal shells gates on it (ADR-053 holds).
- Public routes (`(auth)/pages`, `faqs`, `policies`): the server view models (PublishedContentViewModel, PolicyVersionViewModel) and the client types carry no author, staff ID or other staff PII.
- CSRF: every new mutation goes through `apiFetch`, which adds `X-CSRF-Token` on POST, PUT, PATCH and DELETE. There are no bare `fetch` calls in non-test, non-mock code. The audit export is a GET navigation.
- Mocks and secrets: the MSW handlers and their sessionStorage fixtures are only reached through the existing dev-only dynamic import in app/providers.tsx. No app, component or lib module imports `mocks/`. No secrets are in the diff.
- Dependencies: web/package.json and web/package-lock.json are unchanged.
- `npx tsc --noEmit` is clean. `npm run lint` passes with 0 warnings. vitest limited-markup and policy-acceptance-prompt: 5/5 pass.

SCOPE CHECK
- files in allowed scope: 130/130 (web/** plus docs/handover/HO-ADM-002.md, docs/test-cases/TC-HO-ADM-002.md, docs/backlog/ADM.md, docs/gates/HO-ADM-002-ARCHITECTURE.md)
- files outside allowed scope: none
