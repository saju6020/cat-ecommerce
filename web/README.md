# web/ — PurrCare frontend

The frontend scaffold for this repo, per [`docs/handover/HO-CAT-002.md`](../docs/handover/HO-CAT-002.md)
§12.1. This is Step 0 — it builds the scaffold only. **No functional area's screens are
implemented here.** Every later frontend slice (Cat Profiles, Accounts & Identity, Appointments,
…) extends this and must not recreate it.

## Stack

| Layer                | Choice                                                       | Version                          |
| -------------------- | ------------------------------------------------------------ | -------------------------------- |
| Framework            | Next.js 15, App Router                                       | `15.5.26`                        |
| UI                   | React                                                        | `19.3.0`                         |
| Language             | TypeScript, `strict: true`, `noUncheckedIndexedAccess: true` | `5.9.3`                          |
| Styling              | Tailwind CSS v4                                              | `4.3.3`                          |
| Components           | shadcn/ui — copied into `components/ui/`, owned in place     | (n/a — copied, not a dependency) |
| Server state         | TanStack Query v5                                            | `5.103.2`                        |
| Forms                | react-hook-form + zod                                        | `7.88.0` / `4.6.5`               |
| Icons                | lucide-react                                                 | `1.48.0`                         |
| Charts               | recharts                                                     | `3.10.1`                         |
| Dates                | date-fns                                                     | `4.4.0`                          |
| Mocking              | MSW                                                          | `2.15.0`                         |
| Unit/component tests | Vitest + Testing Library + jsdom                             | `5.0.1`                          |
| E2E                  | Playwright + `@axe-core/playwright`                          | `1.63.0` / `4.13.0`              |
| Lint/format          | ESLint (flat config) + Prettier                              | `9.39.5` / `3.9.9`               |

Exact versions are pinned in `package.json` (no `^`/`~`). See §10 of HO-CAT-002 for the permitted
dependency list — everything above is on it. Adding anything not on that list needs approval.

## Running it

```bash
cd web
npm install
cp .env.local.example .env.local
npm run dev          # http://localhost:3000
```

```bash
npm run lint          # ESLint, zero warnings
npm run typecheck     # tsc --noEmit
npm run test          # Vitest
npm run test:e2e      # Playwright (needs `npx playwright install` once, and a running app)
npm run build          # production build
npm run format         # Prettier --write
```

`npm run build` fetches the Newsreader / Public Sans font files from `fonts.googleapis.com` at
build time (`next/font/google`) — it needs outbound network access to that host. It failed under
this session's default sandboxed shell (DNS blocked) and passed once run unsandboxed; if your CI
blocks that host, either allow it or switch `app/layout.tsx` to self-hosted font files.

### Calling the API — same-SITE, not just CORS (HO-ACC-002 §1 Amended, 2026-09-26)

`lib/api/client.ts` calls `NEXT_PUBLIC_API_BASE_URL` directly — a real cross-origin request, with
`credentials: 'include'`, never a same-origin proxy. This is the owner's explicit choice (separate
subdomains in production), not an oversight, but it has one hard requirement: a `SameSite=Lax`
session cookie (`pc_session` / `pc_staff_session`) is only ever sent on a **same-SITE** request —
same registrable parent domain, **and the same scheme** ("schemeful same-site"). CORS allowing the
origin is not enough; a same-site check happens first, independently of CORS, and a real browser
silently drops the cookie if it fails.

- **Production**: the web app and the API live on two subdomains of the same registrable domain,
  both served over `https` (e.g. `https://www.example.com` and `https://api.example.com`). That is
  same-site, so the cookies are sent. Deploying the API on a different registrable domain, or on a
  different scheme, breaks sign-in silently (the API returns `200`/`Set-Cookie`, the browser just
  never attaches the cookie back).
- **Development**: `NEXT_PUBLIC_API_BASE_URL` must use the **same scheme** as the page the browser
  is actually on. `.env.local.example` points at `http://localhost:5005` — the API's `http` launch
  profile — precisely because `npm run dev` itself serves plain `http`. Pointing this at an
  `https://localhost:7xxx` profile instead will build and run, MSW mode will look fine, but every
  real-API session cookie will silently fail to round-trip, because `http://localhost:<port>` →
  `https://localhost:7xxx` is cross-site on scheme alone, never mind that both say "localhost".
