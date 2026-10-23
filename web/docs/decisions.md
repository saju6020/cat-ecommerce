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

**Narrowed by ADR-370 (2026-10-18):** product photos now have resized WebP copies (480 px for cards, cart and orders; 1,200 px for the product page). The API returns their URLs, direct Cloud Storage signed URLs in production, so the storefront needs no change: `resolveFileUrl` passes absolute `https://` URLs through. Other purposes still have no thumbnails.

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

### ADR-FE-049 — The consultation is one route; history, diagnostics and summaries open over it

**Decision.** The consultation is **one route**; history, diagnostics detail and summaries open in a **drawer or dialog over it**, never a navigation; the note form stays mounted throughout

**Why.** US-CLN-13 "without losing my draft" is guaranteed by structure, not by saving first

**Status.** Built as specified, with one exception: once a visit is finalised the record links to its own printable summary route (`/staff/visits/[visitId]/summary`), which is a navigation. No note form is mounted in that state, so the draft the ADR protects is not at risk.

### ADR-FE-050 — Auto-save goes to the server working copy; nothing is written to browser storage

**Decision.** **Auto-save to the server working copy** (`PUT …/draft`): 3 s after typing stops, at most every 20 s while typing, and on `visibilitychange`/route change with `fetch(…, { keepalive: true })`. On load, a newer draft is **restored automatically** with a notice and **Revert to saved note**. **Nothing is written to browser storage.** Explicit **Save note** writes the record. Under the idle lock auto-save pauses (requests fail) and resumes on the next edit after unlock — never a replay

**Why.** US-CLN-12 AC2 on shared clinic PCs; HO-ACC-002's idle-lock rule

**Status.** Built as specified.

### ADR-FE-051 — Actions come only from allowedActions; a finalised visit is a record, not a disabled form

**Decision.** **Actions come only from `allowedActions`**; a finalised visit renders as a read-only record component (text, not disabled inputs) with addenda beneath

**Why.** A disabled form invites "enable it in dev tools"; the server is the authority (US-CLN-17, US-CLN-22)

**Status.** Built as specified, with one clarification: the open visit also renders as read-only text for a role without `editNote`, and a Vet may still enter diagnostic results on a finalised visit (results can be added after finalising), which `allowedActions` does not list.

### ADR-FE-052 — The vitals confirm flow is server-driven

**Decision.** **Vitals confirm flow is server-driven**: the client shows the server's plausible ranges early (from `vital-sign-rules`), but saves by posting; a `400 VitalsOutsidePlausibleRange` opens a confirm listing each value with its range; *Confirm and save* re-posts with `confirmedOutOfRange`

**Why.** US-CLN-11 AC2; the warning that counts is the one the server recorded

**Status.** Built as specified.

### ADR-FE-053 — MeasurementInput and formatMeasurement carry every unit

**Decision.** **`MeasurementInput`** — numeric input with a permanent visible unit suffix (part of the accessible name: *"Weight, kilograms"*), precision from the rules, no spinner; **`formatMeasurement`** for display

**Why.** UI-8 in one place

**Status.** Built as specified.

### ADR-FE-054 — The signature pad is an in-repo canvas capturing strokes as data

**Decision.** **Signature pad is an in-repo canvas** with pointer events, capturing strokes as normalised `[x, y]` points (4 dp) sent as data; *Clear* and *Undo stroke*; the non-drawing alternative is **upload a signed paper form**

**Why.** ADR-132 (strokes, not images); no library; UI-6 needs an alternative for a pointer-only control

**Status.** Built as specified.

### ADR-FE-055 — FileUploadField pre-checks and uploads, then attaches by fileId

**Decision.** **`FileUploadField`** pre-checks type and size from a map mirroring HO-EMR-001 `FileRules`, uploads with progress, then posts the attach command with `fileId`; the server's refusal (`FileContentMismatch`, `FileRejected`, `FileScanUnavailable`) is shown as the message. Files are displayed only from signed URLs in payloads

**Why.** NFR-SEC-5 is the server's; the client only saves a round trip

**Status.** Built with one change: `FileUploadField` is a thin wrapper over the shared HO-FIL-002 `FileUploader` (the one upload control) instead of a second implementation, so the pre-check map is that module's `FileRules` mirror and the scanner-unavailable sentence is its wording.

### ADR-FE-056 — History loads the summary first, sections on demand

**Decision.** **History loads summary first, sections on demand**: the drawer renders the summary (active problems, last visits, last vitals, weight trend, pending labs) from one call and fetches a section's first page only when its tab opens; "Load more" pages (no virtual-list library)

**Why.** NFR-PRF-4 without a dependency

**Status.** Built as specified.

### ADR-FE-057 — Printing uses a print stylesheet; the PDF is the server's

**Decision.** **Printing** uses a print stylesheet on `/staff/visits/[visitId]/summary` for the browser path and the server's PDF for download and the portal; the client never generates a PDF

**Why.** One source of truth for the summary (ADR-130)

**Status.** Built with one change: the JSON summary carries only `prescriptionsAvailable`, so the prescription-label sentence is a client constant matching the server's message; staff PDF download is fetched as a blob (sends `X-Branch-Id`) and saved via an object URL, the owner's portal uses a plain link.

### ADR-FE-058 — Clinical text is plain text

**Decision.** **Clinical text is plain text** — rendered with `white-space: pre-wrap`, never parsed as markdown or HTML; consent-form bodies (server `LimitedMarkup`) use HO-ADM-002's renderer

**Why.** Notes are untrusted input displayed to other staff and owners

**Status.** Built as specified.

## ADR-FE-079 … ADR-FE-086 — Billing frontend slice (HO-BIL-002)

### ADR-FE-079 — Billing is a route-based section with the customer account as its hub

**Decision.** Billing is a route-based section `(staff)/staff/billing/**` with the **customer billing account** as its hub; the billing profile lives in the admin section (ADR-FE-013); the portal gets `/portal/billing/**`

**Why.** Reception works customer-first; deep links from the inbox and other areas need stable routes

**Status.** Built as specified.

### ADR-FE-080 — Money stays a string end to end; the client does no money arithmetic

**Decision.** **Money stays a string end to end; the client does no money arithmetic or comparison**; composer totals come from the server's preview endpoint, labelled *"Estimate at today's tax rates"* until issue; a lint rule enforces it

**Why.** HO-BIL-001 ADR-181/194 — one calculator; JavaScript `number` cannot represent 0.10 exactly

**Status.** Built as specified.

### ADR-FE-081 — Every money-moving form sends one Idempotency-Key per mount

**Decision.** **Every money-moving form sends one `Idempotency-Key` per mount**, reused on retry (network error, `5xx`, double click) and renewed only after success; submit buttons also disable while in flight

**Why.** HO-BIL-001 ADR-187; a lost response followed by a retry must not record twice

**Status.** Built as specified.

### ADR-FE-082 — Payment-method UI is rendered from the server flags

**Decision.** **Payment-method UI is rendered from `GET /api/billing/payment-methods` flags** (`requiresReference` + `referenceLabel`, `requiresCollector`, `countsInTill`, `allowedRefundMethods`); no component switches on a method value; `isOnline` methods (none in v1) are filtered out of staff recording

**Why.** Phase 5 adds a method without a form rewrite (HO-BIL-001 ADR-182)

**Status.** Built as specified.

### ADR-FE-083 — Actions only from the server allowedActions

**Decision.** **Actions only from the server's `allowedActions`**; there is no edit affordance on issued documents or payments; corrections are presented as *Reverse*, *Credit note*, *Corrected re-issue* and *Refund*, each with a UI-3 confirm

**Why.** BR-11; the state machines are the server's

**Status.** Built as specified.

### ADR-FE-084 — C-5 client guard on reference, notes and refund reference

**Decision.** **C-5 client guard**: reference, notes and refund reference inputs run the same Luhn check as the server on change and on submit; a match blocks submit, clears nothing else, and shows *"This looks like a card number…"*; the value is never sent, logged or put in the URL

**Why.** The only way to keep a PAN out of request logs is not to send it

**Status.** Built as specified.

### ADR-FE-085 — Documents: server PDF; receipts print from a dedicated route

**Decision.** **Documents**: invoices and credit notes download as the server's PDF; receipts print from a dedicated route with a print stylesheet (A4 and an 80 mm width variant) built from the receipt endpoint's data, text only

