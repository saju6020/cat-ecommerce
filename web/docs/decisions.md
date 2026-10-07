# web/docs/decisions.md — Accounts & Identity frontend decision log

MEDIUM/LOW gate item 8 (first pass, HO-ACC-002 first-pass gate fix round, 2026-09-26) — this file
did not exist before this commit; HO-ACC-002 §9 asks for it, and no earlier build (HO-CAT-002 or
otherwise) created it first, so no renumbering was needed. `.ai/decisions/adr.md` is the
**backend's** decision log and is forbidden scope for this frontend slice — nothing here duplicates
or supersedes anything recorded there.

Twelve ADRs were specified by HO-ACC-002 §9 before this build started (`ADR-FE-001`…`012`); one more
(`ADR-FE-013`) was recorded during this gate-fix round, for a decision made while fixing a gate
finding rather than during the original build. Each entry below states the decision as actually
**built** — where an owner correction landed differently from the original proposal, that
correction is what's recorded as the decision, with the original proposal kept (struck through) for
context, exactly as HO-ACC-002 §1 Amended and §9 already do inline.

---

## ADR-FE-001 — Three route groups, not two

**Decision.** `(auth)` — public, a minimal shell, no session guard. `(portal)` — behind
`CustomerSessionGuard`. `(staff)` — behind `StaffSessionGuard` and `IdleLock`.