- **The `pc_csrf` cookie needs its own `Domain`, in production** (SECURITY gate item 7, first-pass
  gate fix round, 2026-09-26): `pc_session`/`pc_staff_session` are `HttpOnly` and only need the
  browser to attach them automatically to a request the same-SITE check above already allows — they
  stay host-only always. `pc_csrf` is deliberately **not** `HttpOnly` (`client.ts` has to read it
  via `document.cookie` to echo it into `X-CSRF-Token`), which means it also needs to be _readable
  from the web app's own page_. A host-only `pc_csrf`, set by a response from `api.example.com`, is
  never visible to `document.cookie` on `www.example.com` — a different host entirely, regardless of
  the same-SITE check having already let the cookie through on the wire. The API's
  `Account:CsrfCookieDomain` setting (empty by default — host-only, correct for same-host local
  development) must be set to the shared parent domain (e.g. `.example.com`, with the leading dot)
  in that production deployment's own `appsettings`/environment, so `pc_csrf` becomes readable on
  both subdomains. Nothing on the web side changes for this — `client.ts` already just reads
  whatever `document.cookie` exposes.
- **`app/page.tsx` asks the API client-side, never reads a session cookie itself** (same gate item):
  a Server Component's `cookies()` read only ever sees cookies scoped to the web app's _own_
  hostname — never a cookie scoped to the API's separate subdomain, however the same-SITE check
  above resolves. Reading `pc_staff_session`/`pc_session` there would silently look like "signed
  out" for everyone in a real cross-subdomain production deployment. The root page instead calls
  `useCustomerSession()`/`useStaffSession()` (the same hooks `CustomerSessionGuard`/
  `StaffSessionGuard` use) client-side, and fails closed to `/sign-in` on anything but a resolved
  session.

### Environment variables for the storefront (HO-SHP-002)

| Variable                    | Where it is read          | Meaning                                                                                                                                                                                                                                                                                                                                                             |
| --------------------------- | ------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `NEXT_PUBLIC_API_BASE_URL`  | browser and server        | The API origin the browser calls (see above).                                                                                                                                                                                                                                                                                                                       |
| `API_ORIGIN`                | server only (`getPublic`) | Optional. The API origin the storefront's **Server Components** read public endpoints from (`lib/api/client.ts`). Use it when the server reaches the API on a different address than the browser does (an internal host name, a container network). Unset, it falls back to `NEXT_PUBLIC_API_BASE_URL`. It is never sent to the browser and carries no credentials. |
| `NEXT_PUBLIC_USE_MOCKS`     | dev only                  | `true` starts MSW in the browser and, for server rendering, in `instrumentation.ts`. Both only run under `next dev`; a production build ignores it.                                                                                                                                                                                                                 |
| `NEXT_PUBLIC_CURRENCY_CODE` | browser                   | Optional, default `BDT`. Only the staff price form's fallback when the API has shown no price for the product yet (`lib/shop/currency.ts`); otherwise the currency comes from the API.                                                                                                                                                                              |

## Folder layout

```
web/
  app/
    layout.tsx              root: fonts (next/font/google), Tailwind, <Providers>
    providers.tsx            TanStack QueryClientProvider (client component)
    globals.css               design tokens — base layer + [data-theme="customer"] layer
    page.tsx                  redirects by session (HO-ACC-002 §1 Amended, 2026-09-26) — see
                               "Known open items" below; no longer "first registered nav item"
    (staff)/layout.tsx        back-office shell: <StaffShell> — sidebar, header, <main>
    (portal)/layout.tsx       owner-facing shell: <PortalHeader> — sets data-theme="customer"
  components/
    ui/                       shadcn/ui primitives, copied in and owned (button.tsx so far)
    common/                   shared, non-domain components — DataTable, ConfirmDialog,
                               EmptyState, ErrorState, LoadingState, FieldWithUnit, StaffShell,
                               PortalHeader; see "Shared components" below for the full table
  lib/
    api/
      client.ts               the ONLY fetch call site — ApiError/ApiValidationError/
                               ApiNotFoundError/ApiConflictError (HO-CAT-002 §11.2)
      types.ts                 ValidationError / ValidationResponse / PagedResult<T> ONLY —
                               shared by every area. Domain types belong in that area's own
                               lib/api/<area>.ts
      errors.ts                applyValidationErrors() — bridges ValidationResponse to
                               react-hook-form (HO-CAT-002 §11.8)
    hooks/                     one use<Thing>.ts per area (useAccount.ts, useCats.ts, …) — the
                               only place useQuery/useMutation may be called; enforced by an
                               ESLint rule, not just a convention (no longer empty — HO-ACC-002/
                               HO-CAT-002 both populated it)
    utils.ts                   cn()
  nav/                        navigation registry — see "Adding a functional area" below
    types.ts
    index.ts                  imports all 18 areas — pre-wired, do not edit
    areas/<area>.ts           one file per area, starts as `items: []`
  mocks/                      MSW — see "Adding a functional area" below
    server.ts                  setupServer() for Vitest/Playwright
    handlers/
      index.ts                 combines all 18 areas — pre-wired; only edit to alias a split
                               area folder (handlers/<area>/**), as HO-ADM-002 and HO-NTF-002 do
      areas/<area>.ts          one file per area, starts as `[]`
  tests/
    setup.ts                   Vitest bootstrap: jest-dom + MSW server lifecycle
    unit/                      Vitest specs for lib/ code (tests/unit/<area>, tests/unit/lib)
    components/<area>/         Vitest + Testing Library specs, one folder per area
    e2e/                       Playwright specs, one file per area/flow, plus fixtures.ts
  package.json  tsconfig.json  next.config.ts  eslint.config.mjs  vitest.config.mts
  playwright.config.ts  postcss.config.mjs  .prettierrc  .env.local.example
```