**Why.** One legal rendering (the server's); thermal ESC/POS is POS's (HO-ORD-002)

**Status.** Built as specified.

### ADR-FE-086 — Split tender is a loop, not a multi-row form

**Decision.** **Split tender is a loop, not a multi-row form**: after each accepted payment the form shows the server's new outstanding and offers *"Take another payment"* pre-filled with it, until `0.00`; pending-approval results end the loop with an explanation

**Why.** Each tender is its own record with its own receipt and approval rule (US-BIL-04, US-BIL-11)

**Status.** Built as specified.

---

## ADR-FE-039 … ADR-FE-048 — Imports, batches and inventory frontend slice (HO-IMP-002)

Recorded from `docs/handover/HO-IMP-002.md` §9, word for word. Numbering note: the `ADR-FE-039…148`
block above is allocated to this handover's series (`ADR-FE-039…048` are HO-IMP-002's); they are
recorded in `web/docs/decisions.md` (the file every earlier frontend slice uses) because `.ai/**`
is forbidden scope for a frontend handover (HO-IMP-002 §14) and carries only the backend ADRs.

### ADR-FE-039

**Decision.** Two route-based sections — `/staff/procurement/**` (buying) and `/staff/inventory/**` (holding) — each with its own nav layout; a person with only `Stock.View` sees Inventory and not Procurement, **as presentation only**

**Why.** They are different jobs and different roles; every page still renders the server's `403`

### ADR-FE-040

**Decision.** **The server owns every stock decision.** FEFO order, `isExpired` / `daysToExpiry`, sellable and held quantities, `allowedActions`, `requiresApproval`, variance figures and suggested reorder quantities are rendered from the payload; the UI computes none of them and never re-sorts a FEFO plan

**Why.** An independent client rule silently drifts from BR-3/BR-4 — the two rules with no override

### ADR-FE-041

**Decision.** **Quantities, money and unit costs are strings end to end**, validated by regex (`^\d+(\.\d{1,3})?$` / `(\.\d{1,4})?`) and never passed through `Number`; totals shown come from the server

**Why.** Float arithmetic on stock and money (C-6) is how a count sheet ends up off by 0.0000001

### ADR-FE-042

**Decision.** **Barcode input is a keyboard-wedge `ScanField`**: keystrokes arriving faster than 30 ms apart and ending with Enter are treated as a scan, looked up server-side, and announced in a live region; typing and picking from the SKU picker always remain available

**Why.** HW-2; no camera permission, no scanning library, and a scanner that is just a keyboard cannot break the form

### ADR-FE-043

**Decision.** The **goods-receipt form autosaves a draft to `sessionStorage`** per staff member, restores it with an explicit *"Restore the receipt you were entering?"*, and clears it on post

**Why.** Fifty lines typed beside a pallet must survive a reload; nothing in the draft is personal data

### ADR-FE-044

**Decision.** The **count sheet saves each line to the server as it is counted** and never shows book quantities while the count is open

**Why.** Counts are long and shared; blind counting is the control (HO-IMP-001 ADR-108)

### ADR-FE-045

**Decision.** Alerts are a **nav badge polled every 60 s** plus an alerts page; nothing else polls, and a poll never resets an open form or dialog

