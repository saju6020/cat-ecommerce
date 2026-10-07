# HO-CAT-002-ADDCAT — Customer "add a cat" screen (frontend slice)

## 1. Identity

| Field    | Value                                                                                               |
| -------- | --------------------------------------------------------------------------------------------------- |
| Slice ID | **HO-CAT-002-ADDCAT** (a slice of [`HO-CAT-002`](HO-CAT-002.md), not a new area)                    |
| Date     | 2026-10-06                                                                                          |
| Backlog  | `docs/backlog/CAT.md` row **C-APT1**; owner request 2026-10-06                                      |
| Stories  | US-CAT-01 (register a cat), seen from the owner portal; unblocks HO-APT-002 AC-1 for a new customer |
| Branch   | `ho/CAT-002-ADDCAT` from master `578dc79`                                                           |
| ADR      | None needed. Decision recorded here: `CatForm` gains an additive `audience` prop (no new form)      |

## 2. Purpose

A new customer had no way to add a cat, so could not book online. This adds `/portal/cats/new` and
links to it from My cats and from the booking flow's empty cat step.

## 3. Backend facts (no backend change)

`POST /api/cats` allows customers (`Cats.Register`); for a customer caller the controller replaces
`OwnerId` with the caller (F19). `RegisterCatRequest.OwnerId` is still a `required Guid`, so the
portal sends the empty GUID; the server never reads it for a customer.

## 4. Scope (allowed files)

- `web/app/(portal)/portal/cats/new/page.tsx` (new)
- `web/app/(portal)/portal/cats/page.tsx` ("Add a cat" button and empty state)
- `web/app/(portal)/book/BookingFlow.tsx` (computes `addCatHref` only)
- `web/components/booking/CatStep.tsx` (empty state links to add-a-cat)
- `web/components/cat/CatForm.tsx` (additive `audience` and `cancelHref` props; staff behaviour unchanged)
- `web/lib/validation/bookingReturnPath.ts` (new: validates the booking return path actually navigated to)
- `web/mocks/handlers/areas/cat-profiles.ts` (POST: 100-character name 400; empty-GUID owner means the signed-in customer)
- tests listed in section 7; this document.

Out of scope: photo upload (the register API takes no photo; the design's photo block is not built),
owner-scoped cat list (separate open item), any backend or migration.

## 5. Behaviour

- Fields: name (required), species (default Cat), sex, date of birth with precision (Exact or Estimated), breed, colour and markings. No owner field.
- Client validation mirrors the staff form; server 400s map to fields, unmapped ones to a form-level message. Typed values are always kept (NFR-USA-5).
- Success: toast, then `?next=` if it is a `/book` path (checked with `sanitizeNextPath`) with `catId` set to the new cat, otherwise `/portal/cats/{catId}`.
- Booking flow: with no cats the step shows **Add a cat** to `/portal/cats/new?next=<current /book URL>`, so the booking parameters survive.

## 6. Governing documents

| Document                                        | What you need from it                                                                  |
| ----------------------------------------------- | -------------------------------------------------------------------------------------- |
| [`HO-CAT-002`](HO-CAT-002.md)                   | The parent slice: `CatForm` (§11.7), error mapping (§11.8), scaffold and design tokens |
| [`HO-CAT-001`](HO-CAT-001.md)                   | `POST /api/cats` contract (§12.4) and violation codes (§10)                            |
| [`HO-APT-002`](HO-APT-002.md)                   | The booking flow's cat step and `/book` URL parameters                                 |
| [`HO-ACC-002`](HO-ACC-002.md)                   | `sanitizeNextPath` (`?next=` guard), read only here                                    |
| [`docs/USER_STORIES.md`](../USER_STORIES.md) §2 | US-CAT-01                                                                              |
| [`docs/SRS.md`](../SRS.md)                      | UI-1, UI-6; NFR-USA-4, NFR-USA-5                                                       |
| `docs/backlog/CAT.md`                           | Row C-APT1                                                                             |

There is no `.ai/` document for the frontend; do not apply backend rules to React code.

### Design reference

`docs/design/cat-profile/CatRegister.dc.html` (fields, precision radios), `docs/design/portal/DashboardNewCustomer.dc.html` ("Add a cat" invitation), `docs/design/cat-profile/Portal.dc.html` (warm My cats cards). The portal layout supplies the warm layer.

## 7. Tests

| ID          | Level            | Asserts                                                                                                                                                                                     |
| ----------- | ---------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| U-AC1       | component        | customer mode has no owner field                                                                                                                                                            |
| U-AC2       | component        | empty name refused, values kept                                                                                                                                                             |
| U-AC3       | component        | Exact precision needs a date                                                                                                                                                                |
| U-AC4       | component        | server 400 mapped to its field, values kept                                                                                                                                                 |
| U-AC5-U-AC8 | component (page) | success redirect; `/book` next returns with parameters and catId; off-site next ignored; server error keeps values                                                                          |
| U-AC9       | component        | empty cat step links to add-a-cat with `next`                                                                                                                                               |
| U-AC10      | component        | an OwnerId server error shows as the form-level message in customer mode                                                                                                                    |
| U-AC11      | component (page) | after a successful POST the button stays disabled (Saving…) and a second click sends no second POST                                                                                         |
| U-RP2       | unit             | `buildAddCatHref` carries every booking parameter and round-trips through the return path                                                                                                   |
| U-RP1       | unit             | every open-redirect payload (dot-segments, encoded dots, backslash, `//`, other origin, `/book/../portal/account`) falls back to the cat page; valid `/book?...` returns with the new catId |
| E-AC1       | e2e              | add a cat from My cats, see it, book with it from /book                                                                                                                                     |
| E-AC2       | e2e              | empty name keeps page and values                                                                                                                                                            |
| E-AC3       | e2e              | from the booking detour, returns to /book with the cat chosen                                                                                                                               |

HO-APT-002 E-A1 did not assert the old "no add-cat screen" text, so it is unchanged.
Known gap: the shared cat mock is not owner-scoped, so a truly empty cat list cannot be staged in e2e; U-AC9 covers the empty step.

## 8. Acceptance criteria

1. A signed-in customer can open `/portal/cats/new` from My cats (button and empty state) and register a cat without entering an owner.
2. Errors are plain language and no typed value is lost.
3. On success the customer reaches the new cat's page, or returns to `/book` with its parameters when they came from booking.
4. Staff registration (`/staff/cats/new`) is unchanged.
5. No backend, migration or new dependency.
6. An `OwnerId` server error in customer mode appears as the form-level message, never on a hidden field.
7. A `?next=` that is not a clean `/book` or `/book/**` path (dot-segments, encoded dots, backslash, `//`, other origin) falls back to the cat page.

## 14. Forbidden scope

Mirrors [`HO-CAT-002`](HO-CAT-002.md) §14. Nothing here is a stop-and-ask unless it needs one of these.

| Path                                                   | Why                                                                    |
| ------------------------------------------------------ | ---------------------------------------------------------------------- |
| `src/**`                                               | The .NET backend. A missing field or endpoint is a stop, never an edit |
| `tests/**` (repo root)                                 | Backend tests                                                          |
| `.ai/**`                                               | Backend architecture                                                   |
| `web/lib/validation/nextPath.ts`                       | HO-ACC-002's file; its hardening is ACC backlog row A-APT1             |
| `docs/gates/**`, `docs/SRS.md`, `docs/USER_STORIES.md` | Gate records and requirements are not edited by this slice             |
| `package.json`, lockfile                               | No new dependency                                                      |