Nothing under `components/cat/` exists yet — that is Cat Profiles' own folder (HO-CAT-002
§11.3–§11.7) and should be created by that slice, not by this scaffold or any other area.

## Shared components (`components/common/`, `components/ui/`)

Built once, ahead of the parallel area slices, so no two areas build near-duplicates. Every area
should reuse these rather than hand-roll a second table/dialog/empty-state — see each file's own
JSDoc for the full contract.

`components/common/` (HO-CAT-002 §11.7 names all five as shared; DataTable/ConfirmDialog/
FieldWithUnit are reused explicitly by HO-IMP-002 §5/§13). `StaffShell` isn't one of those five —
it's the scaffold's own `(staff)` layout shell, listed here because HO-CAT-002 also rewrote it
(owner decision 2026-09-25, recorded above):

| Component       | One-line usage                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                    |
| --------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `DataTable`     | Generic list table — filter (optional free-text box), sort (click a `sortable` column header) and paginate, all written to the URL. Pass `columns`, `rows`, `getRowId`, and an `emptyState`; it renders `LoadingState`/`ErrorState`/`EmptyState` itself for the three list states. Domain-specific filters (species, status, …) go in your own filter form above it.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              |
| `ConfirmDialog` | UI-3 confirm dialog. `open`/`onOpenChange` controlled from wherever you keep that `useState`; `consequence` is a **required** prop — there is no generic "Are you sure?" path. Escape closes and focus returns to whatever opened it.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             |
| `EmptyState`    | `title` (says what to do next) + optional `description`/`action`/`icon`. Defaults to a neutral icon — pass `icon={<EmptyCatIllustration />}` (also exported from this file) on a **portal** screen only; never on staff.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                          |
| `ErrorState`    | The "couldn't load this just now" pattern with a **Try again** button wired to `onRetry`. Never pass a raw server/`ApiError` message into `description` — map it to plain language first.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                         |
| `LoadingState`  | Skeleton bars (`variant="table"` for a `DataTable`-shaped skeleton, `variant="card"` for a profile/detail page), not a spinner. Respects `prefers-reduced-motion`.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                |
| `FieldWithUnit` | UI-8's "unit always visible, in the label's accessible name" field, for every weight/dose/quantity input. `id`, `label`, `unit` (e.g. `"kg"`), optional `hint`/`error`.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                           |
| `StaffShell`    | The `(staff)` layout shell — sidebar `<aside>`, header, `<main>`. **Below 640px** (Tailwind's `sm`), the in-flow `<aside>` is _always_ the collapsed `w-16` rail (pure CSS — no `sm:`-gated width class overrides it there, so this is true from the first byte of server-rendered HTML, before hydration); there is no wider in-flow overlay to reproduce below that breakpoint. The rail's "Expand menu" button instead opens the full nav as a real Radix `Dialog` (`<DialogContent variant="sheet-left">`, see below) — a modal, so focus-trapping, `Escape`-to-close, overlay-click-to-close and focus-return all come from Radix, not a hand-rolled scrim/`inert`/keydown implementation. At 640px and up, nothing below changes: the aside is its own always-mounted `w-60`/`w-16` collapse toggle, independent of the sheet. Rewritten in full for HO-CAT-002 (owner decision 2026-09-25, recorded above) — see the file's own doc comment for the rest of the reasoning. |

`components/ui/` additions (shadcn/ui primitives, copied in and owned — `Button` was the only one
before this):

| Component                                         | One-line usage                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 |
| ------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Dialog` family                                   | Radix Dialog, restyled (the `shadow-modal` exception to "borders, not shadows"). `useDialogReturnFocus(open)` is the piece that makes focus-return work for a dialog opened from a plain button rather than a `<DialogTrigger>` — pass its ref to `<DialogContent returnFocusRef={...}>`. `<DialogContent variant="center" \| "sheet-left">` (default `"center"`) — `"center"` is the usual dialog, centred in the viewport, byte-for-byte the same classes as before this prop existed; `"sheet-left"` docks Content to the left edge, full height, for a slide-in nav/filter panel (StaffShell's below-640px nav sheet is the only current user). Everything else — portal, overlay, focus trap, `Escape`, focus return — is identical between variants; only position and sizing differ. Added by HO-CAT-002 directly to this shadcn/ui-owned file (see "Don't edit `components/ui/*`" below) rather than as a second, near-duplicate dialog component, per §9's "customise shadcn/ui components in place." |
| `Toast` family + `Toaster` + `useToast`/`toast()` | The app's one toast surface — already mounted in `app/providers.tsx`. Call `toast({ title, description, variant })` from anywhere; never render a second `<Toaster />`.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        |
| `Table` family                                    | Plain semantic `<table>` wrappers styled to the product-list mockup. Available for a one-off table that doesn't need `DataTable`'s filter/sort/paginate behaviour. `DataTable` (`components/common/`) no longer builds on this — it wraps its own table markup directly instead, in a keyboard-focusable `role="region"` scrollable wrapper this primitive's own `overflow-x-auto` div can't opt out of (ARCH gate fix, HO-ACC-002 §1 Amended, 2026-09-26).                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                    |
| `Badge`                                           | `variant` is `neutral` / `success` / `warning` / `danger`, plus an `icon` prop — UI-6: colour is never the only signal, so pass an icon alongside the text for any status badge.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               |

## Mocks in `npm run dev`

`NEXT_PUBLIC_USE_MOCKS=true` in `.env.local` (already set in `.env.local.example`) starts
browser-side MSW (`mocks/browser.ts`) from `app/providers.tsx`, in development only. It intercepts
`fetch` the same handlers (`mocks/handlers/`) that Vitest/Playwright use via `mocks/server.ts`, so
`npm run dev` works against mocks with no backend running. Set it to `false` (or unset it) to hit
`NEXT_PUBLIC_API_BASE_URL` for real. The worker script is `public/mockServiceWorker.js`,
(re)generated by `npx msw init public --save` if it's ever missing.

## Design tokens

`app/globals.css` defines the PurrCare tokens as CSS custom properties, exposed to Tailwind via
`@theme inline` (so e.g. `--color-surface-page` becomes the `bg-surface-page` / `text-surface-page`
utilities, `--radius-md` becomes the `rounded-md` utility, and so on). No hex literals belong in
component code — consume tokens by name.

- **Base layer** (`:root`) applies everywhere, staff and customer alike.
- **Customer layer** (`[data-theme="customer"]`) overrides `--surface-page`, `--radius-md`, and
  adds `--warm` / `--warm-ink` / `--warm-soft`. It is applied **once**, on
  `app/(portal)/layout.tsx`'s root element, and must never be applied to a staff layout or a
  shared component. `--warm` is a fill colour — never use it for text (contrast).
- Dark mode is **not** implemented: HO-CAT-002 §4.1 explicitly excludes it ("Dark mode, theming,
  i18n | Not required by any Cat Profile story").

## Adding a functional area

This scaffold exists so that up to four areas can be built **in parallel, in separate git
worktrees**, without two agents ever needing to edit the same file. Follow this or a parallel
merge will conflict on exactly the files this section protects.

**Do:**

1. Add your own route folder under `app/(staff)/staff/<area>/` and/or
   `app/(portal)/portal/<area>/`. Next.js discovers routes from the filesystem — no shared file
   lists your routes, so there is nothing to conflict on here.
2. Populate **your own** `nav/areas/<area>.ts` — change `items: []` to your screens' links, and
   set `audience` (`'staff' | 'portal' | 'both'`) correctly. `nav/index.ts` already imports it.
3. Populate **your own** `mocks/handlers/areas/<area>.ts` with MSW handlers for your endpoints,
   derived from your handover's API contract (never invented — HO-CAT-002 §5.1, §20.2).
   `mocks/handlers/index.ts` already imports it.
4. Add your own domain types and endpoint functions in a new `lib/api/<area>.ts` (e.g.
   `lib/api/cats.ts`), mirroring your handover's view models exactly. Add your own
   `lib/hooks/use<Thing>.ts` files declaring the `useQuery`/`useMutation` calls for your area —
   `lib/hooks/` has no aggregating index, so there is nothing shared to edit.
5. Add your own `zod` schemas next to the form that uses them, and your own query-key constants
   next to the hook that owns them (HO-CAT-002 §8.4's convention: `['cats', filters]`,
   `['cat', catId]`, …, namespaced by your area so two areas can never collide).
6. Add components under `components/<area>/` (e.g. `components/cat/`) for anything specific to
   your domain. Only add to `components/common/` for something genuinely reusable by every area,
   and only if it doesn't already exist there.

**Don't:**

- Don't edit `nav/index.ts` or `mocks/handlers/index.ts` — both already import all 18 areas.
- Don't edit another area's `nav/areas/*.ts` or `mocks/handlers/areas/*.ts` file.
- Don't edit `lib/api/client.ts`, `lib/api/types.ts`, `lib/api/errors.ts`, `lib/utils.ts`,
  `components/ui/*`, `components/common/*`, `app/layout.tsx`, `app/providers.tsx`,
  `app/globals.css`, or either route group's `layout.tsx` unless your handover explicitly says to.
  These are shared; treat a needed change to one as a finding to raise, not a silent edit.
  (`lib/identity.ts`, this scaffold's single hardcoded staff identity, no longer exists —
  HO-ACC-002 deleted it outright once real session cookies replaced it.)
  - **Recorded exception (HO-CAT-002, owner decision 2026-09-25):** `components/ui/dialog.tsx`
    (the `variant` prop) and `components/common/staff-shell.tsx` (the below-640px rewrite, both
    documented below) were edited in place by HO-CAT-002 to meet UI-1 (360px) and §8.5's "hand-
    rolled dialogs do not" — declared openly in the commits and reviewed (not silent), and accepted
    by the owner rather than reverted. Every area still inherits the result automatically; this
    bullet exists so the next slice that needs a further shared-component change knows to raise it
    the same way, not to treat "don't edit" as absolute.
- Don't add a dependency not already in `package.json`'s permitted list (HO-CAT-002 §10) without
  approval — `package.json` is the one file this scaffold could not fully insulate from
  cross-area edits, so keep changes to it rare and additive (`npm install --save-exact <pkg>`,
  never `npm audit fix --force` or a version bump of something another area may depend on).

**Template for a brand-new area** (replace `<area>` and `<Area>`):

```ts
// nav/areas/<area>.ts — change items from [] to your screens
items: [{ label: 'Your screen', href: '/staff/<area>', icon: 'list' }],

// mocks/handlers/areas/<area>.ts — add your handlers
export const <area>Handlers: HttpHandler[] = [
  http.get('/api/<area>', () => HttpResponse.json({ ... })),
];

// lib/api/<area>.ts — new file
export interface <Area>ListItem { /* mirror the handover's view model exactly */ }
export function get<Area>List() { return apiFetch<...>('/api/<area>'); }