**Why.** Expiry and low stock are the two things nobody should have to go looking for (UI-5's spirit, without a live screen)

### ADR-FE-046

**Decision.** The **recall console is a three-step, irreversible-by-design flow**: declare (type the lot number), trace (read, export), notify (explicit, with the recipient count). Notification is never automatic and never fires from the declare step

**Why.** The most consequential action in the product; AC-10

### ADR-FE-047

**Decision.** **Expiry and unusable states are text first**, with icons that carry their own labels; an unusable row always says *why* (*"Expired"*, *"Quarantined"*, *"Recalled — held"*, *"In quarantine location"*) and never offers an action the server would refuse

**Why.** UI-6; US-IMP-09's absoluteness has to be visible, not inferred from a red row

### ADR-FE-048

**Decision.** **Approval-gated actions render from the server's state** (`requiresApproval`, `status`, `allowedActions`), and the approval screens are one queue shared by adjustments and counts

**Why.** ADR-107's separation of requester and approver must be obvious, not discovered on submit

## ADR-FE-164 — Cat photos: upload, then attach by file id (HO-CAT-002 follow-up)

### ADR-FE-164

**Decision.** The staff cat photos page does **not** attach on upload: `FileUploadField` holds the uploaded file as a chip (with Remove), and an explicit **Add photo** posts `{ fileId, isPrimary }`. A refused attach (`PhotoFileNotEligible`, photo limit) is shown inline and keeps the chip so the person can retry or remove and re-upload. The first photo of a cat is sent as primary; the page hides the uploader at 10 photos.

**Why.** The attach call has its own refusals (30-minute window, one file per cat, photo cap) that a person must see next to the file that caused them; auto-attaching would hide a chosen file behind a toast. The browser never supplies a storage key, type, size or name (HO-FIL-001 §1 Amended).

## ADR-FE-170 … ADR-FE-172 — Branch calendar view (HO-ADM-002-FU1)

ADR-FE-172 is held for amendments.

### ADR-FE-170

**Decision.** **The branch calendar's open/closed state and times come only from `GET …/calendar`.** The closure list and `weeklyHours` only label the reason. Where they disagree with the server (for example two reads taken at different moments), **the server's intervals win**, and a closed day with no matching reason reads plain *"Closed"*. If the calendar request fails, nothing is drawn from the other two reads

**Why.** `BranchCalendar` is the single conversion point (HO-ADM-001 ADR-045, §20.7). A client recomputation would drift on DST, part-day subtraction and cancelled closures

### ADR-FE-171

**Decision.** **The month view is an in-repo `<table role="grid">` with a roving tabindex** (WAI-ARIA APG grid keys: arrows, Home/End, PageUp/PageDown). It becomes an `<ol>` day list below 768 px. The month is the `?month=yyyy-MM` URL param, defaulting to the current month in branch time

**Why.** Extends ADR-FE-017 (no calendar library, no sideways scroll). Keeps month links shareable and safe with the Back button. One Tab stop keeps a 31-cell grid usable from the keyboard

## ADR-FE-059 … ADR-FE-068 — Pharmacy and prescriptions frontend slice (HO-PHM-002)

Recorded from `docs/handover/HO-PHM-002.md` §9. Where the build departs from the handover's text, the
departure is in that file's §1 *Amended* rows and named below.

### ADR-FE-059

**Decision.** Prescribing lives **under the cat** (`/staff/cats/[catId]/prescriptions/**`) so the clinical header — allergies included — is always on screen, and the composer is an **embeddable component** (`PrescriptionComposer`, with `DietAuthorisationForm`, `VisitPrescriptionsList` and `WitnessDialog`) that takes the cat, an open visit (or none, for a renewal) and a callback, and never navigates on its own

**Why.** UI-7 while prescribing; the consultation screen (HO-EMR-002) must not need a second composer. Build note: HO-EMR-002 now exists, but its workspace was not edited by this slice (out of scope, §14) — it reaches the composer through the cat's Prescriptions tab and the visit picker until it embeds the component

### ADR-FE-060

**Decision.** **The safety panel renders the server's assessment only.** A debounced check (400 ms) runs on every clinically relevant change; acknowledgements are stored **against the fingerprint** and cleared when it changes; submit sends that fingerprint; `400 PrescriptionWarningsNotAcknowledged` / `409 PrescriptionWarningsChanged` replace the panel with the assessment the body carries and say *"Something changed — please review the warnings again."* A repository scan (`clinical-logic-scan.test.ts`) fails the build on dose maths, allergy or interaction matching, batch ordering or expiry comparison in this slice

**Why.** The client must never decide safety, and a tick against yesterday's warnings is not consent

### ADR-FE-061

**Decision.** **Units are part of the value.** `Quantity` (and `SignedQuantity`) is the only way a dose, quantity, weight, strength or range is rendered — one text run, a non-breaking space between number and unit, `data-clinical-value` on the element. Dose inputs pair a number with a **unit select that starts empty** and offers only the units the medicine's form allows; `lib/format/units.ts` is the one table of unit labels. Deviation: no `aria-label` spelling the unit on the element (a label on a plain `<span>` has no role to name, which axe reports)

**Why.** UI-8 — a bare `0.3` on a dosing screen is a defect

### ADR-FE-062

**Decision.** **The dispense screen follows the server's plan.** Suggested batches are preselected; expired and non-dispensable batches are listed **disabled with the reason as text**; choosing away from the suggestion reveals a required reason; `canDispense: false` (only expired stock, an ended validity, no supplies) renders a blocking notice and **no Dispense button, no batch control, in the DOM**; a scanned lot selects the matching batch or says it is not in the plan. Deviation: batches are chosen with checkboxes plus a quantity box (a supply can be split, which one radio cannot express); the group is labelled. The only arithmetic is the exact decimal sum behind *"8 of 10 ml selected"* (`lib/format/pharmacy.ts`, integers in ten-thousandths)

**Why.** BR-3 and BR-4 as the user sees them; the client has no expiry logic to get wrong

### ADR-FE-063

**Decision.** **The witness step is a modal** on `components/ui/dialog.tsx`: the witness types **their own** identifier and password on the same device; both fields `autocomplete="off"`; the credentials live in the dialog's local state only and are cleared on submit, cancel, close, unmount and on every refusal (password first, focus back to the identifier); failures show the server's one generic message. Credential-bearing calls (dispense, reverse, register correction) are **plain functions, not TanStack mutations**, so a password never sits in the mutation cache. Owner decision 2026-10-10: the witness must be a vet or nurse and not the dispenser; the dialog says so

**Why.** HO-PHM-001 ADR-146; a password manager filling the dispenser's credentials, or a cache retaining a password, would defeat the witness

### ADR-FE-064

**Decision.** **Labels print from the frozen snapshot** through a dedicated print route; the print stylesheet is generated from two constants (`LABEL_WIDTH`, `LABEL_HEIGHT`, default 62 × 100 mm, OQ-1) and embedded with the label, as the receipt's is, rather than in a new CSS file; **Print** calls `window.print()` then records ONE `POST …/label-prints` per click — never per render

**Why.** HO-CAT-002's cage-card precedent; a reprint must be the same label and must be counted

### ADR-FE-065

**Decision.** **Append-only UI.** Register entries, dispensings and issued prescriptions have **no edit control**; corrections, reversals and renewals are separate routes/dialogs that create something new and say so. The register's *Add correction* is for vets only; an administrator may reverse a dispensing but cannot correct the register (owner decision 2026-10-10). A reversal's confirmation says the stock returns to **quarantine**, as the backend does

**Why.** C-2, D-5 — the UI must not suggest history can change

### ADR-FE-066

**Decision.** **Portal repeat requests** render only from `repeat.canRequest`; the refusal text is the server's, verbatim, with **Book a consultation** (`/book?catId=`) when the code is an ended validity or no repeats left; an open request shows **Withdraw**; `?action=repeat` opens the dialog on load only when a repeat is allowed. The list endpoints are plain arrays, so the cat filter and the paging of *Past* (20 a page) are applied on the client

**Why.** US-PHM-06 AC2 "told why"

### ADR-FE-067

**Decision.** **Formulary editor** edits the whole medicine in one form with `expectedVersion`; the controlled flag and activation are **separate confirmed actions**; interactions are a repeatable row group with a medicine picker **or** a term; a `409` keeps the typed values and offers *Reload*

**Why.** Mirrors HO-PHM-001 ADR-138's separate command; a controlled-flag change must be deliberate

### ADR-FE-068

**Decision.** The allergy-matching limitation is **stated on the safety panel**: *"Allergy checks compare recorded allergies with this medicine's ingredients and classes. A misspelt allergy may not be matched."*

**Why.** HO-PHM-001 ADR-140 — a check that can miss must say so where it is relied on

## ADR-FE-089 … ADR-FE-098 — Cart, checkout and orders (HO-ORD-002)

### ADR-FE-089

**Decision.** **The cart is server state.** One `['cart']` query serves guests and customers; the guest identity is the API's `HttpOnly` cookie, which the client never reads. When the cart says `guestCartPending`, `useCart` calls the merge endpoint once and refetches.

**Why.** HO-ACC-002 forbids browser-stored drafts and tokens; a client-side cart would have to be merged in the browser, which cannot see the guest cookie.

### ADR-FE-090

**Decision.** **Checkout is one route with a stepper**, every choice in URL search params. **The quote is the single source of truth** for prices, charges, slots, payment availability and whether the order can be placed.

**Why.** NFR-USA-5 across reloads and the Back button; a client that computes anything about money or availability drifts from the server.

### ADR-FE-091

**Decision.** **Acknowledgement is bound to a fingerprint.** Place order stays disabled until the customer ticks the acknowledgement for *this* quote; any new fingerprint (poll, option change, `409 CheckoutQuoteChanged`) clears the tick and re-opens the panel. The tick is also disabled while the quote for the latest choices is still settling.

**Why.** FR-ORD-3: acknowledging a quote they never saw is the failure this prevents.

### ADR-FE-092

**Decision.** **No arithmetic on money in the browser and no payment-instrument UI.** Amounts are server strings rendered by `formatMoney`; totals, change due and refund suggestions come from the server. Two Vitest source scans (`tests/guards/`) fail the suite on money arithmetic and on card-like inputs, because `eslint.config.mjs` is outside this handover's scope.

**Why.** C-5, C-6: a rounding difference is a support incident and a card field is a compliance incident.

### ADR-FE-093

**Decision.** **Staff actions render only from the API's `allowedActions`** (and the queue's `flags[]`); a server `403` renders the permission-denied state. Dialogs live in the screen, not under their button, so a 30-second refetch that removes an action never closes a dialog being filled in.

**Why.** HO-APT-002 ADR-FE-028; the order status machine is the server's. Returns have no `allowedActions` in the API, so their panels follow the return's status and the server still refuses a wrong move.

### ADR-FE-094

**Decision.** **Printables are print-styled routes** on one `PrintSheet` shell (A4 for the pick list and packing slip, 80 mm for the receipt). The chrome sits outside the sheet and the print stylesheet hides everything but the sheet. No PDF library, no server render.

**Why.** HW-1: the browser's print dialog already targets the thermal and A4 printers.

### ADR-FE-095

**Decision.** **Barcode scanning is a focused input with burst detection** (`useScanner`: keys under 50 ms apart ending in Enter are a scan; slower typing is manual entry; neither looks anything up before Enter). No scanner library, no camera. The basket, tenders and scan buffer are component state, never storage. `F2` focuses the payment amount; Enter adds the payment and opens a review whose *Complete sale* is focused.

**Why.** HW-2 keyboard wedge; NFR-USA-2's keyboard-only sale without leaving the scan field.

### ADR-FE-096

**Decision.** **Bank-transfer instructions are a first-class screen**: account details with copy buttons, the payment reference (the order number), a countdown to the deadline on the server clock, and *I have made the transfer* which submits the customer's own reference. The order is refetched when the deadline passes.

**Why.** US-ORD-19: the customer is away from the screen when they pay; the reference is what lets staff match the money.

### ADR-FE-097

**Decision.** **Money submissions are idempotent and single-shot**: one key per attempt (kept across retries of the same payload), buttons disabled while pending, no automatic retry, and `409 RequestInProgress` reads *"This is already being processed."*

**Why.** FR-ORD-20: an automatic retry over an ambiguous failure is how a customer is charged twice.

### ADR-FE-098

**Decision.** `/cart` is public in the storefront group; `/checkout` sits behind the customer guard and is reached with `?next=/checkout`; both are in the open-redirect allow-list.

**Why.** HO-ORD-001 OQ-1 (no guest checkout); the guard must stay closed.

## ADR-FE-119 … ADR-FE-126 — Customer portal home and re-order (HO-POR-002)

ADR-FE-124 and ADR-FE-125 (signed media URLs and the stay diary) are **retired**: the diary moved to HO-BRD-002 (README C-6), and BRD is skipped (owner, 2026-10-10). ADR-FE-126 records where the merged backend differs from the handover.

### ADR-FE-119