**Why.** HO-CAT-002 shipped two route groups (`(staff)`/`(portal)`), both of which assumed a
signed-in identity was always available (`lib/identity.ts`'s single hardcoded staff identity).
Accounts & Identity adds the screens where no identity exists yet (sign-in, registration, password
reset) — a third, guard-free group is the natural home for them, rather than carving exceptions
into the two guarded groups.

**Status.** Built as specified. See `app/(auth)/`, `app/(portal)/layout.tsx`,
`app/(staff)/layout.tsx`.

---

## ADR-FE-002 — Cross-origin `fetch`, not a same-origin proxy (corrected 2026-09-26)

**Decision (as built).** The browser calls the API directly, cross-origin, with
`credentials: 'include'` — `lib/api/client.ts`'s `NEXT_PUBLIC_API_BASE_URL` design, inherited
unchanged from HO-CAT-002. Session cookies (`pc_session`/`pc_staff_session`) are `HttpOnly`;
`pc_csrf` is not (it has to be readable by `client.ts` to echo into the `X-CSRF-Token` header). The
browser never holds a bearer token of any kind.

~~**Original proposal (§12.2, never built).** Session transport is `HttpOnly` cookies over a
same-origin proxy (`next.config.ts` rewrites), so the browser only ever talks to its own origin and
CORS/same-site never come up.~~

**Why the correction.** The owner's actual deployment (2026-09-26, §1 Amended) puts the web app and
the API on two subdomains of one registrable domain — never literally same-origin — so the proxy
proposal was moot before anything was built against it; `lib/api/client.ts` had already kept
HO-CAT-002's own direct-fetch design throughout, unmodified, and that is what ships. The
requirement this imposes lands at the deployment/env layer instead: a `SameSite=Lax` cookie is only
ever sent on a same-**SITE** request (stricter than CORS, and scheme-sensitive — "schemeful
same-site"), so production needs both subdomains on `https`, and development needs the API's `http`
launch profile (`http://localhost:5005`) to match `npm run dev`'s own plain-`http` origin. See
`web/README.md`'s "Calling the API — same-SITE, not just CORS" for the full requirement, and
`HO-ACC-002.md` §1 Amended (2026-09-26) for the owner's own wording.

**Why (original rationale, still true of what shipped).** `HttpOnly` cookies survive a reload (an
in-memory bearer token does not) and are unreadable to XSS. Matches HO-ACC-001 ADR-031.

**Status.** Built as corrected — no proxy, `next.config.ts` has no rewrites for this.

---

## ADR-FE-003 — Sessions are TanStack Query state, never a store

**Decision.** "Who is signed in" is server data (`useCustomerSession()`/`useStaffSession()`,
`['session', 'customer'|'staff']`), never copied into `useState`, `useReducer`, or a third-party
store. Sign-in success clears the **whole** query cache (`queryClient.clear()`) before navigating,
not just the session key.

**Why.** No new state-management library is introduced (see the "don't invent a second store"
guidance this codebase already follows). A single source of truth means there is no second copy
that can drift from what the server actually thinks. Clearing the whole cache on sign-in (not just
the session key) exists because a different person signing in on the same tab must never see the
previous person's still-cached data — see `lib/hooks/useSession.ts`'s own comment on why a bare
`invalidateQueries()` was tried first and rejected (the guard being navigated into isn't mounted
yet at the moment sign-in succeeds, so `refetchType: 'active'` would leave the stale previous value
in place for the guard's very next read).

**Status.** Built as specified; strengthened in the first-pass gate fix round (SECURITY gate item 6) — both sign-in `onSuccess` handlers call `queryClient.clear()` directly now, replacing an
earlier narrower helper.

---

## ADR-FE-004 — `lib/identity.ts` is deleted outright

**Decision.** HO-CAT-002's single hardcoded staff identity module is deleted, not deprecated or
left dead. MSW mode supplies identities through mocked session endpoints
(`mocks/handlers/areas/accounts.ts`), exactly like the real API does.

**Why.** A hardcoded identity that survives, even unused, into a real build is an authentication
bypass waiting to be re-wired accidentally. Deleting it outright — rather than leaving it unused —
makes that impossible by construction.

**Status.** Built as specified. `lib/identity.ts` does not exist in this branch;
`web/README.md`'s "Don't edit" list has been corrected in this gate-fix round (item 17) to say so,
since it previously still listed the file as a "don't touch it" exception.

---

## ADR-FE-005 — Password length checked both sides; breach check server-only

**Decision.** Minimum length (10 characters) is checked client-side (`zod`, immediate feedback)
**and** server-side (authoritative). A breached-password check is server-side only — the client
never ships or consults a breach list.

**Why.** NFR-SEC-1 is a server obligation the client cannot discharge on its own; a client-side
breach list would leak (a static asset an attacker can just read) and bloat the bundle for no
security benefit, since the server check is authoritative regardless.

**Status.** Built as specified. See `RegisterForm.schema.ts`'s `registerSchema` (client) and the
`PasswordTooShort`/`BreachedPassword` mappings in `applyValidationErrors` (server).

---

## ADR-FE-006 — Self-hosted CAPTCHA; refetch after every failed submit

**Decision.** The CAPTCHA is the backend's own self-hosted image (`useCaptcha()`); the client never
judges an answer itself. After **any** failed submit that reached the server (validation, `429`,
`5xx`, or a network failure), the password and CAPTCHA answer fields are cleared and a fresh
challenge is fetched. A client-side-only validation failure (caught before the request is even
sent) does **not** spend the challenge.

**Why.** Server-issued CAPTCHA challenges are single-use; skipping the refetch after a failed
attempt guarantees the next submit is rejected for a stale challenge, not the actual reason the
user thinks it failed.

**Status.** Built as specified, in `RegisterForm.tsx`'s `onSubmit` catch block.

---

## ADR-FE-007 — Non-disclosing screens throughout auth

**Decision.** One identical sign-in error for every failure (wrong password, unknown identifier,
disabled account — indistinguishable). One client-owned confirmation message rendered on every
`200` from the password-reset-request endpoint, regardless of whether the identifier existed (the
response body itself is never rendered). The reset link is `/reset-password#token=…` — read once
from the URL fragment (never sent to the server as part of a GET, never logged by any proxy), then
stripped from the visible address bar; the page is served with `Referrer-Policy: no-referrer`. One
"this link is invalid or has expired" state covers every reason a reset attempt can fail.

**Why.** A server that carefully avoids disclosing which identifiers are registered is undermined
if the screen built on top of its responses varies by outcome anyway.

**Status.** Built as specified. See `SignInForm.tsx`, `ForgotPasswordForm.tsx`,
`ResetPasswordForm.tsx`/`ResetPasswordPageClient.tsx`.

---

## ADR-FE-008 — Route-based account tabs; explicit Save for compliance settings

**Decision.** The portal account area (`/portal/account/{profile,addresses,notifications,data}`) is
a route-based tab shell — each tab is a real, deep-linkable URL, not client-only tab state.
Compliance-relevant settings (notification preferences, account deletion) save on an explicit
button press only, never on toggle/blur/change.

**Why.** Deep-linkability lets a support flow or a bookmark land directly on the right tab.
Consent-adjacent actions (what a customer is contacted about, deleting their account) must be a
deliberate, reviewable act — an auto-save on toggle would make "I was just trying it out" a real,
unrecoverable risk.

**Status.** Built as specified. See `app/(portal)/portal/account/**` and each area's own
`UnsavedChangesGuard` usage (ADR-FE-013 below) for what happens if a customer tries to navigate away
mid-edit instead of saving.

---

## ADR-FE-009 — Role catalogue and capability matrix come from the API

**Decision.** The list of roles and what each one can do is rendered from
`GET /api/staff/admin/role-catalogue` (`StaffAccountForm`/`StaffAccountDetail`), never hardcoded in
the frontend. Role-based UI gating is presentation only — hiding a button never substitutes for the
server's own `403`, which renders `PermissionDeniedState`.

**Why.** FR-ADM-2/NFR-SEC-2 — the server is the actual authority on what a role can do; a frontend
copy of that list would drift, and relying on hidden-button gating as the real security boundary
would be a client-side-only control an attacker can simply route around.

**Status.** Built as specified.

---

## ADR-FE-010 — The idle lock is an in-place overlay, never a route

**Decision.** `IdleLock` wraps `{children}` inside the `(staff)` guard as a modal overlay; the
locked page stays mounted underneath it, so every open form's react-hook-form state survives
re-authentication completely untouched. Nothing about a draft is copied, serialised, or stored —
it survives purely because the component tree is never unmounted. After re-authenticating, the
lock lifts **only if** the re-signed-in `staffAccountId` matches the one held before the lock; a
different person signing in instead gets a discard notice and a full reload.

**Why.** US-ACC-08 AC2, for every existing and future staff-side form (C-9) — a route-based
"you've been logged out, please sign in again" page would unmount and lose whatever was being
typed.

**Status.** Built as specified; the overlay itself was corrected in the first-pass gate fix round
(SECURITY gate item 5) from the shared `Dialog`'s default translucent `bg-ink/40` overlay to an
opaque `bg-surface-page` (`components/ui/dialog.tsx`'s new, additive `overlayClassName` prop) — the
locked page staying mounted and legible **behind a translucent overlay** meant a customer's name,
phone numbers, addresses and medical data stayed readable on an unattended terminal after the
session had timed out, which is not what "never lose the draft" was meant to also guarantee.

Also amended in that same round (ARCH gate HIGH 1/HIGH 2, first pass): `getStaffSessionStatus()`
now distinguishes a `401 session_idle_timeout` from `session_invalid` (a new
`StaffSessionIdleTimeoutError`), so `StaffSessionGuard` no longer redirects on an idle-timeout —
only `IdleLock` does anything with it, by locking. Session-error routing moved from one global
handler guessing "staff or customer" from the request path to each guard (`CustomerSessionGuard`,
`IdleLock`) registering its own handler directly, since only one of `(portal)`/`(staff)` is ever
mounted for a given URL.

**Amendment (third-pass gate fix round, 2026-09-27) — a page's OWN query, not only the session
query.** ARCHITECTURE gate third pass, HIGH: staying mounted is not enough on its own if a page's
own query (`useStaffAccount(id)`, `useCat(id)` under `CatProfileDataShell`) independently 401s on
the SAME focus event that triggers the lock — TanStack keeps its stale `data` but sets
`isError: true`, and a page branching on `isError` alone still swaps the mounted form out for
`ErrorState`. `StaffLockProvider` (`IdleLockContext.tsx`) was given a second responsibility: while
locked, pause every query's own focus/reconnect-triggered refetch at the root
(`queryClient.setDefaultOptions`), so a focus event racing the lock can never resolve into a page's
state afterward. Every affected page/component also gained `isError && !data` (not `isError` alone)
as a second, independent layer — `StaffAccountDetailPage`, `StaffCustomerDetailPage`,
`MergeReview.tsx`.

**Amendment (fourth-pass gate fix round, 2026-09-27) — the root-cause mechanism itself replaced.**
ARCHITECTURE gate fourth pass, HIGH: `setDefaultOptions` never reaches an ALREADY-MOUNTED query
observer — `useBaseQuery` computes and pushes its own options into the observer once, and a
page/layout whose element identity is stable across a lock (the whole point of this ADR) never
re-renders just because `lock()` ran, so its query keeps whatever `refetchOnWindowFocus` it already
had. Replaced by pausing TanStack's own global `focusManager`/`onlineManager` singletons directly:
`setEventListener(...)` is swapped to a no-op while locked (detaching the real `window`
`visibilitychange`/`online`/`offline` listeners entirely, so no real event reaches either manager at
all) and each manager's own default setup is reinstalled on unlock and on unmount while still
locked — `StaffLockProvider` now owns this pause for the whole app, not only for the staff-session
query.

Two more direct-looking alternatives were tried and rejected, both confirmed empirically: manually
overriding what each manager REPORTS (`focusManager.setFocused(false)`/`onlineManager.setOnline
(false)`) also gates TanStack's own retryer (`canContinue()` in `retryer.js`), pausing an
ALREADY-IN-PROGRESS retry (e.g. `useStaffAccount`'s `retry: noRetryOn403`) until the override
lifts — proven live by a query hanging indefinitely instead of settling into the `isError` state
its own page's `isError && !data` guard depends on. `onlineManager.setOnline(false)` alone (without
a listener swap) was also rejected: its own real event listener calls `setOnline(true)`/`setOnline
(false)` directly (no neutral re-evaluation step the way `focusManager`'s listener has), so a real
reconnect during the lock silently overrode the forced value — and forcing `isOnline()` to `false`
at all also paused every OTHER `networkMode: 'online'` operation while locked, including the
re-sign-in `useMutation` call `IdleLock` itself uses to unlock, which would then never reach the
network at all, permanently stuck behind the very lock it exists to lift.

The third pass's own `cancelQueries(undefined, {revert: false})` call is REMOVED, not kept
alongside this fix: with `revert: false`, a cancelled query is dispatched as `isError: true`
regardless of whether it had any prior data, which could turn a page's very first load into an
error screen; `revert: true` (the untouched TanStack default) is what preserves a query's prior
data across a cancelled fetch instead. Pausing `focusManager`/`onlineManager` already stops a new
focus/reconnect-triggered fetch from ever starting while locked; nothing further needs cancelling.

`lock()`/`unlock()` are correctly idempotent (a `pausedRef` guard — a second `lock()` call while
already paused is a no-op, and `unlock()`/the unmount cleanup only ever restores once), and restore
on unmount while still locked (a client navigation away from every `(staff)/**` route, the only
place `locked` can ever be `true`), closing the fourth-pass MEDIUM the same review found — the App
Router has no route-change lifecycle hook the provider could otherwise use to detect this.

---

## ADR-FE-011 — Merge integrity: fresh read, no default survivor, one request

**Decision.** No survivor is ever pre-selected — staff must choose one explicitly. `MergeReview`
does a **fresh** `GET /api/staff/customers/{id}` read of every member on arrival, never trusting the
duplicates-report row it was reached from. One `POST .../merge` request per non-survivor member; the
server moves the cats — this component never calls a cat endpoint directly.

**Why.** UI-3 (a destructive, hard-to-reverse action needs deliberate confirmation, not a
convenient default), FR-ACC-10. A report row can be stale by the time staff act on it; re-reading
each record right before merging closes that window.

**Status.** Built as specified. `MergeReview.tsx`'s own comment records an open gap this ADR
surfaced but did not resolve: HO-ACC-001's fresh single-customer read reports only `catCount`, not
a per-cat list, unlike the duplicates report — tracked as item 13 of the first-pass gate fix round,
not silently patched by trusting the report row instead.

---

## ADR-FE-012 — Data the system doesn't record is stated as such, never shown as zero

**Decision.** Where this slice has no data for something the SRS doesn't ask the system to track
(orders, invoices, running balances), the screen says so explicitly (a "not yet recorded" notice),
never renders a bare `0`/`৳0.00`/empty table implying the true value is zero.

**Why.** A number on screen is a claim about reality. "Not recorded" and "recorded as zero" are
different facts, and only one of them is true here.

**Status.** Built as specified. See `NotYetRecordedNotice.tsx`, used from `MergeReview.tsx`.

---

## ADR-FE-013 — `UnsavedChangesGuard` replays a prevented click, never `window.confirm` (2026-09-26)

**Decision.** In-app navigation away from a dirty form no longer uses `window.confirm()`. Every
same-document `<a>` click is intercepted in the capture phase; if the form is dirty
(`when={true}`), the click is **always** prevented and the destination `href` captured into state,
which opens the shared `ConfirmDialog` (the same one every other confirmation in this app uses).
Confirming replays the navigation for real via `router.push(href)`; cancelling (or dismissing the
dialog any other way) simply never calls it, leaving the user exactly where they were — the same
observable outcome as `window.confirm()` returning `false`, reached without the browser's native
dialog. A real document unload (reload/close) still uses the native `beforeunload` prompt, because
the platform gives no cancellable, custom-UI-capable alternative for that specific case.

**Why.** `window.confirm()` is synchronous — its return value must be known before the click event
that triggered it finishes propagating. A React-rendered dialog is necessarily asynchronous (it
renders, waits for a later, separate click, then calls back), so it structurally cannot supply a
synchronous return value the way `window.confirm()` does. "Always prevent, capture the intended
destination, replay it only on confirm" is the standard way to reconcile a synchronous DOM event
with an asynchronous confirmation — it was not previously used anywhere else in this codebase, but
is the correct general pattern for this exact mismatch, not an app-specific workaround.

**Consequence, explicitly accepted.** The App Router has no `router.events`-style
navigation-blocking API (unlike the Pages Router), so this only ever intercepts a real, same-document
`<a>` click — a `router.push()`/`router.replace()` call made from outside a click handler (rare on
these forms) is not covered. This is a documented platform limitation, not a gap silently
introduced here.

**Amendment (second-pass gate fix round, 2026-09-26) — the browser's own Back/Forward buttons.**
CODE_REVIEW MEDIUM (second pass) found that Back/Forward fires `popstate`, not a click this
component's own capture-phase listener ever saw, so a dirty form was lost silently on Back the same
way it used to be lost on a link click before the decision above. `popstate` fires only **after**
the navigation already happened, so there is no cancellable browser API for it either — this uses
the standard **sentinel history entry** technique instead: while dirty, a duplicate history entry
for the current page is pushed once (`sentinelPushedRef` guards against pushing again on every
re-render while still dirty), so the first Back press lands on it — a same-URL, invisible "no-op"
navigation — and fires `popstate` before the browser would otherwise actually leave. The `popstate`
handler re-pushes the same sentinel again (`pushState` never itself fires `popstate`, so this is
safe) and opens the identical `ConfirmDialog`; confirming replays the _original_ Back intent for
real via `history.go(-2)` (one entry past the sentinel), guarded by a ref
(`isProgrammaticNavRef`) so the `popstate` that call itself produces is not re-intercepted as a
second user-initiated Back press.

**Consequence, explicitly accepted (second-pass amendment).** This technique depends on exactly one
extra history entry sitting between the user and "away" at any given moment, which holds for a
single Back press but can be defeated by pressing Back twice in rapid succession before React has a
chance to re-push the sentinel — a low-probability race on real hardware, not eliminated. Forward
after a genuine, confirmed Leave is not specially handled either — it simply navigates forward like
any other page, since `when` is false again once this component unmounts with the page it guarded.
Both are accepted rather than engineered around further, since the underlying problem (no
cancellable Back/Forward API) has no complete solution, only mitigations.

**Amendment (second-pass gate fix round, 2026-09-26) — same-origin-only link replay.** CODE_REVIEW
suggestion (second pass): the link-click path now only replays a same-origin `href` through
`router.push` — comparing the resolved `anchor.origin` against `window.location.origin`, reliable
regardless of whether the markup wrote a relative or absolute `href`. An absolute, cross-origin
`href` (a different site, `mailto:`, `tel:`, a download link) is left to navigate natively instead,
which `beforeunload` above already covers as a real document unload; handing it to `router.push`
(which only understands an in-app route) would either throw or silently do nothing useful.

**Amendment (third-pass gate fix round, 2026-09-27) — three more gaps in the sentinel technique,
fixed rather than accepted.** ARCHITECTURE gate third pass, LOW: **(a)** when the guarded page is
the tab's very first history entry, pushing a sentinel and later replaying `history.go(-2)` on
confirm is a silent no-op (nothing sits two entries back), and it also leaves
`isProgrammaticNavRef` set, which would swallow the next real Back press unguarded — the sentinel
is now only pushed when `window.history.length > 1`, falling back to `beforeunload` only in the
first-entry case, matching what a real Back press already does there regardless of this component.
**(b)** the confirmed-Leave replay could still trip a second, native `beforeunload` prompt for the
SAME decision — `whenRef` stayed `true` until the `when` prop itself caught up, a render this
component might not survive to see if the destination is another document — now disarmed
synchronously (`whenRef.current = false`) at the top of `onConfirm`, before either kind of replay.
**(c)** a sentinel used to be pushed again on every dirty→clean→dirty cycle (the tracking ref was
reset back to `false` whenever `when` went false), stacking duplicate sentinels that made extra Back
presses do nothing — one sentinel now lasts the lifetime of the component instance (one page visit),
never reset while still mounted.

**Amendment (fourth-pass gate fix round, 2026-09-27) — a wrong `history.length > 1` guess is now
made safe, not avoided.** CODE_REVIEW gate fourth pass, MEDIUM: the third pass's own (a) fix (only
push a sentinel when `window.history.length > 1`) rested on an inference that does not hold in both
directions. `length === 1` is unchanged and still fully reliable — it counts both directions, so a
lone value of 1 leaves no room for anything either way, meaning no sentinel is pushed and Back is
left entirely native, exactly as before. `length > 1`, though, does NOT reliably mean something is
behind: it also counts forward entries, so a page that is genuinely the tab's first entry can still
report `length > 1` if the user later navigated forward from it and pressed Back again
(back → forward → dirty → Back); the guard would then push a sentinel believing something real sits
behind it, and a confirmed Leave's single `history.go(-2)` would silently no-op (nothing really is
two entries back), stranding the user on the sentinel with no visible sign anything went wrong.
Rather than try to make the `length > 1` guess itself reliable (structurally impossible from one
number alone), the CONSEQUENCE of it being wrong is made safe instead: **(1)** each component
instance (`useId()`) stamps its own real entry once, on mount, with a base marker in `history.state`
(`__unsavedGuardBase`), and every sentinel it ever pushes carries that same marker (state spread
forward, never replaced) plus its own `__unsavedGuard` id; `onPopState` only treats a landing as
"our sentinel was just consumed, we are back on the page this instance guards" when the landed-on
state carries THIS instance's own base marker — landing anywhere else is left alone (no re-push, no
dialog; `beforeunload` remains the only guard for that transition) rather than reacting with a
certainty `history.length` could never really back up. **(2)** a confirmed Leave now replays via two
sequential `history.back()` calls, not one `go(-2)` — the first is guaranteed to land somewhere real
(the guarded page itself, always immediately behind the sentinel), and only once that step's own
`popstate` actually arrives does the second one run, to attempt moving past the guarded page too.
When nothing is genuinely behind it, that second call is a harmless no-op with no further
`popstate`, but the first step has already, correctly, left the user on the guarded page — never
stuck on the sentinel the way a single out-of-range `go(-2)` used to leave them.

**Amendment (fifth-pass gate fix round, 2026-09-27) — the "is this popstate ours" signal moved out
of `history.state` entirely.** CODE_REVIEW gate fourth pass, MEDIUM (closed this round): the fourth
pass's `__unsavedGuardBase`/`__unsavedGuard` marker pair, stamped into `history.state` via
`replaceState`/`pushState`, was silently strippable by Next.js's own App Router — traced directly in
the installed `next@15.5.26` source (`app-router.js`, the `router-reducer/*` files): the router
patches `history.pushState`/`replaceState` globally and calls `history.replaceState(historyState,
'', canonicalUrl)` for the CURRENT entry whenever the canonical URL is unchanged (a
`router.refresh()`, a Server Action, a same-URL `<Link>`/`router.push()`, or any ancestor layout
doing the same) — `historyState`'s own base object is `{}` unless `preserveCustomHistoryState` is
`true` (only for the very first render and a Back/Forward-triggered restore), discarding ANY
existing custom key without firing a `popstate` or otherwise notifying this component at all. Once
stripped, `onPopState`'s old check would fail for EVERY subsequent Back press for the rest of that
mounted instance's life — silently, with no dialog, no re-push, and no way to detect or recover,
exactly the class of failure NFR-USA-5 exists to prevent. Not triggered by either page this
component guards today (neither calls `router.refresh()` or a Server Action), but latent for any
future page that does. Fixed by moving the signal into `sentinelActiveRef`, a plain component-local
`useRef` that already existed (previously only gating "don't push a duplicate sentinel"): since this
component is the only thing that ever pushes or pops that sentinel, and a same-entry `replaceState`
never changes WHICH entry is current, only that entry's `state` content, the ref is never invalidated
by Next's own stripping — the whole failure class no longer applies, rather than being worked around.
`useId()`/the mount-time `replaceState` stamp are removed entirely; `pushSentinel` no longer writes
any marker into `history.state` at all. A new test reproduces Next's own `replaceState({}, ...)`
call by hand and confirms the guard still reacts to the next Back press (confirmed it fails against
the fourth-pass marker implementation).

**Amendment (fifth-pass gate fix round, 2026-09-27) — a narrower instance of the double-Back race,
in the two-step replay.** CODE_REVIEW gate fifth pass (SUGGESTION-strength, not blocking): between
the confirmed-Leave replay's first `history.back()` and its own `popstate`, a genuine extra user Back
press is possible on real hardware (`history.back()` is asynchronous). If it lands as the SECOND
`popstate` received, `isProgrammaticNavRef.current` is by then `true` again (re-armed for the
replay's second leg), so the user's own real `popstate` is misattributed as the replay's own step —
silently consumed, and the actual second-leg `popstate` (now unguarded) is then treated as a fresh
real Back press. Net effect: the browser can end up one entry further back than the replay intended,
with no confirm dialog for that extra step. This is not a new, independent failure mode — it is the
SAME underlying cause the double-Back race above already documents and accepts (no cancellable
Back/Forward API, so a real press racing a programmatic one can only be bounded, never eliminated),
just reached through the two-step replay specifically rather than the initial sentinel push. It is
also not a data-loss path: `whenRef.current` is set `false` synchronously at the very start of
`onConfirm`, before either replay leg runs, so no `beforeunload`/dialog re-triggers for the
over-shot landing, and the page being guarded has already been deliberately abandoned by the user's
own confirm click.

**Status.** Built in the first-pass gate fix round (MEDIUM/LOW gate item 9), replacing the
`window.confirm()` this component originally shipped with; extended in the second-pass gate fix
round (Back/Forward interception, same-origin-only replay), the third-pass gate fix round (the
three fixes above), the fourth-pass gate fix round (the state-marking amendment, since replaced) and
the fifth-pass gate fix round (the two amendments above). Used from `ProfileForm.tsx` and
`NotificationPreferenceMatrix.tsx` (ADR-FE-008's "explicit Save" tabs — the two places a customer can
lose an un-saved, deliberate edit by navigating away).

---

## Numbering note — HO-ADM-002's block shifted by one (2026-09-27)

`docs/handover/README.md`'s ADR table allocates `ADR-FE-013…018` to HO-ADM-002. By the time this
branch started (fast-forwarded to master `08e873e`), HO-ACC-002 had already used `ADR-FE-013`
above for `UnsavedChangesGuard` — its thirteenth ADR, one beyond its own `001…012` allocation. To
avoid a duplicate heading in this file, HO-ADM-002's six ADRs are recorded as **`ADR-FE-014…019`**
instead — the same six decisions the handover's own §9 names as `ADR-FE-013…018`, shifted by one.
Flagged for the integrator to reconcile the README table (out of this handover's own file scope —
`docs/handover/README.md` is not in HO-ADM-002 §13); no other area's numbering is touched here.

## ADR-FE-014 — The admin area is a route-based section, not a modal stack

**Decision.** `/staff/admin/**` is a set of ordinary, deep-linkable App Router routes with its own
nav layout (`app/(staff)/staff/admin/layout.tsx`, `AdminNav`), not a stack of dialogs opened over
one shell route. Each admin resource (branches, services, tax classes, settings, audit log,
content, policies, system) is its own route and loads only its own data.

**Why.** Deep-linkability (bookmark a branch's hours tab; share a service's price history) and
per-route data loading both need a real route, not application state that only exists after a
click sequence. `AdminNav` filters `adminNavItems` (a presentation map mirroring HO-ADM-001 §12.3's
`ActionRoles` table by name only) against the signed-in staff member's `roleIds` — convenience, not
authorization: every page still renders `PermissionDeniedState` on the server's own `403` (U-A1).

**Status.** Built as specified. See `components/admin/AdminNav.tsx`, `adminNavItems.ts`.

## ADR-FE-015 — Branch time is rendered with `Intl.DateTimeFormat`, never a time-zone library

**Decision.** Every branch-local wall-clock value (opening hours, shifts, closures) is formatted
and labelled with its zone using `Intl.DateTimeFormat`'s own `timeZone` option
(`lib/format/time.ts`); a genuine instant (an audit-log row, a price's `effectiveFrom`, a calendar
interval) is converted the same way. No `moment-timezone`, `date-fns-tz` or similar dependency is
added (§10). A branch-local pair (`opens`/`closes`, both already local to the branch) is never
converted — only formatted and labelled — since there is nothing to convert.

**Why.** The runtime's built-in `Intl` API with IANA time-zone data is sufficient for every
formatting need this area has; a second library would duplicate that data and add a dependency
§10 explicitly forbids. Every time label ends with the zone name so it is never mistaken for the
browser's own zone (§8.4).

**Status.** Built as specified. See `lib/format/time.ts` (`formatInZone`, `zonedLocalToLabel`,
`timeFieldLabel`, `timeOnlyInZone`, `localDateOf`, `todayInZone`).

## ADR-FE-016 — The active branch is TanStack Query state (`['staff', 'active-branch']`) sent as `X-Branch-Id` by `client.ts`

**Decision.** The active branch id lives in exactly one place, the TanStack Query cache, under the
key `['staff', 'active-branch']` (§8.3) — there is no store of our own beside it. `useMyBranches()`
(`lib/hooks/useActiveBranch.ts`) fetches `GET /api/staff/branches/mine` and, the first time it
resolves with nothing cached yet, seeds that key from the server's own `activeBranchId`/
`primaryBranchId` via `queryClient.setQueryData`. `ActiveBranchPicker` is the only other writer,
also via `setQueryData`, and additionally invalidates the `['admin', ...]` query-key family so every
branch-scoped query refetches. `useActiveBranchId()` reads the same key with a plain `useQuery`
(fetching disabled — the value only ever arrives via `setQueryData`), so a component re-renders on
change exactly the way it would for any other cached resource.

`client.ts`'s `apiFetch` sends the value as `X-Branch-Id` on every request, reads included, not
only unsafe methods — but it runs synchronously and cannot call `useQueryClient()` (not a hook).
`lib/branch.ts`'s `getActiveBranchId()` is the one non-React read of the cache this needs: it calls
`getQueryData(['staff', 'active-branch'])` on a reference to the app's single `QueryClient`
instance, which `app/providers.tsx` hands over by calling `lib/branch.ts`'s own
`registerQueryClient()` from a `useEffect` — a reference to the one real store, not a second copy of
the value. That call has to live in an effect, not in the `useState` lazy initialiser that creates
the `QueryClient`: verified against a real `next dev` build, React Strict Mode there calls that
initialiser twice on mount and keeps only one call's result, so a registration made inside it can
permanently register the discarded instance instead — see `app/providers.tsx` and `lib/branch.ts`'s
own comments. `ActiveBranchPicker` is hidden outright when the signed-in staff member has one branch
or none. **Not persisted anywhere** — a reload always re-seeds from the primary branch.

**Why.** `apiFetch` cannot depend on React context, but the active branch is still fundamentally
server-seeded session-adjacent state, not client-invented state — TanStack Query, already the
single store for every other piece of server data in this app, is the smallest thing that satisfies
both, with no second, competing store alongside it. Never persisting it (no storage) means a stale
branch choice can never survive a reload or leak between two people sharing a terminal.

**Status.** Reworked after ARCHITECTURE gate HO-ADM-002-ARCHITECTURE's HIGH finding (first pass):
the build originally shipped as a module-level variable plus a subscriber list read through
`useSyncExternalStore`, entirely outside TanStack Query and outside this handover's §13 allowed
scope — a global store (forbidden, HO-ACC-002 §4.3), and a deviation from this ADR as originally
written, not what is described above. The owner chose to bring the implementation back to the
approved shape rather than accept the deviation (HO-ADM-002 §1 Amended, 2026-09-28 (6)). See
`lib/branch.ts`, `lib/hooks/useActiveBranch.ts`, `components/branches/ActiveBranchPicker.tsx`
(§13), and `app/providers.tsx` (outside §13; the minimal addition described above — one call to
`registerQueryClient()`, needed only so `lib/branch.ts` has something to read the cache through).
`ActiveBranchPicker` is mounted in
`app/(staff)/layout.tsx` (that file's own scoped edit) rather than inside `StaffShell`'s header
markup — `components/common/staff-shell.tsx` is outside this handover's §13 allowed scope, so the
picker renders as the first element above every staff page's own content instead. Flagged for a
human reviewer as a visual placement decision, not a scope violation.

## ADR-FE-017 — The roster view is an in-repo `<table>`/list, never a scheduler library

**Decision.** `RosterWeekView` is a real `<table>` (row headers = staff, column headers = the
seven days) on desktop/tablet, and a day-grouped list below 768 px — both always mounted, CSS-only
toggled the same way `StaffShell`'s own nav rail already does, so neither depends on JS having run
to pick the right one. No calendar, scheduler or grid dependency is added (§10). Leave, roster
exceptions ("Extra shift"/"Shift cancelled") and "Outside opening hours" are rendered as **text**,
never colour alone (U-D2, UI-6).

**Why.** UI-1's 360 px floor rules out a horizontally-scrolling grid outright; a real `<table>`
with genuine header cells is what makes the desktop view screen-reader-navigable by row/column at
all, and a day list is the only shape that fits 360 px without truncating either staff names or
shift times.

**Status.** Built as specified. See `components/roster/RosterWeekView.tsx`.

## ADR-FE-018 — Content is rendered from `LimitedMarkup` by one in-repo parser, shared by preview and public

**Decision.** `components/content/LimitedMarkup.tsx` parses exactly HO-ADM-001 §8.8's markup
(paragraphs, `##`/`###` headings, `- ` bullets, `**bold**`, `[text](url)` with https/site-relative
urls only) directly into React elements. No `dangerouslySetInnerHTML`, no markdown package.
`ContentEditor`'s live preview and every public renderer (`/pages/[slug]`, `/faqs`, policy text)
use the **same** component, so what the editor sees is what the public gets (U-F2).

**Why.** A second markup-to-HTML path (even a "trusted" one) is exactly the kind of stored-XSS
surface ADR-052 (HO-ADM-001) exists to close off; parsing straight to React elements means there is
no HTML string at any point for an attacker's unexpected input to hide inside.

**Status.** Built as specified. See `components/content/LimitedMarkup.tsx`.

## ADR-FE-019 — Effective-dated records are Current/Scheduled/Past, never edited

**Decision.** `EffectiveDatedHistory` (prices, tax rates) and `PolicyDetail`'s version list both
group their rows into **Current / Scheduled / Past** by comparing `effectiveFrom`/`publishedOn`
against now, with **no edit or delete control anywhere** on any row (U-C1). The only action is
"Schedule a new …" (`ScheduleChangeDialog`) or "Publish new version", each of which disables a past
effective date/time client-side (U-C2) and states in its own confirm what stays unaffected
("Bookings and invoices made before {date} keep their price.").

**Why.** Mirrors HO-ADM-001 ADR-049's append-only price/rate model and ADR-052's append-only policy
versions — offering an edit the server will refuse is worse than not offering one at all (NFR-USA-4
in spirit: never invite an action that can only fail).

**Status.** Built as specified. See `components/admin/EffectiveDatedHistory.tsx`,
`components/admin/ScheduleChangeDialog.tsx`, `components/content/PolicyDetail.tsx`.

## ADR-FE-020 — URL-backed filter fields use local state as their display source of truth, resynced from the URL only when the URL matches none of this hook's own pending writes (corrected 2026-09-28, third pass)

**Decision (as built).** `AuditLogSearch`'s text inputs and the content list's `kind`/`status`
selects no longer read their displayed value straight from `useSearchParams()`.
`lib/hooks/useUrlFilterState.ts` (new) holds the displayed values as local `useState`, updated
**synchronously** on every keystroke/change; `lib/hooks/useFilterUrlUpdate.ts`'s own `pendingParams`
ref (the params the next URL is built from) is updated synchronously the same way.

Both hooks track **every query they have written to the router that the URL hasn't yet been
observed to reflect** — `pendingWrites`, a list, oldest write first — not only the most recent one.
When the URL matches _any_ entry in that list, it is unambiguously this hook's own write landing
(an intermediate one, a coalesced one, or the final one): that entry and everything written before
it are dropped, and neither `pendingParams` nor the displayed `values` are ever touched. Only a URL
that matches **nothing** in the list is genuinely external (Back, Forward) and triggers a resync,
which also clears the list — none of the writes it held apply once the URL has moved somewhere this
hook never asked for.

Free-text inputs (`AuditLogSearch`'s five text fields, not its two date fields, not the content
list's two selects) additionally debounce their URL write — `useUrlFilterState`'s own
`debouncedFields` parameter, `FILTER_WRITE_DEBOUNCE_MS` (300ms) after the last keystroke — so a
typing burst produces at most a handful of navigations rather than one per character; `flush(name)`
writes immediately, wired to each debounced field's own `onBlur` and Enter key
(`AuditLogSearch`'s new `onFilterCommit` prop) so the URL is never left stale once the person has
moved on from the field. The field's own **display** is never debounced — `setValue` updates local
state synchronously regardless — only the URL write is delayed.

A `popstate` listener was tried, in the second pass, as the "was this external" signal and rejected:
confirmed directly against this app, real browser, that the App Router's own `useSearchParams()`
can already reflect a Back navigation — and an effect watching it can already have re-run — before
the native `popstate` event reaches a `window` listener (Chromium's App Router client prefers the
Navigation API internally, whose `navigate` event fires ahead of the legacy `popstate` per spec).
Comparing query strings has no such ordering dependency — it only cares what the URL _is_ relative
to what the hook itself has written, never _why_ or _when_ it got there.

**Why.** A controlled field driven straight from `useSearchParams()` is only ever as fresh as the
last committed client-side navigation; `router.push`/`replace` is async, so any re-render between
an edit and that navigation's commit — an ordinary event in a real browser, not a contrived timing
— reset the field to the stale value, corrupting fast typing and silently losing quick successive
edits to different fields (TEST gate HIGH, second pass, 2026-09-28, U-G6). Local state removes the
round trip from the display path entirely; the URL itself stays the single source of truth for
sharing (deep links) and for browser history (Back/Forward stepping over whole edits, §8.3),
exactly as before.

**Status.** Corrected after TEST gate HIGH, third pass (still 2026-09-28) — the second pass's own
fix above (comparing the live URL against a _single_ "last write" string, `expectedQuery`) removed
the second pass's whole-string-corruption symptom but left a rarer, non-deterministic race: the
real History API commit can lag `router.push`/`replace` by several hundred ms, and `useSearchParams`
does not necessarily skip straight from one of this hook's writes to the next — it can pass through
an **intermediate, already-superseded** write of its own first. Compared against a single "last
write" string, that intermediate value never matched, so it was wrongly treated as external and
used to reset local state — silently losing whichever keystroke happened to land after that
intermediate value was written, roughly one dropped character in a handful of runs at ordinary
(150ms/key) typing speed. Reworked to the `pendingWrites`-list-plus-debounce shape described above;
`expectedQuery` no longer exists in either hook. See `lib/hooks/useFilterUrlUpdate.ts`,
`lib/hooks/useUrlFilterState.ts`, `components/audit/AuditLogSearch.tsx` (the new `onFilterCommit`
prop), `app/(staff)/staff/admin/audit-log/page.tsx`, `app/(staff)/staff/admin/content/page.tsx`.

## ADR-FE-149 … ADR-FE-158 — File Storage frontend slice (HO-FIL-002)

HO-FIL-002 §6/§9 owns this numeric range. All ten are recorded together here since they describe
one small, cohesive component set (`web/components/files/`) built in one pass — each `Status`
paragraph says what was actually built, not just what was proposed.

### ADR-FE-149 — Client-side validation mirrors `FileRules`; the server is still the authority

**Decision.** `components/files/fileRules.ts` mirrors HO-FIL-001 §8.3's `FileRules.cs` byte for
byte (same purposes, same allow-lists, same caps) and is consulted, per file, **before** any byte
is sent. A file that fails either check is never uploaded (C2) — the resulting queue row goes
straight to `status: 'error'` with no `XMLHttpRequest` ever constructed for it.

**Why.** A 60 MB file refused a minute into a slow upload, only to be told the same thing the
client could have said instantly, is a worse experience than an immediate refusal — but the server
re-checks everything regardless (magic bytes, size, scan), because the client check is a courtesy,
never the guarantee (HO-FIL-001 §8.2).

**Status.** Built as specified. `useFileUpload.addFiles` calls `validateFileClientSide` per file
before ever queuing it for upload; `tests/unit/files/file-rules.test.ts` proves every purpose's
allow-list/cap matches `FileRules.cs` field for field.

### ADR-FE-150 — One request per file, sequential, abortable, `XMLHttpRequest` for progress

**Decision.** `lib/hooks/useFileUpload.ts`'s queue runs **one upload in flight at a time**
(`runningRef`, a single-flight guard), moving to the next queued file only once the current one
settles (success, error, or a deliberate cancel). Each upload has its own `AbortController`.
Progress reporting is the one documented exception to "`lib/api/client.ts` is the only place that
calls `fetch`" (HO-CAT-002 §8.2 rule 2): `fetch`'s request body has no cross-browser progress
event, so `lib/api/files.ts`'s `uploadFile` constructs an `XMLHttpRequest` directly instead — the
one other network call site in this app, kept in `lib/api/` like every other area's endpoint
function, not in `components/` or `lib/hooks/`.

**Why.** A vet on clinic Wi-Fi uploading eight photos gets one legible progress bar and one named
failure this way, instead of eight parallel requests that all slow to a crawl together and fail as
an undifferentiated group.

**Status.** Built as specified. `tests/unit/files/use-file-upload.test.tsx`'s E3 case proves three
queued files upload strictly one at a time and that a middle failure does not block the third.

### ADR-FE-151 — Violation codes map to owner-facing sentences; `FileScanUnavailable` stays queued

**Decision.** `components/files/fileErrors.ts`'s `describeFileError` is the **only** place a
server violation code (or a transport failure, `NetworkFailure`) becomes visible text — the
server's own `errorMessage` is read only into `Error.message` for logs, never rendered
(`lib/api/files.ts`'s `FileUploadValidationError`). `FileScanUnavailable` reads as a service state
("try again shortly") and the row stays queued with **Retry**; every other code that describes
something wrong with the file itself offers no Retry (OQ-2 — retrying an oversized or infected file
cannot help). `FileRejected` (the scanner actually rejected the content) is removed from the queue
right after its one announcement, per OQ-1's recorded default, rather than left as a lingering row.

**Why.** NFR-USA-4 and A3/A4 — a person can act on "the largest allowed here is 5 MB", not on
`FileTooLarge`; and a scanner outage is the service's problem, never phrased as the uploader's
mistake.

**Status.** Built as specified. `tests/unit/files/file-errors.test.ts` checks every sentence in
§8.2's table verbatim; C4 (`file-uploader.test.tsx`/`use-file-upload.test.tsx`) proves
`FileScanUnavailable` keeps the row with Retry, and that Retry can actually succeed.

### ADR-FE-152 — Real `<input type="file">`, kept a sibling of the drop-zone `<button>`

**Decision.** `<FileUploader>`'s drop zone is a real, natively focusable `<button>`; a visually
hidden `<input type="file">` (`tabIndex={-1}`, `aria-hidden`) sits beside it, opened imperatively
by the button's `onClick`. Drag-and-drop handlers live on the same button.

~~As HO-FIL-002 §8.3 first describes it, the input is a DOM **descendant** of the button
("a button wrapping a visually hidden input").~~ **Built instead as siblings under one wrapping
`<div>`, corrected during implementation.** Nesting the input inside the button means the input's
own programmatic `.click()` (called from the button's handler) dispatches a real click event that
bubbles straight back up through the very button that triggered it — reproduced directly: this
re-entered the button's `onClick` a second time before the platform's own re-entrancy guard on
`.click()` stopped it going further, calling `inputRef.current.click()` twice for one keypress.
Siblings under a shared wrapper give an identical result for every user (one real control, keyboard
and screen-reader operable, drag-and-drop layered on top) without that bubbling hazard.

**Why.** Nothing reachable only by dragging (C1); a real `<input>` for the one moment it matters
(the OS file dialog), a real `<button>` for everything else (focus, `Enter`/`Space` activation,
drop target).

**Status.** Built as corrected. `tests/components/files/file-uploader.test.tsx`'s C1 case proves
the button is the only tab stop and that activating it opens the input's picker exactly once.

### ADR-FE-153 — One polite announcement per terminal event; never per progress tick

**Decision.** `<FileUploader>` renders one `aria-live="polite"`/`role="status"` region, visually
hidden, keyed on `announcement.id` (incremented on every call to `useFileUpload`'s internal
`announce()`) so two consecutive, identically-worded announcements are still each a fresh DOM node
for assistive tech. `<progress>` carries the visual/percentage progress and is never wired into
that region.

**Why.** HO-FIL-002 §8.3: a live region that narrates every progress tick is worse than no live
region at all; completion, failure and removal are the three events worth a person's attention.

**Status.** Built as specified. C6 (`file-uploader.test.tsx`) proves exactly one announcement per
terminal event; a dedicated case proves the region's text stays empty for the whole "uploading"
window even while `<progress>`'s value visibly changes.

### ADR-FE-154 — Expired signed URLs trigger one re-fetch, then a plain message

**Decision.** `<FileThumb>` and `<MediaViewer>` both take an optional `onRefresh` callback —
supplied by the host, which is the only thing that knows how to re-run its own read for a fresh
signed URL (this slice never talks to `GET /api/files/{id}/content` directly, HO-FIL-001 §14). Both
triggers named by §8.4 are wired for images and video: an `<img>`/`<video>` load failure calls
`onRefresh`, and so does rendering a file whose `urlExpiresOn` has already passed, checked
proactively in an effect rather than waiting for the resulting `403` to reach `onError`. Either way
`onRefresh` is called **at most once per `fileId`** — the guard is keyed on `fileId` alone and is
never reset by a prop update that keeps the same `fileId` (gate-fix 2026-09-28, SECURITY MEDIUM:
keying or resetting it on `url` instead meant a host that re-fetches and passes a new, still-bad URL
for the same file refreshed forever). A second failure, or no `onRefresh` at all, shows "This file
could not be loaded — refresh the page." and stops. An expired URL is never treated as a missing
file — the same plain message covers both, since a person cannot act differently on the distinction
anyway.

**Why.** ADR-FE-154 as specified — a diary or product page left open past the five-minute signed-URL
lifetime should recover once, quietly, rather than nag or loop.

**Status.** Built as specified, for images and video (the two element types that fire a real
`onerror` and the two this proactive check applies to). **Known limitation, not built**: an
`<iframe>`-embedded PDF past its URL's expiry has no equivalent load-failure signal this component
can observe — an expired PDF link renders whatever JSON body the `403` response actually sends
inside the frame, undetected, and nothing here proactively checks `urlExpiresOn` for the PDF branch
either. Flagged here rather than silently assumed covered; no consuming area uses a PDF through this
component yet (§16 doesn't exercise it), so this has not blocked anything so far.
`tests/components/files/file-thumb.test.tsx` and `media-viewer.test.tsx` (C8) prove the
one-refresh-then-message behaviour for images, including the case where a host-driven prop update
supplies a second, still-failing URL for the same file.

### ADR-FE-155 — Full-size images with lazy loading and fixed dimensions; no thumbnails exist

**Decision.** `<FileThumb>` renders the original image at whatever `width`/`height` the caller
passes (default 96×96), with `loading="lazy"` and `decoding="async"` — never a smaller derivative,
since HO-FIL-001 stores originals only (its own OQ-5).

**Why.** Explicit dimensions avoid layout shift; lazy/async loading is the cheapest available
mitigation for a screen that may show many of these at once (the boarding diary, §18 A6).

**Status.** Built as specified.

### ADR-FE-156 — The host owns the uploaded-file list state; the uploader is controlled

**Decision.** `<FileUploader>` keeps no memory of a file once it finishes uploading — its
`onUploaded` callback hands the HO-FIL-001 §12.4 view model to the host, and the host's own state
(usually the same array `<FileList>` renders) is what survives a re-render or a failed form submit
elsewhere on the page. The uploader's internal queue holds only in-flight and errored rows.

**Why.** HO-FIL-002 §7's "the host owns the file list state, so a failed form submit does not lose
the uploads" — the uploader cannot itself protect a value it doesn't hold past the moment of
success.

**Status.** Built as specified.

### ADR-FE-157 — No preview for a file the browser cannot safely display inline

**Decision.** `<MediaViewer>` renders inline only what §8.3 actually names: JPEG/PNG/WebP `<img>`,
MP4 `<video controls>`, and a PDF via an `<iframe>` (the browser's own viewer). Everything else
(DICOM, ZIP, CSV, …) becomes a plain download link instead — `<a href={file.url} download>`. The
`<iframe>`, the `<img>` and the download `<a>` — in both `<MediaViewer>` and `<FileThumb>` — all
carry `referrerPolicy="no-referrer"` (gate-fix 2026-09-28, SECURITY LOW, defence in depth): the
content-serving route already sends its own `Referrer-Policy: no-referrer` (HO-FIL-001 §8.6), so
this is redundant against a compliant server, but costs nothing and does not depend on that header
surviving an intermediary. `<video>` does not get one — `referrerpolicy` is not a defined content
attribute for that element in the HTML spec (confirmed: React's own type definitions have no such
member), so the response header is the only defence there, which is unchanged and still correct.
The PDF `<iframe>` deliberately has **no** `sandbox` attribute — Chromium refuses to render a PDF
inside a sandboxed frame at all, and the content route's own
`Content-Security-Policy: default-src 'none'; sandbox` already constrains the framed document
regardless of anything this element sets.

**Why.** Nothing here can safely guess how a browser will handle an arbitrary content type inline;
a download link is always correct, where a guessed inline embed sometimes is not.

**Status.** Built as specified.

### ADR-FE-158 — The viewer never renders HTML or SVG from the store, whatever the content type says

**Decision.** `<MediaViewer>`/`<FileThumb>` never use `dangerouslySetInnerHTML`, never fetch and
inline a response body as markup, and never render an `<img src>` whose content type is
`image/svg+xml` as anything other than what ADR-FE-157 already does with an unrecognised type (a
download link). `lib/files/fileRules.ts`'s `isRenderableImageContentType()` — not a bare
`contentType.startsWith('image/')` — is what both components call to decide whether to render an
`<img>` at all; it excludes `image/svg+xml` explicitly (gate-fix 2026-09-28, ARCHITECTURE MEDIUM).
SVG is also not in `fileRules.ts`'s upload allow-list for any purpose (HO-FIL-001 §8.3's own "no
SVG, it is script"), so in practice no purpose can even get one this far — but this ADR's own title
promises the **viewer** enforces it, "whatever the content type says", not only the upload allow-
list, so the exclusion is written explicitly rather than left as an accident of what the allow-list
happens to admit today.

**Why.** HO-FIL-001 ADR-318/§14 — stored content is data, never markup this app trusts and
executes, regardless of what a `Content-Type` header claims.

**Status.** Built as specified; enforced by both the upload allow-list (no purpose accepts
`image/svg+xml`) and, independently, `isRenderableImageContentType()`'s own explicit exclusion in
the viewer — belt and braces, not one guarantee standing in for the other.

---

## Numbering note — HO-NTF-002's block is ADR-FE-159…163 (2026-10-04)

`docs/handover/README.md`'s ADR table allocates `ADR-FE-019…023` to HO-NTF-002. By the time this
branch started (master `cffaa79`), `ADR-FE-014…020` were already used above by HO-ADM-002 (its block
shifted by one — see the earlier numbering note), `ADR-FE-039…148` are allocated to later
handovers, and `ADR-FE-149…158` are used by HO-FIL-002. To avoid duplicate headings, HO-NTF-002's
five ADRs are recorded as **`ADR-FE-159…163`** — the same five decisions its own §9 names
`ADR-FE-019…023`, in the same order (019 -> 159, 020 -> 160, 021 -> 161, 022 -> 162, 023 -> 163).
Code comments and test names in this slice use the 159…163 numbers. Flagged for the integrator to
reconcile the README table (out of this handover's file scope — `docs/handover/README.md` is not in
HO-NTF-002 §13).

## ADR-FE-159 … ADR-FE-163 — Notifications frontend slice (HO-NTF-002)

### ADR-FE-159 — The unread count polls every 60 s and on window focus, paused while the tab is hidden

**Decision.** `useUnreadCount()` (`lib/hooks/useInbox.ts`) is a TanStack Query with
`refetchInterval: 60_000`, `refetchIntervalInBackground: false` and `refetchOnWindowFocus: true`
(with `staleTime: 0` so the focus refetch is not swallowed by the app-wide 30 s stale window). No
WebSocket, no server-sent events, no push, no service worker.

**Why.** The inbox is not time-critical, and anything urgent also goes by SMS or email. Polling
needs no new infrastructure and survives proxies that break long-lived connections.

**Status.** Built as specified. `tests/components/inbox/inbox-badge.test.tsx` (U-A2) proves the
60 s refetch, the focus refetch, and that nothing is requested while the document is hidden.

### ADR-FE-160 — The email preview is server-rendered HTML in a `sandbox=""` iframe, in one component, lint-enforced

**Decision.** `components/notifications/SandboxedEmailPreview.tsx` renders the preview endpoint's
`htmlBody` as `<iframe sandbox="" srcDoc={htmlBody} title="Email preview">`. `sandbox` is the empty
string: no scripts, forms, popups, same-origin access or top navigation. It is the only component
in `web/` that may set `srcDoc`; `eslint.config.mjs` has a `no-restricted-syntax` rule (JSX
attribute, object property and assignment forms) that fails `npm run lint` anywhere else. The value
comes only from the preview response, never from state the user typed. It is the one HTML rendering
path in `web/`; the log and the inbox never render HTML.

**Why.** An administrator must see what the email looks like. The renderer HTML-encodes every text
node, and the sandbox makes the document inert even if that renderer had a bug.

**Status.** Built as specified. The mock preview HTML deliberately contains a `<script>` canary;
`tests/components/templates/sandboxed-email-preview.test.tsx` (U-C5, U-C6) and the e2e (E-X1 flow)
prove the attribute, the lint rule, and that the canary never runs.

### ADR-FE-161 — The unsubscribe token is read from the URL fragment, removed at once, and submitted only on a click

**Decision.** `/unsubscribe#token=…`: on mount the page reads `token` from `location.hash` and
immediately calls `history.replaceState` to `/unsubscribe`. Nothing is sent on load; the
**Unsubscribe** button sends `POST /api/notifications/unsubscribe`. No session is required.

**Why.** Fragments never reach servers, proxies or request logs. Mail scanners and link previewers
prefetch links, so a page that acted on load would unsubscribe people who never clicked.

**Status.** Built as specified (U-D1…U-D3).

### ADR-FE-162 — Message bodies in the inbox and the log are plain text; in-app links only when site-relative

**Decision.** Inbox titles/bodies and log subjects/titles/bodies/provider responses are rendered as
React text children inside `whitespace-pre-wrap` elements — never markup, never `LimitedMarkup`,
never HTML. A `linkPath` becomes a link only when `lib/routing/isSafeInternalPath()` accepts it
(single leading `/`, no `//`, no `/\`, no backslash or control character anywhere).

**Why.** Bodies carry customer-supplied values (a cat's name) and administrator-edited template
text; plain text removes the whole injection class, and the path check removes open redirects.

**Status.** Built as specified (U-A4, U-A5, U-E5).

### ADR-FE-163 — The client never substitutes placeholders or renders templates; it asks the server to preview

**Decision.** The template editor inserts `{{name}}` tokens and nothing else. The preview is a
`useQuery` (`lib/hooks/useNotificationTemplates.ts`) on the unsaved contents and sample values,
enabled after a 600 ms debounce (`useDebouncedValue`), with `placeholderData: keepPreviousData` and
the query's `AbortSignal` passed to `client.ts`, so a newer preview cancels an older in-flight one.
Sample values are sent as `sampleValues` and never saved. A preview `400` is shown verbatim in the
panel and on the offending field (mapped from `Contents[i].Body` etc.) without blocking typing.

**Why.** One renderer — the one that sends — so the preview cannot lie. A client-side copy of the
rules would drift. The MSW preview handler contains a small renderer only because it is the mock
of that server; app code never imports it.

**Status.** Built as specified (U-C2, U-C3).

---

## ADR-FE-024 … ADR-FE-028 — Appointments frontend slice (HO-APT-002)

### ADR-FE-024 — The booking flow is one route with a stepper; choices live in URL params, the hold only in memory

**Decision.** `/book` is a single page. Cat, category, service, clinic, date and vet are URL search
params (`useFilterUrlUpdate`), so a reminder link (`/book?catId&category`) deep-links into it and a
reload keeps the choices. The current hold (`appointmentId`, `holdExpiresOn`) is component state of
`BookingFlow` — never the URL, never storage. A reload loses it; the server expires it. A single cat or
single clinic is not written to the URL, it is derived, so Back never bounces off a skipped step.

**Why.** A reload must never pretend a hold survived. **Status.** Built. Leaving releases the hold
(`DELETE`) on Back (awaited before the slot list is asked for again, or the customer's own hold would
still count as busy), on a confirmed in-app navigation (`LeaveHoldGuard`), on unmount and on `pagehide`
(`keepalive`) — OQ-3, best effort.

### ADR-FE-025 — The hold countdown runs on the server clock

**Decision.** `client.ts` samples every response's `Date` header; `getServerClockOffsetMs()` is the
median of the last five, `useServerNow()` is `Date.now()` plus it. `HoldCountdown` counts to
`holdExpiresOn` on that clock, is a `role="timer"`, and announces (separate polite region) only at 5:00,
1:00 and 0:00 — thresholds already behind its starting time are skipped, never announced late. At 0 it
calls `onExpire` once; the flow drops the hold, refetches the slots and says *"Your hold has ended —
please choose a time again."*

**Status.** Built. **Open item for the integrator:** a cross-origin browser only exposes `Date` when the
API sends `Access-Control-Expose-Headers: Date`; `HostPipelineExtensions` does not. Without it the
offset stays 0 and the countdown uses the browser clock (the mock sends the header).

### ADR-FE-026 — The calendar is an in-repo CSS layout; every drag has a Move… dialog

**Decision.** `CalendarGrid` positions blocks with plain CSS on the payload's `gridMinutes`
(`components/calendar/layout.ts` is geometry only). Pointer-event drag (`useCalendarDrag`, no library)
snaps to the grid and the column under the pointer; a drop opens the same confirmation as the **Move…**
dialog, then *"Moving…"* shows at the target until the server answers; a `400` snaps back with the
server's reason in a toast and a live region, a `409` says the appointment changed and refetches. Below
768 px the grid is replaced by `CalendarAgenda`. The dialog takes a typed time on the grid — the
appointment payloads carry no service id, so the slots endpoint cannot be asked (HO-APT-002 §1 Amended).

**Status.** Built. A click that ends a drag is swallowed (`consumeClick`), or the column's
"new appointment" click would fire on a same-column drop.

### ADR-FE-027 — Polling replaces data, never an open form, dialog or drag

**Decision.** Front desk and calendar poll every 30 s. Dialogs and forms own their state (a poll cannot
reset them: U-D2); `CalendarGrid` reports a drag in progress and the container renders the data it had
when the drag began (`useFrozenWhile`), applying the fresh poll after the drop (U-E3).

### ADR-FE-028 — Actions come from `allowedTransitions` and `canChange`, never from a status

**Decision.** Status actions are rendered only from the API's `allowedTransitions`; customer change
actions only from `canChange`. The day list and the queue carry no `allowedTransitions`, so the Front
desk reads each open row's detail (polled with the list) and shows actions only from that answer — the
alternative, a status-to-buttons table, is exactly the drift this ADR forbids. Moves are offered on every
block and refused by the server with its reason. **Status.** Built; recommend the API add
`allowedTransitions` to the day-list and queue entries (one request fewer per row).

---

## ADR-FE-029 … ADR-FE-038 — Shop catalogue frontend slice (HO-SHP-002)

### ADR-FE-029 — A fourth route group, `(shop)`: public, own storefront shell, no guard

**Decision.** `app/(shop)/**` is the public storefront group with its own shell (header with search
and category navigation, banners, maintenance gate, footer) and the warm customer layer
(`data-theme="customer"`). It has **no session guard**: it reads `['session','customer']` only to show
_Sign in_ or the customer's first name. Personal actions send a guest to `/sign-in?next=`. `/` now
sends a signed-out visitor to `/shop` (OQ-1); a signed-in customer or staff member keeps HO-ACC-002's
owner-decided destinations.

**Why.** `(auth)` is a minimal shell, `(portal)` requires a session, and guests must browse (SRS §2.3).

**Status.** Built as specified. Deviation recorded in HO-SHP-002 §1 Amended (the `/` redirect keeps the
signed-in destinations).

### ADR-FE-030 — Storefront pages are Server Components fed by `getPublic`

**Decision.** Catalogue, product, countries and home are Server Components. Their data comes from
public endpoints through `client.ts`'s server-only `getPublic` (no cookies, no identity headers,
`revalidate: 60`; status read `revalidate: 10`). Metadata and `alternates.canonical` come from the
slug. Interactive islands (filters, sort, search box, variant selector, gallery, buy box) sit on top
and never block the server-rendered page. `client.ts` remains the only file that calls `fetch`. In
mock mode `instrumentation.ts` starts `msw/node` and publishes its intercepting `fetch` as
`globalThis.__mswFetch`, which `getPublic` uses when present — Next re-wraps `fetch` as each route
compiles in `next dev`, and a wrapper created after the first request stops reaching MSW's
interceptor (reproduced; only the first compiled route was served from the mocks).

**Why.** NFR-PRF-1 (LCP on 4G), the slug is the SEO identity (FR-SHP-2), and the single-`fetch` rule.

**Status.** Built as specified, plus the `__mswFetch` seam (mock mode only; inert in production).
`NEXT_PUBLIC_USE_MOCKS` is used instead of the handover's `NEXT_PUBLIC_API_MOCKING`, matching the repo.
The catalogue is deliberately not wrapped in `<Suspense>`: a streamed boundary needs JavaScript to
reveal its content, which would break the no-JavaScript render.

### ADR-FE-031 — Catalogue state is URL state, through one parser

**Decision.** Search, every filter, sort and page live in URL params; `lib/shop/catalogueParams.ts`
reads and writes them (unknown params dropped, repeated values kept in order). A filter change resets
the page; a sort change keeps every filter; filters and sort use `router.replace`, pages use
`router.push`. The server is the only filter engine — nothing in the UI filters, sorts or counts. The
controls show the shopper's last choice at once (`pending`, ADR-FE-020's reasoning for checkboxes)
because a router navigation is asynchronous.

**Why.** US-SHP-04 AC2, shareable and reload-safe; NFR-PRF-2 is a server budget.

**Status.** Built as specified. HO-ADM-002's `useUrlFilterState`/`useFilterUrlUpdate` are used for the
staff list (single-valued fields); they cannot model `brand=A&brand=B` or the API's `pageNumber`, so the
storefront uses `useCatalogueNavigation`. Note: in `next dev`, pressing Back immediately after a hard
reload returns to the reloaded page even for plain links (a framework behaviour); the E-A2 spec
exercises Back from the reloaded page's next navigation.

### ADR-FE-032 — Availability, expiry, purchasability, eligible cats and the checklist come only from API fields

**Decision.** Availability, the best-before notice, the prescription gate and the publish checklist are
rendered from the API's fields. "No stock recorded" (a `null` or absent stock copy) is shown as such,
never as 0; a real zero reads "0 sellable". The UI handles every SKU being `OutOfStock` (HO-SHP-001's
fail-closed rule until HO-IMP-001): out of stock is stated in text, the alert is offered, and nothing
implies a purchase.

**Why.** HO-SHP-001 ADR-082/084; HO-ACC-002 ADR-FE-012 "a number is a claim".

**Status.** Built as specified.

### ADR-FE-033 — The prescription gate explains itself and shows nothing clinical

**Decision.** A prescription diet is visible to everyone. `PrescriptionGate` shows the OQ-6 copy, a
sign-in link to a guest, _None of your cats has a current authorisation for this food._ plus **Book a
consultation** → `/book?category=Consultation`, or each eligible cat's name and authorisation end date.
`PrescriptionCatPicker` is built for HO-ORD-002 and is not rendered in this slice (there is no cart).

**Why.** US-SHP-10 AC1; BR-2.

**Status.** Built as specified.

### ADR-FE-034 — The product editor is route-based sections under one layout

**Decision.** `/staff/shop/products/[productId]/**` sections (details, food, variants, images, kit,
related, translations) share a layout carrying the status, the server's publish checklist and the
status actions. Each section saves on its own with `expectedVersion` and keeps its values on a `400`
or `409` (the `409` shows a **Reload** button). Publish is never disabled: the server's refusal is
shown and focus moves to the checklist.

**Why.** HO-SHP-001 ADR-095; HO-ACC-002 "submit is never disabled to express invalidity".

**Status.** Built as specified. The staff read now returns food, images, health focus and the SKU
label (HO-SHP-001-FU1, backlog S1), so the forms load them; `stock` per SKU is still deferred.

### ADR-FE-035 — Images: no upload, a stored file id plus metadata, plain `<img>`

**Decision.** There is no upload path. `ImagesEditor` takes the stored file's id (the merged API's
`fileId`) with alt text (required), scope and primary flag, with the honest note _Image upload isn't
available yet — enter the stored file's key._ Images render with a plain `<img>` (no `next/image` remote
configuration; `next.config.ts` is out of scope). `url` is always `null` today (backlog SHP row 2), so the
in-repo placeholder renders with the alt text the API sent.

**Why.** HO-SHP-001 ADR-091; NFR-SEC-5; UI-6.

**Status.** Built as specified (the field is a file id, not the handover's storage key — §1 Amended).

### ADR-FE-036 — Money and tax display only through `lib/format`

**Decision.** `formatPriceRange` ("From" when variants differ), `taxLabel` (`incl. VAT` / `+ VAT`,
OQ-5), `formatRating`, `formatBestBefore`, `countryFlag` and `parseMoneyForCompare` (price-filter
min ≤ max only) live in `lib/format/shop.ts`; amounts stay strings; no arithmetic on amounts.

**Why.** C-6; SRS Q-5 open.

**Status.** Built as specified.

### ADR-FE-037 — Product-content language is `?lang=`

**Decision.** `?lang=` is passed as `locale`; the switcher appears only when `GET /api/shop/locales`
lists more than one language; every language-varying field carries `lang` from `fieldLocales`; UI labels
stay English. Product-page metadata keeps one canonical URL per product.

**Why.** FR-SHP-15 / US-SHP-14; WCAG 3.1.2.

**Status.** Built as specified.

### ADR-FE-038 — Personal storefront actions require sign-in and return to the same variant

**Decision.** Save, stock alert and review send a guest to `/sign-in?next=` with the product URL and
`?sku=`; `/shop` joins the sign-in allow-list (`/portal/**` already covers `/portal/reviews/new`). The
alert confirmation shows the server's `deliverability`: when `willNotify` is false it says so (OQ-7) and
links to notification settings. Review title/body, names, brands, ingredient list and storage
instructions are rendered as text, never HTML; only description, feeding guide and an origin's sourcing
notes go through `LimitedMarkup`.

**Why.** HO-SHP-001 F33 / OQ-2; backlog SHP row 22.

**Status.** Built as specified.
