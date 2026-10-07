# HO-NTF-002 — Notifications (frontend)

## 1. Identity

| Field | Value |
|---|---|
| Handover ID | **HO-NTF-002** |
| Title | Notifications — frontend slice |
| Date | 2026-09-22 |
| Author | Claude Opus 5 |
| Backlog | [`docs/USER_STORIES.md`](../USER_STORIES.md) v2.4 §13 — **US-NTF-01 … US-NTF-06** |
| SRS | FR-NTF-1, 2, 4, 6; FR-POR-6 (preferences); **UI-1, UI-3, UI-4, UI-6**; NFR-USA-4, NFR-USA-5; NFR-LEG-4 |
| Pairs with | [`HO-NTF-001`](HO-NTF-001.md) — **§12.4 there is the contract**. On any disagreement about the wire, HO-NTF-001 wins (§20.4) |
| Builds on | [`HO-CAT-002`](HO-CAT-002.md) (scaffold, stack), [`HO-ACC-002`](HO-ACC-002.md) (session guards, `client.ts`, route groups, preference matrix, customer detail, error mapping) and [`HO-ADM-002`](HO-ADM-002.md) (admin shell, `adminNavItems`, `lib/format`) |
| Delivery order | Section 4 of 17 in [`README.md`](README.md#delivery-order) |
| Approved design | None. Build to §11 with the existing shadcn/ui tokens |
| Baseline commit | `4007225` |
| Amended | **2026-10-04 — ADR numbering (implementation, by this handover's own build).** §9's `ADR-FE-019…023` are recorded as **`ADR-FE-159…163`** in `web/docs/decisions.md` (019 -> 159, 020 -> 160, 021 -> 161, 022 -> 162, 023 -> 163), with a numbering note there. On master `cffaa79` `ADR-FE-014…020` were already used by HO-ADM-002, `039…148` are allocated to later handovers and `149…158` are used by HO-FIL-002. Same pattern HO-ADM-002 used; `docs/handover/README.md` is outside this handover's scope and is left for the integrator to reconcile. Every code comment and test name in this slice uses the 159…163 numbers. <br><br>**2026-10-04 — implementation, files outside §13 (each with its reason; nothing else outside §13 was touched).** (1) **`web/mocks/handlers/areas/accounts.ts`** — the accounts mock owns `GET/PUT /api/customers/me/notification-preferences` and the seeded customer, so it gained the HO-NTF-001 §12.4 amendment (`preferredChannel`, `effectivePreferredChannel`, `availablePreferredChannels` on GET; `preferredChannel` on PUT with `InvalidPreferredChannel` / `PreferredChannelHasNoDestination`) and three small additive exports the notifications mock needs: `getStaffRoleIdsForRequest` (so a nurse's cookie gets the `403`, E-E2), `revokeMarketingConsent` / `grantMarketingConsent` (so an unsubscribe link turns the seeded customer's marketing preference off, E-D1). (2) **`web/components/account/NotificationPreferenceMatrix.tsx`** — three optional props (`beforeTable`, `extraDirty`, `onServerError`) so `PreferredChannelField` sits inside the SAME form and the same explicit Save (§11.2, ADR-FE-008): without them the field could only be a second form with a second Save. No behaviour changes when the props are omitted (its existing test file passes unchanged). (3) **`web/components/common/portal-header.tsx`** — a `sessionActions` slot; §13 allows `(portal)/layout.tsx` to gain `InboxBadge`, but the header markup lives in this common component, so the layout passes the badge in and the header renders it only once a session exists (a signed-out visitor never polls the unread count). (4) **`web/nav/areas/notifications.ts`** instead of `(staff)/layout.tsx` for the staff **Messages** entry — the nav registry (`web/README.md` "Adding a functional area") is how every area adds a staff nav item, `StaffShell` renders it, and the layout needs no edit; shown to every staff role (presentation only), so a nurse reaches the server's `403` as `PermissionDeniedState` (E-E2). (5) **`web/tests/unit/is-safe-internal-path.test.ts`** — §13 lists only `tests/components/**` and `tests/e2e/notifications-*.spec.ts`; the pure path-safety function is a unit test, not a component test. (6) **`web/components/admin/adminNavItems.ts`** — §13 allows "one entry only" and the **Message templates** entry was NOT present on master `cffaa79` (HO-ADM-002 did not add it despite the pre-implementation amendment), so this build adds it (Administrator, ShopManager = `NotificationTemplates.View`). <br><br>**2026-10-04 — implementation, deviations and decisions the human should confirm.** (1) **Mock path.** §12's `web/mocks/handlers/notifications/*.ts` is built as named and wired in `mocks/handlers/index.ts` under an alias (as HO-ADM-002 did); `mocks/handlers/areas/notifications.ts` stays the empty stub. (2) **`InboxBadge` at 0.** §11.1 says "hidden at 0"; the count bubble is hidden at 0 but the link to `/portal/inbox` stays (accessible name "Inbox"), otherwise the inbox would be unreachable with no unread mail. (3) **Resend confirm for a different channel.** §11.5's *"Send this message again by {channel} to {destination}?"*: the detail endpoint returns only the ORIGINAL message's destination, so resending by a different channel names whose contact detail is used (*"…by email to Sam Rahman's email address on file?"*); the same channel shows the full destination. (4) **Reset endpoint body.** HO-NTF-001 §12.4's table lists `POST …/reset` without a body; the real controller (`ResetNotificationTemplateRequest`) requires `{ expectedContentVersion }`, which is what is sent. (5) **Save/reset re-read.** Both return `204`, so the hook re-reads the template afterwards to learn the new `contentVersion` (needed as the next `expectedContentVersion`); the editor sends the version its form values were loaded from, never a background-refetched one, so someone else's edit is never silently overwritten. (6) **Default log range.** `to` is the later of the browser's local date and the UTC date (the API's day boundaries are UTC), so a message created a moment ago is always inside the default last-7-days range. (7) **Customer filter.** "HO-ACC-002's customer search" is `GET /api/staff/customers?mobile=|email=` (no free-text name search exists): staff type a mobile number or email, pick the match, and the filter stores its id. (8) **Template editor for ShopManager.** `NotificationTemplates.View` includes ShopManager, `.Manage` is Administrator only: a non-Administrator sees the editor read-only with no Save / Reset (presentation; the server still decides). (9) **Mock defaults.** The in-app default bodies of the two care templates include `{{bookingLink}}` because HO-NTF-001 §8.2 makes it required on every channel's body. (10) **E-E1 role.** Run as the seeded Administrator: the mock accounts store has no Reception account that can sign in; both roles hold `NotificationLog.View/Resend`. <br><br>**2026-10-04 — real-API runs not performed.** §16's "then the real API" column (E-A1…E-X2) cannot be produced from this worktree (no database, no API process); recorded as NOT RUNNABLE in `docs/test-cases/TC-HO-NTF-002.md`. E-D1 is MSW-only by §16's own wording. **Contract observation, not shimmed (§20.4):** `GET /api/admin/notifications` answers a missing or over-long range with `400 { "error": "…" }`, not the `ValidationResponse` shape; the UI never sends such a range (it is refused client-side first), so nothing here depends on it. |

---

## 2. Objective

Build the screens that let people **see and control messages**:

- **Customers**: an in-app inbox with an unread badge, a choice of preferred channel on the existing
  notification-preferences page, and a public unsubscribe page reached from marketing messages.
- **Administrators**: a template editor with placeholder insertion and a live, server-rendered preview.
- **Reception and administrators**: a searchable notification log, a detail view with every delivery
  attempt, and a manual resend. The log also warns when no SMS or email provider is connected.

Three things make this harder than it looks:

1. **Message content is untrusted.** A template body or a cat's name can contain anything. Inbox and
   log bodies are shown as **plain text**. The one place an HTML email is shown, the template preview,
   is a fully sandboxed frame fed only by the server's renderer (ADR-FE-020).
2. **The client never renders templates.** Placeholders are substituted by the server's preview. A
   client-side copy of the rendering rules would drift from what is actually sent (ADR-FE-023).
3. **No provider is chosen yet** (HO-NTF-001 OQ-1). Outside Development, SMS and email end
   `Undeliverable`. The log must make that obvious, not look like a bug (§11.5).

---

## 3. In scope

### 3.1 Inherited — do not revisit

The HO-CAT-002 stack and layering; HO-ACC-002's session model, `client.ts`, route groups, error mapping,
`PermissionDeniedState`, `UnsavedChangesGuard` and the explicit-save rule for preferences
(ADR-FE-008); HO-ADM-002's admin shell, `adminNavItems` and `lib/format`.

### 3.2 Stories and screens

| Story | Screens | Route |
|---|---|---|
| US-NTF-01 | **Inbox** list with unread state; **unread badge** in the portal header; **preferred channel** on the preferences page | `/portal/inbox`; `(portal)` layout; `/portal/account/notifications` (HO-ACC-002's page) |
| US-NTF-02 | Template list; template editor with channel tabs, variable palette, live preview, save, reset to default | `/staff/admin/notification-templates/**` |
| US-NTF-04 | Public unsubscribe page; the consent matrix itself is HO-ACC-002's and unchanged | `/unsubscribe` |
| US-NTF-05 | No screen. The log shows *"Held for quiet hours until 08:00"*; quiet hours are edited on HO-ADM-002's settings screen | — |
| US-NTF-06 | Notification log with filters and paging; notification detail with attempts, chain and **Resend**; queue summary with the provider warning; a **Messages** link from the staff customer page | `/staff/notifications/**`; `/staff/customers/[customerId]` (HO-ACC-002's page) |
| US-NTF-03 | No screen. Reminders arrive in the inbox and on the preferred channel | — |

---

## 4. Out of scope

| Excluded | Why |
|---|---|
| Any backend change | HO-NTF-001 owns `src/**` |
| Choosing or configuring an SMS/email provider | HO-NTF-001 OQ-1 |
| Push notifications, a service worker, web push permission prompts | Not routed in v1 (HO-NTF-001 §4.3) |
| WebSocket or server-sent events for the badge | Polling (ADR-FE-019) |
| Deleting inbox items | HO-NTF-001 OQ-8 |
| Rich-text or HTML template editing | Templates are `LimitedMarkup` / plain text (HO-NTF-001 ADR-062) |
| Client-side placeholder substitution or markup rendering of templates | ADR-FE-023 |
| Campaign sending | Not in the backlog |

Forbidden shapes are HO-ACC-002 §4.3's, unchanged: no global store, no token in JS, no `fetch` outside
`client.ts`, no `dangerouslySetInnerHTML`, no client-side authorization decision.

---

## 5. Prerequisites

| Required | State |
|---|---|
| Node 20 LTS, HO-CAT-002 scaffold | ❌ at baseline — §20.1 |
| **HO-ACC-002 built** — portal and staff layouts, preference page, customer detail page, `client.ts` | ❌ at baseline — §20.1 |
| **HO-ADM-002 built** — admin shell, `adminNavItems`, `lib/format` | ❌ at baseline — §20.1 |
| HO-NTF-001 API | ❌ — build against MSW generated from §12.4 there, then re-run every E-case against the real API (HO-ACC-002 §5.1's rules) |

---

## 6. Governing documents

| Document | What you need |
|---|---|
| [`HO-NTF-001`](HO-NTF-001.md) | §12.4 endpoints and shapes; §10.4 codes to map; §8.2 template limits and variable kinds; §8.5 routing rules (so screens explain what happens); §8.10 why reset messages cannot be resent |
| [`HO-ACC-002`](HO-ACC-002.md) | §8 session model; §8.6 accessibility additions; §11.2 client; §11.7 the preference page; §11.14 error mapping |
| [`HO-ADM-002`](HO-ADM-002.md) | §8.4 `lib/format`; §11.1 admin nav |
| [`docs/SRS.md`](../SRS.md) §3.1, §6.4 | UI-1/3/4/6; NFR-USA-4/5 |

---

## 7. Build rules

| Req | Rule | Where |
|---|---|---|
| **UI-1** | 360 px floor. The log table becomes a card list below 768 px; the template editor stacks the preview **below** the fields below 1024 px | §11.4, §11.5, E-X2 |
| **UI-3** | Confirm, stating what is affected: saving a template, resetting it to default, resending a message, unsubscribing | Each screen |
| **UI-4** | The log paginates and filters; filters live in the URL | §11.5 |
| **UI-6** | WCAG 2.1 AA. The unread badge has a text alternative (*"3 unread messages"*); status is text, never colour alone; the preview frame has a `title`; tab panels use the ARIA tabs pattern | HO-ACC-002 §8.6 applies |
| **NFR-USA-4** | Server messages in plain language; `errorCode` never rendered; `cannotResendReason` shown as a sentence | HO-ACC-002 §11.14 |
| **NFR-USA-5** | The template editor keeps its values across a validation error; `UnsavedChangesGuard` on the editor and on the preferences page | §11.3 |

---

## 8. Frontend architecture

### 8.1 Route map — additions

```
web/app/
  (portal)/
    layout.tsx                                  ⚠ gains InboxBadge in the header          US-NTF-01
    portal/inbox/page.tsx                       inbox                                     US-NTF-01
    portal/account/notifications/page.tsx       ⚠ gains PreferredChannelField (HO-ACC-002) US-NTF-01
  (auth)/
    unsubscribe/page.tsx                        public unsubscribe                        US-NTF-04
  (staff)/staff/
    admin/notification-templates/page.tsx       list                                      US-NTF-02
    admin/notification-templates/[key]/page.tsx editor                                    US-NTF-02
    notifications/page.tsx                      log + summary                             US-NTF-06
    notifications/[notificationId]/page.tsx     detail + resend                           US-NTF-06
    customers/[customerId]/page.tsx             ⚠ gains a Messages link (HO-ACC-002)      US-NTF-06
  (staff)/layout.tsx                            ⚠ one staff-nav entry: Messages
```

`adminNavItems` (HO-ADM-002) gains **Message templates**.

### 8.2 API modules

`lib/api/inbox.ts` (list, unread count, mark read), `lib/api/unsubscribe.ts`,
`lib/api/admin-notification-templates.ts` (list, detail, save, reset, preview),
`lib/api/notifications-log.ts` (list, detail, resend, summary). HO-ACC-002's `lib/api/account.ts`
gains the three preferred-channel fields on its preference types.

### 8.3 State

| Kind | Where |
|---|---|
| Inbox pages, unread count | TanStack Query `['inbox', …]`, `['inbox', 'unread-count']`; marking read invalidates both |
| Templates | `['admin', 'notification-templates']`, `[…, key]` |
| **Preview** | `useQuery` keyed on a stable hash of the unsaved contents and samples, `enabled` after a 600 ms debounce, `AbortSignal` passed to `client.ts` so a newer preview cancels an older one (ADR-FE-023). `placeholderData: keepPreviousData` so the panel does not flash |
| Log, detail, summary | `['notifications', 'log', params]`, `[…, id]`, `['notifications', 'summary']`; a resend invalidates the detail and the log |
| Log filters | **URL search params** |
| Editor values | react-hook-form + zod |

### 8.4 Rendering message content — ADR-FE-020, ADR-FE-022

| Content | How it is shown |
|---|---|
| Inbox title and body; log subject, title and body; provider responses | **Plain text** in a `whitespace-pre-wrap` element. Never markup, never `LimitedMarkup`, never HTML |
| In-app `linkPath` | Followed only when `isSafeInternalPath(path)`: starts with `/`, not `//`, not `/\`, no scheme. Otherwise the item has no link |
| Email preview `htmlBody` (template editor only) | `<iframe sandbox="" srcDoc={htmlBody} title="Email preview">`. **`sandbox` is the empty string** — no scripts, forms, popups, same-origin access or top navigation. The value comes only from the preview endpoint's response |
| Email in the log | Its **text** body only. The log never renders HTML |
| `"[redacted]"` bodies | *"Content removed — {reason}"*, e.g. *"it contained a password-reset link"* |

`components/notifications/SandboxedEmailPreview.tsx` is the **only** component in `web/` allowed to
set `srcDoc`. A lint rule (`no-restricted-syntax` on `srcDoc` outside that file) enforces it.

---

## 9. ADRs and open questions

Record in `web/docs/decisions.md`, continuing HO-ADM-002's series.

| ADR | Decision | Why |
|---|---|---|
| **ADR-FE-019** | The unread count polls every **60 s** and on window focus, paused while the tab is hidden; no WebSocket, SSE or push | The inbox is not time-critical; urgent messages also go by SMS/email |
| **ADR-FE-020** | The email preview is server-rendered HTML shown **only** in a `sandbox=""` iframe via `srcDoc`, in one component, enforced by a lint rule. It is the one HTML rendering path in `web/` | An admin must see what the email looks like; the sandbox makes the HTML inert even if the renderer had a bug |
| **ADR-FE-021** | The unsubscribe token is read from the URL **fragment**, removed from the address bar at once (`history.replaceState`), and submitted only on an explicit click; no session needed | Fragments never reach servers or logs; mail scanners prefetch links, so the page must not act on load |
| **ADR-FE-022** | Message bodies in the inbox and the log are plain text; in-app links are followed only when site-relative | Bodies carry customer-supplied values; plain text removes the whole injection class |
| **ADR-FE-023** | The client never substitutes placeholders or renders templates; it inserts `{{name}}` tokens and asks the server to preview (debounced 600 ms, latest wins) | One renderer — the one that sends — so the preview cannot lie |

| # | Question | Default |
|---|---|---|
| OQ-1 | Badge cap | `99+` |
| OQ-2 | Inbox page size | 20 |
| OQ-3 | Should opening an inbox item mark it read, or need a button? | Opening marks it read |
| OQ-4 | Default log range | Last 7 days |

---

## 10. Dependencies

**No new package.** A rich-text editor, template engine, HTML sanitiser, markdown library, WebSocket
client or date library beyond HO-CAT-002's is a §20 stop.

---

## 11. Specified changes — components and screens

Each component under ~150 lines, with distinct loading / empty / error states.

### 11.1 Inbox — US-NTF-01

| Component | Behaviour |
|---|---|
| ⭐ `InboxBadge` | In the `(portal)` header: a link to `/portal/inbox` with the count from `['inbox','unread-count']`; hidden at 0; `99+` above 99; accessible name *"Inbox, 3 unread messages"*. Polling per ADR-FE-019 |
| ⭐ `InboxList` | Newest first, paged (20); filter **All / Unread**; each row: title, first line of the body, relative time with the absolute time in `lib/format`, unread marker as **text** ("Unread") plus weight. Empty state: *"No messages yet. Reminders and updates about your cats will appear here."* |
| `InboxItem` | Expands in place (`<details>`-like disclosure button); on open → `POST …/read` (OQ-3), invalidate; if `linkPath` is safe, a **View** link. Body in plain text (§8.4) |

### 11.2 Preferred channel — US-NTF-01 (on HO-ACC-002's page)

`PreferredChannelField` — above HO-ACC-002's `NotificationPreferenceMatrix`, inside the **same form and
the same explicit Save** (ADR-FE-008): a radio group *"Send important messages by"* with **Text message
(SMS)** and **Email**, from `availablePreferredChannels`. A channel the account has no identifier for
is shown disabled with *"Add an email address to your profile to choose email"* and a link to the
profile tab. The current effective choice is selected even when `preferredChannel` is null. Help text:
*"Password resets, reminders and updates are sent this way, and a copy always appears in your inbox."*
The server's `PreferredChannelHasNoDestination` maps to this field.

### 11.3 Template editor — US-NTF-02

| Component | Behaviour |
|---|---|
| `TemplateTable` | Name, key, category (*Transactional* / *Marketing*), channels, *Customised* or *Default*, last edited. Row → editor |
| ⭐ `TemplateEditor` | Header: name, description, category, urgency (*"Ignores quiet hours"* for urgent), and for secret templates a notice: *"Contains a password-reset link. It is never copied to the inbox and cannot be resent."* **Channel tabs** (ARIA tabs) for each channel of the definition: email → Subject + Body; SMS → Body; in-app → Title + Body + Link; push (if present) → Title + Body with the note *"Push messages are not sent in this version."* Each field shows its limit counter from `limits`. **Save changes** (confirm: *"Messages sent from now on will use this wording. Messages already sent keep theirs."*) sends `expectedContentVersion`; `409` → *"Someone else changed this template. Reload to see their version."* with **Reload**. **Reset to default** (confirm) only when customised. `UnsavedChangesGuard` |
| ⭐ `VariablePalette` | Lists the definition's variables and the standard ones: name, description, sample, and a **Required** badge on channels in `requiredOn`. Clicking inserts `{{name}}` at the caret of the **last focused** field of the active tab (react-hook-form `setValue`, caret restored). `Url` variables are offered in the email body as `[Link text]({{name}})` |
| ⭐ `TemplatePreviewPanel` | Uses the preview query (§8.3) with the unsaved values. **SMS**: the text, then *"{characters} characters · {segments} SMS · {GSM-7 \| Unicode}"*; a `Unicode` note explains that non-English characters shorten each SMS. **Email**: subject line, `SandboxedEmailPreview`, and a disclosure *"Plain-text version"*. **In-app**: a card mirroring `InboxItem`. **Sample values** can be edited in a small form under the panel (they are sent as `sampleValues`, never saved). A preview `400` shows the server's message **inline in the panel and on the offending field** (mapped by `propertyName`) without blocking typing. The named-variable message is shown verbatim — it is the AC |
| `SandboxedEmailPreview` | §8.4. Fixed height with its own scroll; `title="Email preview"`; the only `srcDoc` in `web/` |

### 11.4 Unsubscribe — US-NTF-04

`UnsubscribePage` (`(auth)` shell, client component):

1. On mount, read `token` from `location.hash`, then `history.replaceState` to `/unsubscribe` (ADR-FE-021).
2. No token → the invalid-link state.
3. Show *"Stop marketing messages?"*, *"You'll still receive important messages about your account,
   your cats and your orders."*, and an **Unsubscribe** button. **Nothing is sent on load.**
4. `200 wasSubscribed: true` → *"Done. You won't receive marketing {emails \| text messages \| in-app
   messages} from us any more."*; `false` → *"You were already unsubscribed from these messages."*
   Both link to *"Manage all your preferences"* (`/sign-in?next=/portal/account/notifications`).
5. `400 InvalidUnsubscribeLink` → *"This link isn't valid any more. You can manage your preferences
   after signing in."* `429` → *"Too many attempts. Please wait a minute and try again."*

### 11.5 Notification log — US-NTF-06

| Component | Behaviour |
|---|---|
| ⭐ `NotificationQueueSummary` | Top of `/staff/notifications`: queued, retrying, sending, age of the oldest queued message, and last-24-hour counts, refetched every 30 s. **When `channelsConfigured.sms` or `.email` is false**, a warning banner (`role="status"`, not an alert that interrupts): *"SMS is not connected to a provider. Text messages — including password resets — are not being delivered. Customers still receive in-app copies where available."* |
| ⭐ `NotificationLogFilters` | Date range (**required**, default last 7 days, max 93 — shown before submit), customer (HO-ACC-002's customer search → id), destination (free text; the server normalises), channel, template (from the template list), status. All in the URL; back button restores them |
| `NotificationLogTable` | Time, template, channel, destination (masked), recipient, status as text (*Sent*, *Retrying*, *Held until 08:00*, *Undeliverable*, …), attempts, *Resend* / *Fallback* tags, last provider response (truncated). Paged. Below 768 px: cards |
| ⭐ `NotificationDetail` | Full destination; recipient with a link to the customer page; template and version; category and urgency; subject/title/body as plain text or the redaction notice; **attempts table** (time, outcome, provider, provider message id, provider response as text); **chain**: links to the request's other notifications, the fallback source or fallbacks, and the resend source or resends |
| ⭐ `ResendPanel` | When `canResend`: channel select from `resendableChannels` (default the original), confirm *"Send this message again by {channel} to {destination}?"*; on `201` → *"Queued"* or *"Held for quiet hours until {time}"* with a link to the new notification. When not: `cannotResendReason` as a sentence, e.g. *"This message contained a password-reset link and can't be resent. Ask the customer to request a new one."* `ResendLimitReached` → *"This message has already been resent 3 times."* |
| `CustomerMessagesLink` | On HO-ACC-002's staff customer page: **Messages** → `/staff/notifications?recipientId={id}` with the default range. Shown only to roles in the presentation map for `NotificationLog.View` (the server still decides) |

### 11.6 Error mapping — `lib/api/errors.ts`

Append `propertyName → field` entries for C1 (`Contents[i].Subject/Title/Body/LinkPath` → the channel
tab's field; the tab containing the first error is activated), C7 (`Channel`), and the preferences
`PreferredChannel`. Map `TemplateVersionConflict`, `NotificationCannotBeResent`, `ResendLimitReached`,
`ResendChannelNotAvailable` and `InvalidUnsubscribeLink` to the messages in §11.

---

## 12. Mocks — `web/mocks/handlers/notifications/*.ts`

Generated from HO-NTF-001 §12.4. Fixtures: three shipped templates (one secret, one customised) and a
**test marketing template** with `unsubscribeLink`; preview responses for GSM-7 and Unicode SMS and an
email whose `htmlBody` contains a `<script>` (to prove the sandbox) and a cat name with markup
characters; preview `400`s for `UnknownTemplatePlaceholder`, `MalformedTemplatePlaceholder` and
`RequiredTemplatePlaceholderMissing`; a `409` on save; an inbox with read, unread and linked items,
including one with an unsafe `linkPath` (`//evil.example`); log pages covering every status, a
fallback chain, a resend chain, a held-for-quiet-hours row and a redacted reset message; summaries
with both channels configured and with SMS unconfigured; unsubscribe `200` (true/false), `400`, `429`;
preferences with and without an email identifier.

---

## 13. Allowed scope

```
web/app/(portal)/portal/inbox/**
web/app/(portal)/layout.tsx                                   ← InboxBadge only
web/app/(portal)/portal/account/notifications/page.tsx        ← PreferredChannelField only (HO-ACC-002's page)
web/app/(auth)/unsubscribe/**
web/app/(staff)/staff/admin/notification-templates/**
web/app/(staff)/staff/notifications/**
web/app/(staff)/staff/customers/[customerId]/page.tsx         ← CustomerMessagesLink only (HO-ACC-002's page)
web/app/(staff)/layout.tsx                                    ← one nav entry only
web/components/{inbox,notifications,templates}/**
web/components/account/PreferredChannelField.tsx
web/components/admin/adminNavItems.ts                         ← one entry only (HO-ADM-002's file)
web/lib/api/{inbox,unsubscribe,admin-notification-templates,notifications-log}.ts
web/lib/api/account.ts                                        ← preferred-channel fields only
web/lib/api/types.ts · web/lib/api/errors.ts                  ← append
web/lib/hooks/**                                              ← new hooks only
web/lib/routing/isSafeInternalPath.ts
web/lib/validation/**
web/mocks/handlers/notifications/** · web/mocks/handlers/index.ts
web/eslint.config.mjs                                         ← the srcDoc restriction only (§8.4)
web/tests/components/{inbox,notifications,templates,account}/**
web/tests/e2e/notifications-*.spec.ts
web/docs/decisions.md                                         ← ADR-FE-019…023
docs/test-cases/TC-HO-NTF-002.md · docs/gates/HO-NTF-002-*.md
```

## 14. Forbidden scope

`src/**`, `tests/**`, `.ai/**`, `.claude/**`, `docs/**` (except the two named paths), every other
HO-CAT-002, HO-ACC-002 and HO-ADM-002 file beyond the lines in §13, `web/package.json`,
`tsconfig.json`, `next.config.ts`.

---

## 15. Component test cases

| ID | Case | Expected |
|---|---|---|
| U-A1 | `InboxBadge` with 0, 3, 150 | Hidden; "3" with *"Inbox, 3 unread messages"*; "99+" |
| U-A2 | Badge polling | Refetch every 60 s and on focus; none while the document is hidden |
| U-A3 | `InboxItem` open | `POST …/read` once; badge count invalidated |
| U-A4 | Item with body `<b>hi</b>` and `**x**` | Literal text |
| U-A5 | Item with `linkPath` `/portal/cats/1`, `//evil.example`, `https://evil.example`, `/\evil` | Link / no link / no link / no link |
| U-B1 | `PreferredChannelField` with both identifiers; mobile only | Both enabled; email disabled with the hint |
| U-B2 | Change channel and a matrix cell, save | One `PUT` with both; nothing sent before Save |
| U-B3 | Server `PreferredChannelHasNoDestination` | Error on the field |
| U-C1 | `VariablePalette` insert while the SMS body is focused at a caret | `{{catName}}` inserted at the caret; caret after it |
| U-C2 | Preview debounce: five keystrokes in 300 ms | One request after 600 ms; an earlier in-flight one aborted |
| U-C3 | Preview `400 UnknownTemplatePlaceholder` | The server's message (naming `{{catname}}`) shown in the panel and on the field; typing not blocked |
| U-C4 | SMS preview Unicode, 2 segments | *"… · 2 SMS · Unicode"* and the note |
| U-C5 | Email preview containing `<script>` | Rendered inside an iframe whose `sandbox` attribute is `""`; no script runs (the mock sets a global the test checks) |
| U-C6 | Lint rule | A test file using `srcDoc` outside `SandboxedEmailPreview` fails `npm run lint` |
| U-C7 | Save `409` | Conflict message with Reload; values kept |
| U-C8 | Secret template | Notice shown |
| U-C9 | Save error on the email subject while the SMS tab is active | Email tab activated, field focused |
| U-D1 | `UnsubscribePage` load with `#token=abc` | No request on load; hash removed from the URL |
| U-D2 | Click Unsubscribe → `200 true` / `200 false` / `400` / `429` | The four messages of §11.4 |
| U-D3 | No token | Invalid-link state, no button |
| U-E1 | `NotificationQueueSummary` with SMS unconfigured | Warning banner with `role="status"` |
| U-E2 | Log filters round-trip | URL params; back button restores; > 93 days blocked before submit |
| U-E3 | Log row statuses | Each shown as text; held row shows the time in the branch zone |
| U-E4 | Detail of a redacted reset | *"Content removed — it contained a password-reset link"*; resend refusal sentence |
| U-E5 | Detail body with markup characters; provider response with `<script>` | Plain text |
| U-E6 | `ResendPanel` confirm and `201` held | Confirm names channel and destination; *"Held for quiet hours until 08:00"* with a link |
| U-E7 | `ResendLimitReached` | The limit sentence |
| U-X1 | Every form: server `400` | Mapped to its field; `errorCode` not in the DOM |
| U-X2 | Editor and preferences: dirty + navigate | Unsaved-changes confirm |

## 16. End-to-end test cases

MSW first, then the real API (Development senders; the worker running); both runs recorded.

| ID | Case | Expected |
|---|---|---|
| E-A1 | Real API: a cat with a vaccination due in 14 days; restart the Api (the sweep runs once at start) | The customer's inbox shows the reminder with a **View** link to the cat page; the badge shows 1 |
| E-A2 | Open the item | Badge clears |
| E-B1 | Change preferred channel to email and save | Saved; reload shows email |
| E-C1 | Admin edits the vaccination template with `{{catname}}` | Preview and save both show the named-variable error |
| E-C2 | Fix it, save, reset to default | Customised → Default |
| E-D1 | Unsubscribe from a marketing email link. **MSW run only** — no marketing template ships in this slice, so the real-API run is recorded as *not runnable* until the first one (HO-SHP back-in-stock); HO-NTF-001 I-E1…I-E3 prove the server half | Success; the preference matrix shows email marketing off |
| E-E1 | Reception opens the log, filters by the customer, opens a failed SMS, resends by email | New notification linked in the chain |
| E-E2 | Nurse opens `/staff/notifications` | `PermissionDeniedState` from the server's `403` |
| E-X1 | axe on every route in §8.1, clean and with errors, with the editor preview open | Zero serious/critical |
| E-X2 | Every route at 360 / 768 / 1024 / 1440 | No horizontal scroll, no clipped control |

---

## 17. Implementation order

1. Confirm prerequisites (§5) — else stop.
2. Types and API modules from HO-NTF-001 §12.4; MSW handlers; `isSafeInternalPath`; the lint rule.
3. Inbox and badge → preferred channel field.
4. Unsubscribe page.
5. Template list and editor: tabs, palette, preview panel, sandboxed email preview.
6. Log, summary, detail, resend, customer-page link.
7. U-series, E-series on MSW, then E-series against the real API per HO-NTF-001 phase.

```bash
cd web && npm run lint && npm run typecheck && npm run test && npm run test:e2e && npm run build
```

---

## 18. Acceptance criteria

| # | Story | Criterion | Evidence |
|---|---|---|---|
| A1 | NTF-01 | Customers read messages in an inbox with an unread badge | U-A1…U-A3, E-A1, E-A2 |
| A2 | NTF-01 | Customers choose their preferred channel, limited to the identifiers they have | U-B1…U-B3, E-B1 |
| A3 | NTF-02 AC1 | Administrators edit templates with placeholders and preview them with sample data before saving | U-C1, U-C2, U-C4, U-C5, E-C2 |
| A4 | NTF-02 AC2 | An invalid placeholder is refused with the variable named, in the preview and on save | U-C3, E-C1 |
| A5 | NTF-04 AC1 | Marketing messages' unsubscribe links work without signing in and never act on page load | U-D1…U-D3, E-D1 |
| A6 | NTF-06 AC1 | Staff search the log by customer, destination, channel, template, status and date, and see every attempt | U-E2, U-E3, E-E1 |
| A7 | NTF-06 AC2 | Staff resend a failed or missed message, and are told plainly when it cannot be resent | U-E4, U-E6, U-E7, E-E1 |
| A8 | HO-NTF-001 OQ-1 | An unconnected provider is visible to staff, not silent | U-E1 |
| A9 | — | No message content is rendered as HTML except the sandboxed preview | U-A4, U-A5, U-C5, U-C6, U-E5 |
| A10 | UI-6 / UI-1 | Zero serious/critical axe violations; 360–1440 px | E-X1, E-X2 |

---

## 19. Required gates

| # | Gate | Focus |
|---|---|---|
| 1 | ARCHITECTURE | §8 layering; no new dependency; the scoped edits to HO-ACC-002 and HO-ADM-002 files match §13; no client-side template rendering |
| 2 | SECURITY | The sandboxed preview (`sandbox=""`, one component, lint-enforced); plain-text bodies; `isSafeInternalPath`; the unsubscribe token handling (fragment, removal, no action on load); resend confirm shows the destination |
| 3 | CODE_REVIEW | Preview debounce and abort; polling paused in background; component size |
| 4 | TEST | `TC-HO-NTF-002.md` first; mock and real runs separate |
| 5 | ACCEPTANCE | Every §18 row |

---

## 20. BLOCKED conditions — stop and report

**20.1 A prerequisite is missing** (Node, HO-CAT-002 scaffold, HO-ACC-002, HO-ADM-002). Do not build a
parallel scaffold, shell or preference page.

**20.2 Message content seems to need HTML rendering outside the template preview.** It is a new
decision; do not add it.

**20.3 The preview seems to need client-side rendering** (to be faster, to work offline). Do not
re-implement the server's renderer — ADR-FE-023.

**20.4 The real API disagrees with HO-NTF-001 §12.4.** Report it; do not shim.

**20.5 A screen needs an endpoint HO-NTF-001 does not specify.** Report it; do not invent a mock field.

**20.6 A new dependency seems necessary** (§10). Raise it.