**Decision.** **The portal home is one request and one shell.** `GET /api/portal/dashboard` feeds every tile through `usePortalDashboard`; every tile is a `DashboardSection`. There is **no stays tile and no subscriptions tile**, not even a placeholder, because the backend has no such key (owner skipped BRD and SUB, 2026-10-10). When BRD or SUB are built, a tile is added with its key; nothing else changes

**Why.** The backend composes the sections and degrades each on its own (ADR-260), so a second request per tile would only add latency and inconsistency. One shell makes the three states impossible to render differently by tile

**Status.** Built. See `components/portal/dashboard/**`, `app/(portal)/portal/page.tsx`.

### ADR-FE-120

**Decision.** **Unavailable is never empty or zero, and a missing key is neither.** `Unavailable` renders "We couldn’t load … just now" with **Try again** (refetching the dashboard). A key that is absent from a section the backend is meant to deliver renders "Not available yet" with no retry. The no-cats invitation shows only for a `Ready` cats section with `hasCats: false`. Money is never summed across currencies

**Why.** A customer who sees "No cats" or "Nothing to pay" because a read failed has been told something false (ADR-FE-012, ADR-260). A missing key means "not built yet", which a retry cannot fix

**Status.** Built. See `DashboardSection`, `CatsTile`, `BalancesTile`.

### ADR-FE-121

**Decision.** **Tiles link out; they never rebuild another area’s page.** Every path the dashboard links to comes from `lib/routing/portal-links.ts`. Targets whose handover is not built (`/portal/orders/**`, `/portal/prescriptions`, `/cart`) have a constant there and are listed in `docs/test-cases/TC-HO-POR-002.md`; the day those pages land, only the pages change

**Why.** HO-POR-002 section 20.5. One file to reconcile, and a test (`links-and-guards.test.tsx`) that fails if a component writes a path of its own

**Status.** Built.

### ADR-FE-122

**Decision.** **There is no payment control in the portal.** Balances show what is owed per currency, the backend’s `howToPay` text, the clinics’ phone numbers (public branches) and links to invoices. No button or link whose name contains "pay" exists on the page (component test U-A4, e2e E-A2)

**Why.** SRS section 1.2: no payment gateway in v1; payments are COD, cash, in-store card and bank transfer

**Status.** Built.

### ADR-FE-123

**Decision.** **Re-order is Orders’ endpoint and the server decides.** The dashboard calls `POST /api/customers/me/orders/{orderId}/reorder` (HO-ORD-001 C15) with a UUID `Idempotency-Key`, shown when the order item’s own `canReorder` is true. The key is kept across a retry of one click and renewed after an answer. The client checks no stock, prescription or basket rule. The result dialog reports `addedCount` and each `unavailable[] { productName, reason }`; `reason` goes through a label map and an unknown code gets a generic sentence. Success invalidates `['cart']`, not the dashboard

**Why.** HO-POR-001 section 1 Amended 2026-10-10 (3): the portal has no re-order of its own. The ORD-002 review failed on a client workaround for a missing endpoint; where the backend gives less than the design shows (no added-lines list, no "already in your basket", no price-change flag), the screen shows less and the gap is a backend backlog row

**Status.** Built. See `useReorder`, `ReorderButton`, `ReorderResultDialog`, `describeReorderReason` in `lib/api/errors.ts`.

### ADR-FE-126

**Decision.** **The merged backend wins on the wire, and every difference is recorded** in HO-POR-002 section 1 Amended 2026-10-13. Differences found: (1) no `stays` / `subscriptions` key; (2) no portal re-order route, no `ReorderResultDto`, no `OrderNotFound` / `OrderNotReorderable` / `ReorderLineOutcome`; the call is Orders’, and it needs `Idempotency-Key`; (3) its answer is `{ addedCount, unavailable[{ productName, reason }] }` only, so there is no added list, no quantities, no "AlreadyInCart", no price-change flag; and a second re-order adds again; (4) `reason` is an open set of codes (`NotPurchasable`, `OutOfStock`, `PrescriptionNotAuthorised`, a cart rule code), not ten outcomes; (5) the server does not refuse a re-order of a non-re-orderable order: `canReorder` on the order item is the only gate, so there is no `400 OrderNotReorderable`; (6) the open-invoice count is taken over at most the newest 500 issued invoices; (7) the dashboard `visits[].followUpOn` and `orders[].trackingNumber` / `canReorder` are present (EMR, ORD); (8) `GET /api/portal/dashboard` is the only portal route

**Why.** HO-POR-002 section 20.4 and the owner’s 2026-10-13 instruction: do not shim, record. The backend gaps are in `docs/backlog/POR.md` (rows 21 onward)

**Status.** Built as specified.

---

## ADR-FE-069 … ADR-FE-078 — Surgery frontend slice (HO-SRG-002)

ADR-FE-069 … 076 are the eight the handover specified, recorded as **built**. ADR-FE-077 and 078 are two decisions taken during the build. Where the merged backend differs from the handover the backend wins and the difference is in HO-SRG-002 section 1 Amended (2026-10-14).

### ADR-FE-069 — Booking is one form with a three-resource picker

**Decision.** `SurgeryResourcePicker` takes the date, surgeon, assistant (the chosen surgeon is removed from the assistant options) and theatre, and asks `GET /api/staff/surgeries/availability` on every change. The starts are the server's, shown as buttons (*"09:00–10:00"*, *"Outside opening hours"*); `BusyExplainer` lists each resource's busy intervals with the server's labels under the picker, and a day with no start offers the next day that has one (the same endpoint, up to 7 days ahead). A `400` shows **every** clash beside its field (`theatreId`, `surgeonId`, `assistantId`, `startsOn`, `catId`, `serviceId`, `campaignId`) and in a summary that takes focus; a `409` says *"Someone has just booked that time. Availability has been refreshed."* and refetches. The booking choices live in the URL (`useSurgeryBookingParams`, `router.replace`).

**Why.** US-SRG-01 wants each resource checked with the reason; the server is the only clash authority, so the browser lists what it was told and works nothing out.

**Status.** Built. See `components/surgery/SurgeryResourcePicker.tsx`, `BusyExplainer.tsx`, `SurgeryBookingForm.tsx`, `lib/validation/surgery.ts` (`mapBookingViolations`).

### ADR-FE-070 — The anaesthesia log is an append-only timeline

**Decision.** An ordered list by `occurredOn` (time as text on the left, kind as icon **and** word, one column at every width). There is no edit control and no delete control anywhere. **Correct…** opens a form prefilled from the entry and posts a linked correction; the original stays, struck through and marked *"Corrected"* with *"Corrected at 10:42 by Dr Akter — reason"* and a link to the correction, and the correction links back.

**Why.** US-SRG-04 AC2 and US-SRG-08 AC2 must be visible, not only enforced. HO-SRG-002 section 20.6 stops any request for an edit or delete control.

**Status.** Built. See `components/anaesthesia/AnaesthesiaTimeline.tsx`, `CorrectEntryDialog.tsx`. Tests U-C1, U-C2.

### ADR-FE-071 — A controlled drug needs the witness step, and the credential is never kept