// lib/hooks/use<Area>.ts — new file
export function use<Area>List() {
  return useQuery({ queryKey: ['<area>'], queryFn: get<Area>List });
}
```

## Known open items for the next slice to pick up

- **`lib/api/client.ts` does not attach identity headers yet.** HO-CAT-002 §11.2 asks for this,
  but the actual header name is part of the API contract (HO-CAT-001 §12.4 / HO-ACC-001 §12),
  which this scaffold deliberately does not read or invent (§20.2). Add it once an area reads
  that contract.
- **`npm audit`** reports 2 advisories in Next.js 15.5.26's bundled internal `postcss` copy
  (fixed only by Next 16, which HO-CAT-002 §3.1 does not permit). Not our direct `postcss`
  dependency (`8.5.28`, unaffected). Revisit when a Next 15.x patch picks up a fixed `postcss`.
- **`app/page.tsx`** (HO-ACC-002 §1 Amended, 2026-09-26, integrator-authorised edit) redirects by
  who's visiting, not by nav registry order: a `pc_staff_session` cookie sends them to
  `/staff/cats`, a `pc_session` cookie to `/portal/cats`, otherwise to `/sign-in` — the same two
  hardcoded defaults `(auth)/sign-in` and `(auth)/staff/sign-in` already use after a real sign-in.
  This supersedes both the original "first registered nav item" behaviour and HO-CAT-002 §8.3's
  unconditional `/staff/cats` redirect.