**Decision.** A controlled drug (or a correction of one) goes through `WitnessDialog` (HO-PHM-002's component, built for exactly this reuse). The backend takes the witness's identifier and password **in the same request** (HO-SRG-001 section 1 Amended (16)), not a confirmation id, so the primary button of the drug dialog is **Next: witness**, disabled until the wastage (0 allowed) and every other required part is filled; the entry is saved when the witness confirms. `useLogDrug` and `useCorrectEntry` are plain functions, not TanStack mutations, so a password is never held in a mutation cache; the credential is passed to the request and dropped. A witness refusal stays in the witness dialog and clears the password; any other refusal closes it and shows in the drug dialog with the typed values kept.

**Why.** BR-7 with identifier + password only; a password must never linger in React state, a cache, the URL or storage.

**Status.** Built. Tests U-C3, U-C4, U-C7.

### ADR-FE-072 — Start renders from the server

**Decision.** `StartPanel` shows **Admit** only when `allowedTransitions` has `Admit`, **Start surgery** only when it has `Start` and `startBlockers` is empty, and otherwise lists the blockers (in a live region) as links to the checklist rows. **Start with emergency override** appears only for a vet and only when consent is the sole blocker; it asks for a reason of 10 to 1000 characters (counter, **Continue** disabled below the minimum) and a final confirm that names the vet and says the override is recorded permanently. The reason lives in the dialog's own state, so a 30 s poll never clears it.

**Why.** BR-5; the gate is the server's and the UI must not offer a path the server refuses.

**Status.** Built. Tests U-B3 to U-B6.

### ADR-FE-073 — The recovery board is a front-desk tab, polled every 30 s

**Decision.** `RecoveryBoard` is the front desk's third tab, sorted *Ready for discharge* → *Recovering* → *Stable* (oldest status first), each card with the compact cat header, the owner and a `tel:` link, *"Ended 09:55"* and the status as text with *"since 10:15 · set by Nurse Akter"*; the header says *"Updated 10:42"*.

**Why.** FR-SRG-10 "near real time" is UI-5 polling (HO-SRG-001 ADR-168).

**Status.** Built. The tab is in `app/(staff)/staff/front-desk/FrontDeskView.tsx` (where HO-APT-002 put the tabs), not `page.tsx`.

### ADR-FE-074 — The report editor autosaves; finalising is separate and confirmed

**Decision.** The draft saves 5 s after the last change with the last known `expectedVersion` (none on the first save, which creates the draft) and says *"Draft saved 10:42"*. A `409` stops the autosave and shows a reload prompt that keeps the local text in a read-only box for copying. **Finalise** (vets) saves first, then asks for confirmation; `ReportIncomplete` (one violation whose message lists the missing parts) marks each missing field and focuses the first. A finalised report renders read-only with its addenda and **Add addendum**, never an editor.

**Why.** FR-SRG-5 immutability, visible to the user.

**Status.** Built. Tests U-D1 to U-D3.

### ADR-FE-075 — Units are part of the control

**Decision.** Every dose, quantity, weight and vital is shown and entered with its unit (`lib/format/surgery.ts`, `QuantityInput`, `FieldWithUnit`). The mg/kg figure is labelled *"≈ 0.2 mg/kg at 4.2 kg (weighed 11 Oct)"* and marked "for information"; no warning logic exists in the browser.

**Why.** UI-8; dose warnings are server policy (HO-SRG-001 OQ-11).

**Status.** Built. Tests U-C5 and `tests/unit/surgery-format-and-validation.test.ts`.

### ADR-FE-076 — The post-op sheet is an HTML route with a print stylesheet

**Decision.** `/staff/surgeries/[surgeryId]/post-op-sheet` uses the shared `PrintSheet` (HO-ORD-002 ADR-FE-094): its inline `@media print` rule hides everything but the sheet, so no navigation reaches the paper. The owner's discharged page uses the same component. No PDF library. `web/styles/print.css` was not created (section 13 permits it); the shared component already carries the rule.

**Why.** US-SRG-07 "produced"; no new dependency.

**Status.** Built. Test U-E4.

### ADR-FE-077 — Where HO-SRG-001 gives less than the screen wants, the screen says less

**Decision.** Five places, each recorded in HO-SRG-002 section 1 Amended: (1) the surgery detail has no `serviceId`, so the reschedule dialog finds the service in `GET /api/staff/surgeries/services` by the procedure's name; (2) a checklist item carries who completed it as an id only, so a name is shown only when it is the surgeon, assistant or overriding vet; (3) `neuterStatusApplied: false` carries no reason, so the notice says the record was not updated and offers **Apply neuter status**; (4) the discharge response carries no channel, so the success line says the instructions were sent to the owner and are in their inbox; (5) the customer detail has the branch name only, so the clinic's phone and address come from the public branch list.

**Why.** HO-SRG-002 section 20.5: never invent a backend field or a mock field. The screen states what the server gave.

**Status.** Built; each is a backlog candidate for HO-SRG-001.

### ADR-FE-078 — Cross-area components are consumed where they exist; a thin glue where they do not

**Decision.** HO-EMR-002 built a consent capture **page** (`/staff/consents/new`), not a `ConsentCaptureDialog`, so *Record consent* links to it (once per kind, Surgery and Anaesthesia). HO-PHM-002's `WitnessDialog` and `FormularyPicker` are reused as they are. HO-IMP-002 built no `BatchPicker`: `DrugBatchChooser` is a radio list over IMP-002's own `useBatches` hook (usable batches only, soonest expiry first), recorded as a deviation from section 20.2 for the owner to confirm.

**Why.** Section 8.5 assumed component shapes that the merged handovers did not build; the drug dialog cannot work without a batch choice.

**Status.** Built. `DrugBatchChooser` owner-accepted 2026-10-14 ("Accept it"); a shared, location-scoped picker is a later Inventory follow-up (IMP backlog 63).

## ADR-FE-139 … ADR-FE-148 — Cross-cutting and Technical frontend slice (HO-XCT-002)

Numbering: `ADR-FE-139…148` are the block HO-XCT-002 §9 names for itself and no other slice has used them (the NTF note above lists them as "allocated to later handovers"), so they are used as written. Where the merged backend differs from HO-XCT-001 section 12.4 the merged backend wins; every difference is recorded in HO-XCT-002 section 1 Amended (2026-10-14).

### ADR-FE-139

**Decision.** **Offline support is staff-only.** The worker is `public/staff/sw.js`, so its scope is `/staff/`. It is registered only by `ServiceWorkerRegistrar` in the `(staff)` layout. There is no `Service-Worker-Allowed` header and nothing in `next.config.ts` widens the scope. It never controls the storefront, the portal or `(auth)` routes (`tests/components/offline/sw-register.test.ts`)

**Why.** The requirement is the front desk. A root-scoped worker would put customer pages and their data under a cache nobody asked for, and would collide with MSW

**Status.** Built.

### ADR-FE-140

**Decision.** **The worker caches only hashed static assets (`/_next/static/**`, `/staff/icons/**`) and the static offline shell (`/staff/offline`).** Navigations are network-first (4 s). If the network fails, `/staff/offline` is served from cache; any other `/staff/` page gets a `302` to `/staff/offline?from=<path>` (so a reload or a direct visit while offline lands on the board with an explanation, not on a dead session guard). `/staff/sign-in` is never intercepted. Anything under `/api/` and any other origin is never touched (no `respondWith`). Registered only when `NODE_ENV === 'production'`, `NEXT_PUBLIC_USE_MOCKS !== 'true'` and the device opt-in is on; in every other case `reconcileServiceWorker` unregisters our worker (scope `/staff/`, script `/staff/sw.js`, never MSW's) and deletes its caches

**Why.** Caching staff HTML would cache rendered personal data outside the one store we control and wipe. Two workers in mock mode would silently break MSW

**Amended 2026-10-14 (gate fix round).** Navigations fall back to the board only when the fetch REJECTS; there is no timeout of its own, so a slow answer is never mistaken for an outage. Each warm-up evicts hashed chunks that the fresh shell no longer references. A stale worker is unregistered on EVERY load in development and mock mode by `ServiceWorkerCleanup` in the root layout (the `(staff)` layout is not reached when a stale worker breaks `/staff/sign-in`), and sign-out unregisters the worker as well as emptying its caches

**Status.** Built. The redirect for a non-shell navigation is this build's choice; HO-XCT-002 section 12.1 said "serve the cached shell", which would have put the offline page's markup under another page's URL.

### ADR-FE-141

**Decision.** **IndexedDB `purrcare-offline` (version 1, stores `snapshot` and `device`) is the one sanctioned persistence of personal data in this app.** Hand-written in `lib/offline/db.ts`. One snapshot per branch, the HO-XCT-001 payload plus `etag`, `fetchedAt`, `staffAccountId`; written only while the device flag is on; one person per device (another person's records are removed before a write). Wiped on: sign-out (the staff session query is removed from the cache, seen by `OfflineSnapshotSync`), a session read of `null`, `401 session_invalid` on a sync, a person change, `validUntil` passing, the opt-out, "Clear this device now", a schema-version mismatch, and a `403 ActionNotPermitted` on the snapshot. A `401 session_idle_timeout` and a network failure clear nothing. If IndexedDB is unavailable the feature does nothing: there is no `localStorage` fallback. No token, password, CAPTCHA answer or draft is ever stored. "Dismissed" state of the overdue banner is kept in the query cache, not in browser storage. **Amends HO-ACC-002 section 4.3.**

**Amended 2026-10-14 (gate fix round).** **Owner decision: customers' phone numbers are not kept.** `customerPhone` is discarded before anything is written (`withoutCustomerPhones`) and never shown; the backend is asked to stop sending it (backlog row 90). Phone numbers in a record written before this decision are stripped when it is read. The device record also holds `lastActivityAt` and `lockedForStaffAccountId` (opaque id and timestamp, no personal data). Concurrent changes to either store are one IndexedDB transaction, and a cached connection that the browser closes is reopened. Only one open tab refreshes the snapshot: a copy confirmed by any tab within 80 % of the refresh period is left alone

**Why.** NFR-AVL-3 cannot be met without data on the device. Residual risk: a signed-out but unwiped device (power loss) keeps the day's list until `validUntil`, on a physically controlled clinic machine

**Status.** Built. Tests use an in-memory backend (`setOfflineBackendForTests`), because jsdom has no IndexedDB and `fake-indexeddb` is not permitted.

### ADR-FE-142

**Decision.** **Connectivity is the server's answer, not the browser's flag.** `lib/connectivity/monitor.ts` pings `GET /api/system/ping` every 15 s while well, **every 3 s after a failure and for the first minute offline (owner-confirmed 2026-10-14, backlog row 64; it replaces OQ-3's plain 15 s)**, every 15 s until five minutes offline and every 60 s after, on `visibilitychange` and on the window `online` / `offline` events, with a 5 s timeout. Two consecutive failures, or one while `navigator.onLine` is false, mean `offline`; one success means `online`; a `5xx` means `degraded`, and the offline list is not offered. `client.ts` reports every network failure and response through `setConnectivityReporter` (the same direction as `setSessionErrorHandler`, so there is no import cycle). While the offline board is open its own 5 s check shares the monitor and keeps resetting its timer, so on that page the rate is about one ping every 5 s and the 15 s / 60 s back-off does not apply (an accepted exception). There is ONE pending check at any time (`scheduleNext` clears the current handle first), a check already in flight is shared, nothing is scheduled after `stop()`, and a test counts the pings across a long outage and after recovery. A caller's own aborted request is not counted as a failure. A tiny module-level store read through `useSyncExternalStore`: a device fact, not server data and not an app store. `ConnectivityProvider` also feeds the status to TanStack's `onlineManager`, so queries pause and polling stops while offline instead of failing

**Why.** `navigator.onLine` only means "an interface is up". A clinic with a dead uplink would look online

**Status.** Built.

### ADR-FE-143

**Decision.** **Offline is read-only and nothing is queued.** `apiFetch` throws `OfflineError` for `POST`/`PUT`/`PATCH`/`DELETE` before any `fetch` while the monitor says `offline`. `ConnectivityProvider` sets the QueryClient's default mutation `networkMode` to `always` (no change to `app/providers.tsx`). `WriteButton` and `ConfirmDialog` disable themselves with the visible reason "Offline — can't save" and `aria-describedby`. The page stays mounted, so typed input survives. `describeApiError`, `describeUnexpectedError` and `submitWithServerErrors` map `OfflineError` to the offline sentence

**Why.** US-XCT-01 AC2. A paused-and-replayed mutation is neither disabled nor honest

**Status.** Built for `client.ts`, `ConfirmDialog` and the new screens. Other slices' own submit buttons (the front desk's check-in dialogs, for example) are outside section 13 and are NOT disabled, only refused fast; reported as a finding against their owners.

### ADR-FE-144

**Decision.** **The offline board is its own route group, `(offline)`, with no session guard,** reading only IndexedDB (once). It refuses to show data when the copy is missing, out of date, in another schema version, or saved by a different `staffAccountId` than the one known from the in-memory session cache (a hard reload offline has no session in memory, so then only the wipe rules protect it). It locks after `idleTimeoutMinutes` without keyboard, pointer or touch input and cannot be unlocked offline. `?from=` is honoured only for same-site `/staff/` paths (`safeReturnPath`). The page is `force-static` and has no server data

**Amended 2026-10-14 (SECURITY gate fix).** The board does not trust a cold load. On mount it pings once: if the server answers (even with a 5xx) it goes to the staff app (`?from=` or the front desk) and shows nothing, and while open it asks again every 5 s. It opens locked when the device has recorded no activity, or none within `idleTimeoutMinutes` (`lastActivityAt`, written by the staff layout and the board about once a minute); its own idle timer counts that time already passed. The lock is saved in the device store and survives a reload; a successful snapshot sync by anyone clears it (the sync proves a live session and has already replaced another person's copy). Activity is not recorded while the HO-ACC-002 online idle lock is showing, and when that lock fires the board lock is saved too; a `lastActivityAt` in the future counts as no activity. A recovery that happened before the board opened (an earlier outage in the same page load) does not send it away

**Why.** A guard would bounce to sign-in exactly when sign-in is unreachable. The idle lock keeps C-9 true on an unattended terminal, deliberately at the cost of availability in a long outage (OQ-2)

**Status.** Built.

### ADR-FE-145

**Decision.** **Recovery is automatic and announced.** On the first reachable answer after `offline` the monitor calls its recovery listeners: `onlineManager.setOnline(true)` and `invalidateQueries()`. The banner says "Back online — updated just now." for 5 s. A board that was open goes to `?from=` or `/staff/front-desk`, or to sign-in if it had locked, and announces it in a polite live region. It never leaves because a first, optimistic "online" reading came before the first ping

**Why.** US-XCT-01 AC3, and a redirect loop is worse than a slow one.

**Status.** Built.

### ADR-FE-146

**Decision.** **Web vitals use `useReportWebVitals` from `next/web-vitals`.** Samples go through the `sendTelemetry()` function in `client.ts`: `credentials: 'omit'`, `keepalive`, `Content-Type: text/plain` (a CORS simple request, as a beacon), no CSRF or branch header. A sample holds metric, value, **route template** (from `usePathname()` plus `useParams()`, for example `/shop/products/[slug]`), navigation type, effective connection type and viewport class; a batch adds the build id. Batches are at most 10 samples (the API cap) and are flushed on `visibilitychange` (hidden), `pagehide` and route change. Nothing is sent from `/staff/**`. Samples the API would refuse (an upper-case route, an unknown metric) are dropped client-side. Sampling is 100 % (OQ-4). The build id is `NEXT_PUBLIC_BUILD_ID` when set and `unknown` otherwise (setting it needs a `next.config.ts` edit that section 13 does not allow)

**Why.** `credentials: 'omit'` means no cookie, so nothing links a measurement to a person

**Status.** Built.

### ADR-FE-147

**Decision.** **Budgets are checks you can fail, not dashboards, and live in the repo.** `perf-budgets.json` names the storefront routes. `scripts/check-js-budget.mjs` (gzip level 9 of every first-load chunk, layouts included) fails when a route is over `firstLoadJsKb` or no longer exists. `scripts/route-inventory.mjs` generates the route list from `app/**/page.tsx`. `tests/perf/lcp.spec.ts` (CDP Slow 4G, 4x CPU, median of 5, Chromium), `tests/perf/viewport-sweep.spec.ts` (360/768/1024/1440) and `tests/a11y/app-sweep.spec.ts` read that list. They run with their own config, `tests/perf/playwright.config.ts`, because `playwright.config.ts` is outside section 13. No Lighthouse dependency. The budget names `/shop/products/[slug]`, not the guessed `/shop/[slug]` of HO-XCT-002 section 11.6, which does not exist

**Why.** NFR-PRF-1 must be provable before release, on an agreed profile

**Status.** Built; measured numbers are in `docs/test-cases/TC-HO-XCT-002.md`.

### ADR-FE-148

**Decision.** **This slice measures; the owning area fixes.** Edits outside this slice are limited to the lines section 13 names: `app/layout.tsx` (the reporter), `app/(staff)/layout.tsx`, `app/(staff)/staff/customers/[customerId]/page.tsx` (the erase panel), `components/common/ConfirmDialog.tsx` and `WriteButton.tsx`, `components/admin/adminNavItems.ts` (three entries), `lib/api/client.ts` and `errors.ts`, `next.config.ts` (the worker header) and the `package.json` scripts. No sweep excludes a route and no budget was relaxed. Breaches found are reported in the test sheet against the owning handover

**Why.** Otherwise the last handover in the order silently rewrites the others' screens

## ADR-FE-129 … ADR-FE-138 — Reporting screens (HO-RPT-002)

### ADR-FE-129

**Decision.** **One generic report viewer**, drawn entirely from the catalogue payload: filter controls from `parameters`, columns from `columns`, the default range from `defaultRange`, totals and notes from the result. There is no per-report page or component, and `components/reports` names no report key (a source scan, `source-scan.test.ts`, fails if one appears). A key the caller's catalogue does not list is still asked of the server, so a direct URL shows the server's own `403` or `404`.

**Why.** The server defines 21 reports now and will define more. A per-report screen guarantees drift between what the server can filter and what the screen offers.

**Status.** Built. See `components/reports/ReportViewer.tsx`.

### ADR-FE-130

**Decision.** **`/staff` is the staff home and the landing route.** `app/page.tsx` and the staff sign-in default now go to `/staff`; `/staff/cats` stays in the nav. OQ-1 default taken (yes). The nav gets two entries, Home and Reports, in `nav/areas/reporting.ts` (the registry file this area owns), because the shell reads the registry, not `app/(staff)/layout.tsx`.

**Why.** FR-RPT-3 says the home dashboard shows the role's figures; HO-ACC-002 flagged the landing route as a product decision.

**Status.** Built. **Owner-confirmed 2026-10-14 (OQ-1: "Dashboard").** No existing Vitest test asserted the old landing. *Correction (TEST gate, 2026-10-14):* the Playwright case `home.spec.ts` "signed in as staff lands on ..." did, and was changed to expect `/staff` under a coordinator scope amendment (HO-RPT-002 Amended (19)).

### ADR-FE-131

**Decision.** **The client computes no figure.** No totals, margins, percentages, currency maths, next-run times, covered periods or default date ranges. Money stays a string; `defaultRange`, `totals`, `nextRuns` and `periodForNextRun` come from the server. Only string tests are used where a word is needed (is a variance amount non-zero), and the chart converts a string to a number purely to plot it. Consequence: the design's "Yesterday / This week / Last week / Last 30 days" presets are NOT offered, because the API has no way to resolve them and resolving them here would be date arithmetic; the one preset is "Default: {period}" from `defaultRange`.

**Why.** Two implementations of "net revenue" is how a dashboard and a report end up disagreeing in front of the owner.

**Status.** Built. **Owner 2026-10-14: "One date preset" is accepted as built.** Presets are an owner question (HO-RPT-002 section 1 Amended).

### ADR-FE-132

**Decision.** **Exports are fetched as a blob and saved with an object URL**, revoked at once. The fetch is an `XMLHttpRequest` in `lib/download/reportExport.ts` (as `inventory-download.ts` does) so `client.ts` stays the only `fetch` call site; every status maps onto `client.ts`'s error classes, and a `429` keeps its `Retry-After`. The filename is the server's `Content-Disposition` when the browser can read it; the API exposes only `Date` to cross-origin scripts, so otherwise it is `purrcare-{key}-{from}-to-{to}.{ext}`, the same pattern the server uses.

**Why.** A navigation cannot show `413 ReportTooLargeForPdf` or `400 ReportTimedOut`.

**Status.** Built. CORS exposure of `Content-Disposition` and `Retry-After` is a backend follow-up.

### ADR-FE-133

**Decision.** **A report table collapses to labelled cards when its container is narrower than 640 px**, measured with `useNarrowContainer`, not at a 768 px viewport. The staff sidebar takes 240 px from 640 px up, so the content width is the honest signal. Cards are a `<dl>` per row; the desktop first column is a sticky row header; on narrow screens sorting is a `<select>`.

**Why.** UI-1 at 360 px for tables the server may make 12 columns wide; the same approach as the inventory stacked tables.

**Status.** Built.

### ADR-FE-134

**Decision.** **Charts are optional companions** for `timeBucketed` reports only: closed by default, lazily loaded, `aria-hidden`, with a visually hidden `<table>` repeating every point through `formatCell`. Series are the numeric columns; points are labelled by the leading non-numeric columns. Bar for counts and money, line for percentages.

**Why.** UI-6; a chart that recomputed from raw rows would contradict ADR-FE-131.

**Status.** Built.

### ADR-FE-135

**Decision.** **The dashboard polls on the payload's `refreshSeconds`** (floor 10 s), never in the background, keeps the previous tiles while a refetch is in flight or fails, and announces "Dashboard updated" in a polite live region at most once a minute. "Updated HH:mm" is the server's `generatedOn` in the branch zone.

**Why.** UI-5: the refresh must not reset an open dialog or a focused control.

**Status.** Built.

### ADR-FE-136

**Decision.** **Schedule timing is previewed by the server.** The editor posts the draft timing to `…/preview` (debounced 500 ms, a mutation) and shows the next runs, the covered period and the quiet-hours hold exactly as returned. The preview carries no zone, so instants are labelled with the branch's zone (the primary branch's for "All branches"). The periods the editor offers mirror `ReportSchedulePeriods.IsCompatible` from the catalogue's `kind` and `defaultPeriod` as a convenience; the server still decides.

**Why.** One calculator, on the server, so the DST night cannot disagree.

**Status.** Built.

### ADR-FE-137

**Decision.** **Permission is expressed by absence.** A report the caller may not open is not in the catalogue; a restricted column is not in the payload and is not rendered; a tile not sent is not drawn. There is no "locked" affordance. The Reports nav entry is shown to every staff role (presentation only, like Messages): a Nurse gets `PermissionDeniedState` on `/staff/reports`.

**Why.** Telling someone which financial columns exist but are withheld is itself information.

**Status.** Built. Hiding the nav entry per role needed a shell change outside this handover (section 1 Amended). *Update, owner 2026-10-14 ("Hide the menu entry"):* `NavItem.roles` was added and the Reports entry is hidden for roles without `Reports.Any` (a Nurse); the page-level `403` refusal stays as defence in depth.

### ADR-FE-138

**Decision.** **The merged backend wins on the wire and every difference is recorded** in HO-RPT-002 section 1 Amended: `branchId=all`, `groupBy` values, the relative-period names, no zone on the preview, no `groupBy` on a schedule, recipient errors that do not name the person, a variance with no direction, deferred reports simply absent.

**Why.** HO-RPT-002 section 20.4 and the owner's instruction: do not shim, record.

**Status.** Built.

---

## HO-BRD-002 — Boarding (ADR-FE-099 … ADR-FE-108)

The block `ADR-FE-099 … ADR-FE-108` is the one `docs/handover/README.md` reserves for HO-BRD-002 (checked
free on master and on every open `ho/*` branch before use). Each entry states the decision as built.

### ADR-FE-099

**Decision.** **The booking flow is one route with a stepper; the search lives in URL params and the enquiry lives on the server from the cats step on, with `stayId` in the URL.** `/boarding?arrivalOn&departureOn&catCount&unitType` is the deep link for steps 1-2; once cats are chosen the page calls `POST …/enquiries` and the URL gains `stayId`. A vaccination block renders in place (`VaccinationGatePanel`) with a **Book a vaccination** link and the enquiry is resumable from `/portal/boarding`. **Resume** re-submits the same enquiry (`PUT …/enquiry`), which the server re-evaluates, and lands on the first step that still needs work (gate, then care profiles, then review). `/boarding` sits under the `(portal)` layout, so it needs a customer session like `/book`; the availability endpoint itself is public

**Why.** US-BRD-04 AC2 ("without re-entering details") only works if the draft is not in memory. The portal layout's `CustomerSessionGuard` already carries the query string through sign-in (`?next=`)

**Status.** Built. See `app/(portal)/boarding/**`, `components/boarding/booking/**`. The sign-in allow-list entry for `/boarding` is a one-token edit to `app/(auth)/sign-in/page.tsx`, outside section 13; `BOARDING_NEXT_PREFIX` in `lib/routing/boarding-links.ts` is the value to add (HO-BRD-002 section 1 Amended).

### ADR-FE-100

**Decision.** **The task board completes with one tap on a 44 px+ `Done` button, optimistically, with a live-region announcement and an Undo only while the request is in flight; no dialog, no required note.** `useCompleteTask` flips the card in the query cache in `onMutate`, rolls it back in `onError`, and shows the server's reason. **Undo** aborts the in-flight `POST …/complete` (an `AbortController` signal passed through `apiFetch`) and restores the card; once the server has answered there is no Undo, and a completion that had already reached the server simply shows as done on the next refresh

**Why.** NFR-USA-3 is measured (E-C2); a confirm dialog would double every tap

**Status.** Built. See `useCompleteTask`, `TaskCard`.

### ADR-FE-101

**Decision.** **Dashboard, task board, occupancy board and incident queue poll every 30 s; a poll never resets an open wizard, form, dialog or an in-flight completion.** New data is applied around them: dialogs and sheets own their values in component state, the task list is not remounted by a poll, and newly arrived incidents are announced in a polite live region rather than re-ordering focus. The occupancy board and the task board hold the list they are showing while a dialog is open (`useFrozenWhile`)

**Why.** UI-5 "without losing unsaved input"

**Status.** Built.

### ADR-FE-102

**Decision.** **The occupancy board is an in-repo CSS grid** (rows = units grouped by block, columns = days; 14 days, paged by week, `from` in the URL) with stay bars, maintenance bands (patterned) and turnaround hatching, each also written in text. **No drag-and-drop**: extend and relocate are dialogs, because both need a reason and the server's alternatives. Below 768 px it becomes a day list of units (no horizontal scroll), and the list is also the keyboard path at every width

**Why.** HO-ADM-002 ADR-FE-016; a drag cannot express "why"

**Status.** Built. See `OccupancyGrid`, `OccupancyDayList`.

### ADR-FE-103

**Decision.** **Signatures are captured by an in-repo pointer-event canvas that posts stroke points as JSON, with an equal typed-name + confirmation alternative always visible.** Points are rounded to integers, clamped to 0…10 000 and thinned to stay under the server's 32 KB; nothing is rendered as an image and no file is produced. The typed path is a radio, not a fallback hidden behind the canvas

**Why.** HO-BRD-001 ADR-236; a canvas-only signature is a WCAG failure and there is no storage for an image

**Status.** Built. See `components/signature/SignaturePad.tsx`, `boundStrokes` in `lib/validation/boarding.ts`.

### ADR-FE-104

**Decision.** **Nothing uploads.** Belongings, diary and incident media are recorded as a reference (storage key, type, size) in a clearly marked dev-only field with the honest note "Photo storage is not available yet — the item is recorded without a photo". Where `boarding.requireBelongingsPhotos` makes the server refuse, its message is shown verbatim. The diary shows the recorded reference and caption as text, never an image. HO-FIL-002 section 1 Amended (2026-09-28) confirms BRD stays upload-free; HO-BRD-001's later amendment to store `fileId`s is therefore not consumed here (HO-BRD-002 section 1 Amended)

**Why.** Inventing an upload endpoint or consuming `FileUploader` is outside section 13/14

**Status.** Built. See `MediaReferenceFields`, `MediaRecordDialog`.

### ADR-FE-105

**Decision.** **Every price, total, discount, deposit, balance and charge line is rendered from the server; the client never adds, multiplies or prorates money, and never derives availability, capacity, overdue or "not eating".** Amounts stay strings end to end and go through `formatMoney` only. The overdue chip formats the server's `minutesLate`; the "Not eating — flagged" banner reads the server's flag. The one piece of date arithmetic is the nights count shown beside the date inputs before the server has answered (calendar days between two strings; the server's `nights` replaces it). The early check-out recalculation cannot be shown *before* submit because HO-BRD-001 section 12.4 has no preview endpoint (section 20.5): the wizard says the nights will be recalculated and shows the server's recalculated lines and credit on the result

**Why.** HO-BRD-001 sections 8.8/8.9 own those rules; a second implementation in the browser will disagree

**Status.** Built; the early check-out preview is a recorded gap.

### ADR-FE-106

**Decision.** **There is no payment control anywhere.** The owner sees the amount, the pay-by time and the how-to-pay content page. Staff see **Record payment** (a link to Billing with the stay reference until HO-BIL-002's dialog is wired in), then **Confirm stay**, and an explicit *awaiting approval* state. The UI never claims a stay is confirmed until the server says so, and the word "pay" appears only in text that tells the owner how to pay offline

**Why.** SRS A-2, C-5; the confirmation is the server's reading of Billing (HO-BRD-001 ADR-223)

**Status.** Built. A test greps the boarding bundle for payment words (U-X4).

### ADR-FE-107

**Decision.** **Actions are rendered only from `allowedTransitions[]` and the server's capability flags; a `403` renders `PermissionDeniedState`.** No role check is hardcoded; the sidebar and tab rows hide nothing by role (they link to screens that answer `403` themselves)

**Why.** HO-BRD-001 section 12.3; the role map is the server's (ADR-233)

**Status.** Built.

### ADR-FE-108

**Decision.** **Check-in and check-out are wizards that submit once.** Per-cat steps collect everything in component state; one request creates the whole checklist, so a half-checked-in stay cannot exist. The server's `…ChecklistIncomplete` violations name `Cats[i].Field`; each is mapped back onto the offending cat step and field, the wizard jumps to the first cat with a problem, and nothing already typed is lost. A second click while the request is in flight is ignored

**Why.** HO-BRD-001 section 8.11 refuses an incomplete checklist as a unit

**Status.** Built. See `CheckInWizard`, `CheckOutWizard`.

### ADR-FE-104 and ADR-FE-105 -- corrections (2026-10-15, ARCHITECTURE gate M3)

Appended; the entries above are left as written.

**ADR-FE-104 (no upload) is superseded.** By the owner's decisions of 2026-10-14 ("Add the uploader") and 2026-10-15 ("Yes, diary photos too"), Boarding consumes HO-FIL-002's `FileUploader`, unmodified, for **belongings photos at check-in** (purpose `BoardingBelongings`) and for **diary photos** (purpose `BoardingMedia`); only the returned `fileId` is sent. Incident photos have no field on the incident request and are not offered. `MediaReferenceFields` is removed. Diary media show a **View photo** link from the signed `file.url` the diary returns.

**ADR-FE-105 (server-derived values) keeps its rule and loses two sentences.** The early check-out preview *is* shown before submit (`GET .../check-out-preview`, HO-BRD-001 1.1, with `isEarly`/`isLate`; the browser's clock no longer decides early or late). The built task list has **no** `minutesLate`, so the overdue chip says "Overdue" without minutes; the server's `isOverdue` still drives it. A value the backend does not send is shown as absent and never invented (no 0.00 balance, no assumed signature method, no default "referred by").

**Status.** Built.

## ADR-FE-173 — Hearth customer layer (foundation)

### ADR-FE-173

**Decision.** **The owner-approved "Hearth" look (2026-10-16) is a customer-only layer built on the existing `data-theme="customer"` tokens.** `globals.css` adds `--accent-tint`, `--radius-card` (20px), `--radius-band` (32px) and `--radius-pill` inside `[data-theme='customer']` only, with Tailwind aliases (`bg-accent-tint`, `rounded-card`, `rounded-band`, `rounded-pill`); they are undefined on staff layouts, so staff screens are unchanged. Shared pieces live in `components/hearth/` (`HearthLogo`, `HearthBand`, `LineArtCat`, `IconTile`, `PillSteps`, `OrderTracker`, `ProductPackArt`, `FreeDeliveryProgress`, `fields.ts` pill input classes). `Button` gains `pill`/`pillSecondary` variants and `pillMd` (44px) / `pillLg` (54px) sizes; the staff variants and sizes keep their look (the radius and text size moved from the base class into the sizes). `ShopHeader`, `SearchBox`, `CartBadge`, `LanguageSwitcher` and `PortalHeader` take the Hearth header. Warm `#e8894a` is a fill only, `warm-ink` is the warm text colour, and no emoji or paw prints are used

**Why.** One look for every customer screen without forking components, and no risk to staff screens. Owner decisions of 2026-10-16 in `docs/design/shop/README.md` and `docs/design/orders/README.md`

**Status.** Foundation built. `FreeDeliveryProgress` renders only when the server sends the amount still to go (the quote needs a threshold field; the browser never computes one, ADR-FE-105). `PillSteps` and `OrderTracker` take their statuses from the caller, never infer them. Shop and order pages are restyled on top of this in follow-up work.
