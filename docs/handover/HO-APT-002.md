# HO-APT-002 — Clinic Appointments (frontend)

## 1. Identity

| Field | Value |
|---|---|
| Handover ID | **HO-APT-002** |
| Title | Clinic — Appointments — frontend slice |
| Date | 2026-09-22 |
| Author | Claude Opus 5 |
| Backlog | [`docs/USER_STORIES.md`](../USER_STORIES.md) v2.4 §3 — **US-CLN-01 … US-CLN-10** |
| SRS | FR-CLN-2 … FR-CLN-11; **UI-1, UI-3, UI-5, UI-6, UI-7**; NFR-USA-1, NFR-USA-2, NFR-USA-4, NFR-USA-5; AC-1 |
| Pairs with | [`HO-APT-001`](HO-APT-001.md) — **§12.4 there is the contract**. On any disagreement about the wire, HO-APT-001 wins (§20.4) |
| Builds on | [`HO-CAT-002`](HO-CAT-002.md) (scaffold, cat header), [`HO-ACC-002`](HO-ACC-002.md) (sessions, `client.ts`, customer search, walk-in customer form, customer page), [`HO-ADM-002`](HO-ADM-002.md) (active branch, `lib/format`, the in-repo grid approach) and [`HO-NTF-002`](HO-NTF-002.md) (inbox) |
| Delivery order | Section 5 of 17 in [`README.md`](README.md#delivery-order) |
| Approved design | None. Build to §11 with the existing shadcn/ui tokens |
| Baseline commit | `4007225` |
| Amended | **2026-10-04 — recorded by the agent that built it (not a later handover); every item is a deviation from, or a finding about, this document, so the gates can see it.** (1) **Wire shapes — HO-APT-001's code wins over its §12.4 prose (§20.4, no shim):** the hold `201` carries `performerId`/`roomId` (backlog row 40); confirm, reschedule and move return the appointment detail itself, not `{ appointment }`; `branch` is `{ id, name, phoneNumber }`; `priceAmount` is a JSON number; a `409` carries a `ValidationResponse` (`SlotNoLongerAvailable` on every slot claim including staff Book/Move — backlog row 22 — or `StaleCalendarVersion` on a stale move). `lib/api/client.ts` therefore gains, beyond "Date-header offset capture only", an additive optional body on `ApiConflictError`. (2) **Missing field — service id:** `AppointmentSummary`, `AppointmentDetail`, the calendar rows, the day list and the queue carry the service SNAPSHOT (name, category, duration, price) but no service id, and the slot endpoints need one. The customer reschedule picker bridges it with an exact name + category + duration match against `GET /api/services` (`findServiceIdForSnapshot`; no match means "call the clinic"), and **Move…** takes a typed time on the grid instead of times from the staff slots endpoint (§11.6) — no availability is computed client-side. **Recommended backend follow-up: add `serviceId` to those view models.** (3) **No `allowedTransitions` on the day list or queue** (detail only): the Front desk reads each open row's detail and renders actions from it (ADR-FE-028); recommend adding the field to those entries. (4) **No room override** on staff booking (§11.3): no free-rooms endpoint exists (§20.5); the slot's room is used. (5) The **flagged** payload has no customer, phone or zone: joined from the detail and customer records. (6) The **queue** payload carries only the cat's name and high-risk flag, so the queue card's "compact cat header" (`CompactCatHeader`) shows those; the full header is on the appointment detail and staff booking. (7) `StartDialog` lists every vet and room of the branch (the lists carry no qualification or kind); the server's refusal is the filter, shown in the dialog. (8) **My queue** is shown to roles `Vet` and `Administrator` (no vet account exists in the mocks). (9) **No-show grace** is the server's rule — `RecordNoShow` is in `allowedTransitions` for any `Booked` appointment, and an early attempt is refused with its message. (10) **`Date` header and CORS:** the API does not send `Access-Control-Expose-Headers: Date`, so a cross-origin browser cannot read it and the countdown falls back to the browser clock (ADR-FE-025); the mock sends it. **Integrator follow-up in `HostPipelineExtensions`.** (11) `GET /api/branches` sends `addressLine`/`phoneNumber`; HO-ADM-002's `PublicBranch` type and mock say `address`/`phone` — `getClinicBranches` reads both. (12) **Prerequisite gaps, not built around:** the owner portal has no "add a cat" screen (HO-CAT-002), so the booking flow's cat step says so when empty and E-A1 uses the mock's existing cats; HO-NTF-002 is not merged in this worktree, so no inbox assertion exists (E-A1, E-B2, E-B3). (13) **Files outside §13 touched:** `web/components/portal/CustomerSessionGuard.tsx` (the sign-in `?next=` now carries the query string, so a reminder link returns with its booking parameters; `/book` was already on the sign-in allow-list); `web/nav/areas/appointments.ts` (the shells read the nav registry, so the nav entries live there and the two layouts are unchanged); `web/mocks/handlers/areas/appointments.ts` (re-exports `mocks/handlers/appointments/**`, the HO-ADM-002 split); `web/tests/e2e/appointments-helpers.ts` and `web/tests/components/**/{helpers,nav-mock}.*` (shared test helpers, not specs); `web/components/portal` and the cat page link needed no change (the "Book appointment" link already existed). (14) **Not runnable here, so not claimed:** the real-API re-runs of E-A1…E-E2, a genuine two-customer race (E-A2 runs two MSW contexts and the mock's deterministic lost race), the clinic pass offering a freed slot (E-B3), and leave approval flagging an appointment (E-E2 starts from an already flagged mock appointment). **2026-10-04 — owner decision, closing the ARCHITECTURE BLOCKER (e116c69):** the owner approved the 4-line edit to HO-ACC-002's `web/components/portal/CustomerSessionGuard.tsx` (§14) — §8.1 requires a reminder link to return with its booking parameters, and `sanitizeNextPath` still applies. §13's non-existent `web/lib/routing/**` is corrected to `web/app/(auth)/sign-in/page.tsx`, and the guard file is listed in §13 with this decision as its authority. |
| Amended | **2026-10-06 — by [`HO-CAT-002-ADDCAT`](HO-CAT-002-ADDCAT.md) (backlog C-APT1), closing item (12)'s "no add-a-cat screen" gap.** `web/components/booking/CatStep.tsx`'s empty state no longer tells the customer to ask the clinic: it shows **Add a cat**, linking to `/portal/cats/new?next=/book?<current booking params>`. `web/app/(portal)/book/BookingFlow.tsx` only computes that link (`addCatHref`). On return the booking parameters are kept and `catId` is set to the new cat; the return path is validated by `web/lib/validation/bookingReturnPath.ts` (same origin, `/book` or `/book/**`, no `//` or backslash, re-sanitised with `sanitizeNextPath`). E-A1 is unchanged (it never asserted the old text). |

---

## 2. Objective

Build **online booking for customers** and **the front desk for staff**.

- **Customers** choose a cat, a service, a date and optionally a vet. They see real slots, hold one
  with a visible countdown, confirm, and later reschedule or cancel. When a day is full they can join
  a waitlist. **There is no payment step** — a booking is an open appointment, paid at the clinic
  (HO-APT-001 ADR-073).
- **Reception** books by phone, registers walk-ins into a triaged queue in under 60 seconds, checks
  cats in, records no-shows, and manages a day/week calendar per vet and per room with drag-and-drop
  moves that the server re-validates.

Three things make this harder than it looks:

1. **Time belongs to the branch.** Every time shown or chosen is in the branch's zone, labelled
   (HO-ADM-002 ADR-FE-014). A customer booking from abroad sees the clinic's clock.
2. **The server decides.** Slots, conflicts, cut-offs and allowed actions all come from the API. The
   UI never computes availability and renders actions only from `allowedTransitions` (ADR-FE-028).
3. **The countdown must be honest.** A hold expires on the server's clock, not the browser's. The
   countdown corrects for clock skew and never lets a customer think they still hold a slot they have
   lost (ADR-FE-025).

---

## 3. In scope

### 3.1 Inherited — do not revisit

The HO-CAT-002 stack; HO-ACC-002's session model, `client.ts`, route groups, error mapping,
`PermissionDeniedState`, `UnsavedChangesGuard`, customer search and walk-in customer form; HO-ADM-002's
active branch, `lib/format` and in-repo grid approach; HO-CAT-002's cat profile header (UI-7).

### 3.2 Stories and screens

| Story | Screens | Route |
|---|---|---|
| US-CLN-02, 03 | **Booking flow**: cat → service → branch (hidden with one) → date, vet filter and slots → hold with countdown → confirm (reason for visit) → done | `/book` (deep-linkable: `?catId&category&serviceId&branchId&date&performerId`) |
| US-CLN-04 | My appointments (upcoming / past); appointment detail with **Reschedule** and **Cancel**, or the clinic's contact details after the cut-off | `/portal/appointments`, `/portal/appointments/[appointmentId]` |
| US-CLN-06 | No screen — messages arrive in the inbox (HO-NTF-002) and link to the appointment detail | — |
| US-CLN-09 | "Join the waitlist" when a day is full; my waitlist entries; **claiming an offered slot** from the detail page | `/book` (full-day state), `/portal/appointments` (waitlist section), `/portal/appointments/[appointmentId]` |
| US-CLN-05, 10 | **Front desk**: tabs *Today* (booked list with check-in and no-show) and *Queue* (live, triaged, estimated waits, walk-in registration, start/complete) | `/staff/front-desk` |
| US-CLN-07 | Calendar, day/week × per vet/per room, drag-and-drop move with a keyboard alternative; staff booking; appointment detail; flagged-for-rescheduling list | `/staff/appointments`, `/staff/appointments/new`, `/staff/appointments/[appointmentId]`, `/staff/appointments/flagged` |
| US-CLN-08 | No-show count on the staff booking screen and the staff customer page | HO-ACC-002's customer page gains an **Appointments** panel |
| US-CLN-01 | No screen — HO-ADM-002's service editor | — |

---

## 4. Out of scope

| Excluded | Why |
|---|---|
| Any backend change | HO-APT-001 owns `src/**` |
| **Any payment, deposit or price-to-pay step** | Owner instruction; ADR-073. The service price is shown for information only |
| Vitals, consultation notes, the consultation screen | HO-EMR-002. **Start** here only changes status and opens nothing |
| The portal dashboard | HO-POR-002 |
| Offline day list | HO-XCT-002 |
| A calendar, scheduler, drag-and-drop, date-picker or time-zone library | §10 |

Forbidden shapes are HO-ACC-002 §4.3's, unchanged.

---

## 5. Prerequisites

| Required | State |
|---|---|
| Node 20 LTS, HO-CAT-002 scaffold | ❌ at baseline — §20.1 |
| **HO-ACC-002, HO-ADM-002, HO-NTF-002 built** | ❌ at baseline — §20.1 |
| HO-APT-001 API | ❌ — build against MSW generated from §12.4 there, then re-run every E-case against the real API |

---

## 6. Governing documents

| Document | What you need |
|---|---|
| [`HO-APT-001`](HO-APT-001.md) | §12.4 endpoints and shapes; §10.4 codes and their messages; §8.3 the status machine; §8.5 what a slot is; §8.7 how the queue is ordered and the wait estimated (so the screen can explain it); §12.3 who may do what |
| [`HO-ADM-002`](HO-ADM-002.md) | §8.3 active branch; §8.4 `lib/format`; ADR-FE-016 the grid approach |
| [`HO-ACC-002`](HO-ACC-002.md) | §11.2 client; §11.11 walk-in customer form; §11.14 error mapping; customer search |
| [`HO-CAT-002`](HO-CAT-002.md) | Cat header (UI-7), cat quick-add |
| [`docs/SRS.md`](../SRS.md) §3.1, §6.4 | UI-1/3/5/6/7; NFR-USA-1/2/4/5 |

---

## 7. Build rules

| Req | Rule | Where |
|---|---|---|
| **UI-1** | 360 px floor. The booking flow is single-column; the calendar becomes a **day agenda list** below 768 px (no horizontal grid scroll); the queue becomes cards | §11.1, §11.5, §11.6 |
| **UI-3** | Confirm, stating what is affected: cancel (customer and staff), reschedule, move (*"Move Mitu's vaccination from 10:00 with Dr Rahman to 11:30 with Dr Akter?"*), no-show (*"This counts against {customer}."*), releasing a hold by leaving | Each screen |
| **UI-5** | Front desk and calendar refetch every **30 s** without resetting any open form, dialog or drag (ADR-FE-027) | §11.5, §11.6 |
| **UI-6** | WCAG 2.1 AA. Slots are buttons in a labelled group, not a canvas; the countdown is announced at 5 min, 1 min and expiry, not every second; **every drag has a keyboard and pointer-free alternative** (the *Move…* dialog); triage priority is text plus colour | §11 |
| **UI-7** | The cat header (name, photo, age, sex/neuter, weight, allergies, chronic conditions, alerts, high-risk flag) appears on staff booking, the appointment detail and each queue card (compact variant) | HO-CAT-002's component |
| **NFR-USA-1** | A first-time customer books in under 3 minutes: the flow skips steps with one choice (one cat, one branch) and pre-fills from the deep link | E-A1 |
| **NFR-USA-2** | A receptionist registers a walk-in and queues it in under 60 seconds: one panel, keyboard only, customer search → (new customer inline) → cat → service → priority → **Add to queue** | E-D1 |
| **NFR-USA-4/5** | Server messages in plain language; forms keep values across errors | HO-ACC-002 §11.14 |

---

## 8. Frontend architecture

### 8.1 Route map — additions

```
web/app/
  (portal)/
    book/page.tsx                                  booking flow (stepper)             US-CLN-02, 03, 09
    portal/appointments/page.tsx                   upcoming, past, waitlist           US-CLN-04, 09
    portal/appointments/[appointmentId]/page.tsx   detail, reschedule, cancel, claim  US-CLN-04, 09
  (staff)/staff/
    front-desk/page.tsx                            Today + Queue tabs                 US-CLN-05, 08, 10
    appointments/page.tsx                          calendar                           US-CLN-07
    appointments/new/page.tsx                      staff booking                      US-CLN-07, 08
    appointments/[appointmentId]/page.tsx          detail                             US-CLN-07, 10
    appointments/flagged/page.tsx                  flagged list                       ADM-03 AC2
    customers/[customerId]/page.tsx                ⚠ gains AppointmentSummaryPanel (HO-ACC-002)
    cats/[catId]/page.tsx                          ⚠ gains a Book appointment link (HO-CAT-002)
  (staff)/layout.tsx                               ⚠ nav entries: Front desk, Appointments
  (portal)/layout.tsx                              ⚠ nav entry: Appointments
```

`/book` is in `(portal)`: signing in is required before a hold. An unauthenticated visitor from a
reminder link is sent to sign-in with `?next=` carrying the booking parameters (HO-ACC-002's
open-redirect guard accepts `/book` — one entry added to its allow-list).

### 8.2 API modules

`lib/api/booking.ts` (slots, performers, holds, confirm), `lib/api/my-appointments.ts` (list, detail,
reschedule, cancel, waitlist), `lib/api/staff-appointments.ts` (slots, calendar, day list, detail,
book, move, cancel, check-in, triage, start, complete, no-show, flagged, customer summary),
`lib/api/front-desk.ts` (queue, walk-ins). Public services and branches come from HO-ADM-002's
`public-content.ts` / branches module.

### 8.3 State

| Kind | Where |
|---|---|
| Booking choices (cat, service, branch, date, performer) | **URL search params** (ADR-FE-024) — reload-safe and deep-linkable |
| Current hold (`appointmentId`, `holdExpiresOn`) | Component state of the flow, **not** the URL and not storage. A reload loses it; the server expires it |
| Slots | `['booking','slots', params]`, refetched on step entry and after any `409` |
| My appointments, detail | `['me','appointments', …]` |
| Calendar, day list, queue | `['staff','calendar', range, view, ids]`, `['staff','day', date]`, `['staff','queue', performerId]`, each `refetchInterval: 30_000`; all include the active branch in the key so a branch switch refetches |
| Pending move | Local state of the calendar until the server answers (§11.6) |
| Forms | react-hook-form + zod |

### 8.4 Time — `lib/format` additions

- `formatSlot(startsOn, endsOn, zone)` → `10:30–11:00` and `formatAppointmentWhen(startsOn, zone)` →
  `Mon 12 Oct, 10:30` (both with `Intl`, zone from `branchTimeZoneId`).
- **Server clock offset**: `client.ts` records `Date` response headers; `useServerNow()` returns
  `Date.now() + median offset` of the last five responses. The countdown and "can I still change this"
  hints use it (ADR-FE-025).
- Dates chosen in the booking flow are `yyyy-MM-dd` **branch-local** strings, never `Date` objects.

---

## 9. ADRs and open questions

Record in `web/docs/decisions.md`, continuing HO-NTF-002's series.

| ADR | Decision | Why |
|---|---|---|
| **ADR-FE-024** | The booking flow is **one route with a stepper**; choices live in URL params, the hold only in memory | Reminder links (`/book?catId&category`) deep-link into it; a reload never pretends a hold survived |
| **ADR-FE-025** | The hold countdown counts down to `holdExpiresOn` on the **server clock** (offset from `Date` headers), announces at 5 min, 1 min and 0, and on reaching 0 drops the hold, refetches slots and says *"Your hold has ended — please choose a time again."* | US-CLN-03 AC1/AC2 on a phone whose clock is wrong |
| **ADR-FE-026** | The calendar is an **in-repo CSS grid**, one column per vet or room, rows on the slot grid; drag-and-drop with pointer events; every move also available through a **Move…** dialog; a drop shows the appointment as *pending* at the target until the server answers, then commits or snaps back with the server's reason | No scheduler library (HO-ADM-002 ADR-FE-016); keyboard users must be able to move appointments; the server is the only conflict authority |
| **ADR-FE-027** | Front desk and calendar poll every 30 s; polling replaces data but **never** resets an open form, dialog or an in-progress drag (the refetch result is applied after the drag ends) | UI-5 "without losing unsaved input" |
| **ADR-FE-028** | Status actions (check in, start, complete, no-show, cancel) are rendered **only** from the API's `allowedTransitions`; customer change actions only from `canChange` | The status machine is the server's; a hardcoded button list drifts |

| # | Question | Default |
|---|---|---|
| OQ-1 | Days shown at once in the customer date picker | A 14-day strip plus a "pick a date" month grid (in-repo) |
| OQ-2 | Calendar start and end hours | The branch's earliest opening to latest closing that week, ±1 h |
| OQ-3 | Should leaving the booking flow release the hold at once? | Yes — `DELETE` on route change and `pagehide` (best effort; the server expiry is the guarantee) |

---

## 10. Dependencies

**No new package.** A calendar, scheduler, drag-and-drop, date-picker or time-zone library is a §20
stop.

---

## 11. Specified changes — components and screens

Each component under ~150 lines, with distinct loading / empty / error states.

### 11.1 Booking flow — US-CLN-02, 03 · `/book`

| Step | Component | Behaviour |
|---|---|---|
| 1 | `CatStep` | The customer's alive cats; skipped when there is one or `catId` is given. *"Add a cat"* links to HO-CAT-002's form and returns here |
| 2 | `ServiceStep` | From `GET /api/services` (online-bookable, priced): name, duration, **price for information** (*"Pay at the clinic"*), description. `category` param pre-filters (a vaccination reminder lands on vaccination services) |
| 3 | `BranchStep` | Skipped with one branch; shows address and phone |
| 4 | ⭐ `SlotStep` | `DateStrip` (14 days + month grid, OQ-1); **vet filter** from `GET /api/booking/performers` ("Any vet" default); slots from `GET /api/booking/slots`, grouped morning / afternoon / evening, each a button *"10:30 · Dr Rahman"* (with "Any vet", the first performer is shown and the server chooses); all times labelled with the branch zone. **No slots**: *"No times left on {date}."* with the next day that has slots (fetched ahead one day at a time, up to 7) and **Join the waitlist** |
| — | Hold | Choosing a slot `POST`s the hold. `201` → step 5 with the countdown. `409 SlotNoLongerAvailable` → *"Someone has just taken that time. Please choose another."*, slots refetched, focus returned to the slot list |
| 5 | ⭐ `ConfirmStep` | Summary (cat, service, when, vet, room, branch, *"Pay at the clinic"*); **reason for visit** (optional, 500, plain text); `HoldCountdown`; **Confirm booking**. `400 SlotHoldExpired` → the countdown's expiry path |
| 6 | `BookingDone` | *"Booked."* with the details, *"We've sent a confirmation to your {preferred channel} and your inbox."*, links to the appointment and **Add to calendar** (a client-generated `.ics` from the confirmed data — text only, no library) |

⭐ `HoldCountdown` — `mm:ss` from `useServerNow()`; `role="timer"` with a separate `aria-live="polite"`
region announcing at 5 and 1 minute; at 0 → releases local state, refetches slots, returns to step 4
with the message (ADR-FE-025). **Back** from step 5 releases the hold (`DELETE`).

`WaitlistJoinDialog` — cat, service and date prefilled; optional preferred vet; explains *"If a time
frees up we'll send you a message. You'll have 30 minutes to claim it."* (the minutes come from
`claimWindowMinutes` in the join response); `SlotsStillAvailable`
→ *"Times have just become available — pick one below."* and the slots refetch.

### 11.2 My appointments — US-CLN-04, 09 · `/portal/appointments/**`

| Component | Behaviour |
|---|---|
| `MyAppointmentsList` | Tabs *Upcoming* / *Past*; each row: cat, service, when (branch zone), vet, branch, status as text; **Book another** link. A *Waitlist* section lists entries (`Waiting`, `Offered` with *"A time is available — claim by 10:45"*, `Lapsed`) with **Withdraw** |
| ⭐ `AppointmentDetail` | Details and status. When `canChange`: **Reschedule** (opens `RescheduleSlotPicker` — the `SlotStep` component for the same service, branch and cat, with this appointment's vet preselected — and posts `…/reschedule` directly, confirm *"Move to {new time}? Your current time will be released."*) and **Cancel** (confirm). When not: `ClinicContactCard` — *"It's less than {n} hours until your appointment, so it can't be changed online. Please call {branch} on {phone}."* with `tel:` and email links from `GET /api/branches` — **US-CLN-04 AC2** |
| `ClaimOfferedSlot` | When the detail is a `Held` waitlist offer: the slot, `HoldCountdown` to `holdExpiresOn`, **Confirm booking** (the confirm endpoint). Expired → *"This offer has ended."* |

### 11.3 Staff booking — US-CLN-07, 08 · `/staff/appointments/new`

`StaffBookingForm` — customer search (HO-ACC-002) with an **inline no-show badge** from the
appointment summary (*"2 no-shows"*, text) — **US-CLN-08 "visible at booking"**; cat (with the cat
header); service (all active priced services); date; vet (required for staff booking); slots from the
staff endpoint, where out-of-hours slots are marked *"Outside opening hours"*; optional room override
among free rooms of the kind; reason for visit. **Book** → confirm → detail page. Prefilled from
`?customerId&catId` (the cat page link).

### 11.4 Appointment detail and flagged list — staff

| Component | Behaviour |
|---|---|
| `StaffAppointmentDetail` | Cat header; customer (link) with no-show count; service snapshot incl. price; when, vet, room; status and **status history**; **needs rescheduling** banner with the reason (*"Dr Rahman is on leave that day"*); actions from `allowedTransitions` (ADR-FE-028) plus **Move…** and **Cancel** (reason required). No-show confirm names the customer |
| `FlaggedAppointmentsTable` | Future flagged appointments: when, cat, customer, phone, reason; **Reschedule** opens the Move dialog; **Cancel** |

### 11.5 Front desk — US-CLN-05, 08, 10 · `/staff/front-desk`

| Component | Behaviour |
|---|---|
| `TodayList` | Today's booked appointments at the active branch by time (`GET …/day`): cat, customer, service, vet, status; **Check in** (with an optional triage choice), **No-show** once the grace has passed (the button appears only when `allowedTransitions` includes it) |
| ⭐ `ClinicQueue` | Entries in the server's order; each card: position, priority badge (*Emergency* / *Urgent* / *Routine* as text), compact cat header, customer, service, walk-in or booked (*"Booked 10:15"*), checked in at, **"About 35 min"** estimated wait with a *"How is this worked out?"* disclosure explaining HO-APT-001 §8.7 in one sentence, status; actions from `allowedTransitions`: change priority, **Start** (walk-ins open `StartDialog` — performer and room of the right kind), **Complete**. A **My queue** toggle for vets (`performerId=me`). Header: *"{n} vets on shift · updated 10:42"* |
| ⭐ `WalkInPanel` | Keyboard-first, one panel (NFR-USA-2): customer search → *"New customer"* opens HO-ACC-002's walk-in customer form inline and returns the id → cat (select, or HO-CAT-002 quick-add) → service → priority (radio, **Routine** default) → presenting complaint → **Add to queue**. On success the panel resets with focus on the search field and the new card is highlighted |
| `StartDialog` | Performer (qualified, on shift) and room; the server's refusal shown in the dialog |

### 11.6 Calendar — US-CLN-07 · `/staff/appointments`

| Component | Behaviour |
|---|---|
| `CalendarToolbar` | Day / Week; **By vet** / **By room**; resource multi-select; prev / today / next; date label in the branch zone; **Flagged ({n})** link |
| ⭐ `CalendarGrid` | CSS grid: time rows on the payload's `gridMinutes`, one column per resource per day; off-shift time shaded **and** patterned (not colour alone); closures as labelled bands; appointments as blocks showing cat, service, status, flag and high-risk icons with text alternatives. Below 768 px → `CalendarAgenda` (a list by time) |
| ⭐ Drag and drop | Pointer-down on a block → drag ghost snapped to the grid and the resource column under the pointer → drop opens the move **confirm** stating old and new → `POST …/move` with `expectedVersion`. While pending the block shows *"Moving…"* at the target. `200` → committed, calendar refetched. `400` → snaps back, the server's specific reason shown in a toast **and** read by a live region (*"Dr Akter already has an appointment 11:30–12:00."*) — **US-CLN-07 AC2**. `409` → *"This appointment changed since you loaded the calendar."*, refetched |
| `MoveDialog` | The keyboard and small-screen path: date, vet, room, then time from the staff slots endpoint for that vet; same confirm and same error handling |
| `NewAppointmentFromGrid` | Clicking (or Enter on) an empty cell opens staff booking prefilled with vet, date and time |

### 11.7 Customer page panel — HO-ACC-002's page

`AppointmentSummaryPanel` — no-show count (text), upcoming appointments (links), last visit; **Book
appointment** → staff booking prefilled with the customer.

### 11.8 Error mapping — `lib/api/errors.ts`

Append entries for every HO-APT-001 command's `propertyName`s and map the §10.4 codes to the messages
used in §11 (`SlotNoLongerAvailable`, `SlotHoldExpired`, `ChangeCutoffPassed`, `PerformerBusy`,
`RoomBusy`, `CatAlreadyBooked`, `PerformerNotAvailable`, `PerformerNotQualified`, `RoomKindMismatch`,
`InvalidAppointmentTransition`, `NoShowTooEarly`, `CheckInNotToday`, `SlotsStillAvailable`,
`AlreadyWaitlisted`, `CatNotBookable`). The server's message is preferred when present; the mapped text
is the fallback.

---

## 12. Mocks — `web/mocks/handlers/appointments/*.ts`

Generated from HO-APT-001 §12.4. Fixtures: two branches in **different zones**; services vet-only and
vet-or-nurse, one unpriced (absent from the public list); a day with slots, a full day, a holiday; a
hold that expires in 70 s (to exercise the countdown quickly) and a clock-skewed `Date` header; `409`
on hold; confirm `SlotHoldExpired`; appointments before and after the cut-off; a `Held` waitlist offer;
waitlist entries in each status; a queue with every priority, walk-ins with and without a performer,
and changing estimates between polls; a calendar week with closures, off-shift time, flagged and
high-risk appointments; move refusals for each specific reason, `409` stale version; a customer with
no-shows; every `403`.

---

## 13. Allowed scope

```
web/app/(portal)/book/**
web/app/(portal)/portal/appointments/**
web/app/(portal)/layout.tsx                                   ← one nav entry only
web/app/(staff)/staff/front-desk/**
web/app/(staff)/staff/appointments/**
web/app/(staff)/staff/customers/[customerId]/page.tsx         ← AppointmentSummaryPanel only (HO-ACC-002's page)
web/app/(staff)/staff/cats/[catId]/page.tsx                   ← Book appointment link only (HO-CAT-002's page)
web/app/(staff)/layout.tsx                                    ← two nav entries only
web/components/{booking,appointments,front-desk,calendar}/**
web/lib/api/{booking,my-appointments,staff-appointments,front-desk}.ts
web/lib/api/client.ts                                         ← Date-header offset capture only
web/lib/api/types.ts · web/lib/api/errors.ts                  ← append
web/app/(auth)/sign-in/page.tsx                               ← `/book` on the next-param allow-list only (was `web/lib/routing/**`, which does not exist — corrected 2026-10-04, see §1 Amended)
web/components/portal/CustomerSessionGuard.tsx                ← `?next=` carries the query string only (HO-ACC-002's file — owner-approved 2026-10-04, see §1 Amended)
web/lib/format/**                                             ← §8.4 additions
web/lib/hooks/**                                              ← new hooks only (incl. useServerNow)
web/lib/calendar/ics.ts
web/lib/validation/**
web/mocks/handlers/appointments/** · web/mocks/handlers/index.ts
web/tests/components/{booking,appointments,front-desk,calendar}/**
web/tests/e2e/appointments-*.spec.ts
web/docs/decisions.md                                         ← ADR-FE-024…028
docs/test-cases/TC-HO-APT-002.md · docs/gates/HO-APT-002-*.md
```

## 14. Forbidden scope

`src/**`, `tests/**`, `.ai/**`, `.claude/**`, `docs/**` (except the two named paths), every other
HO-CAT-002, HO-ACC-002, HO-ADM-002 and HO-NTF-002 file beyond the lines in §13, `web/package.json`,
`tsconfig.json`, `next.config.ts`, `eslint.config.mjs`.

---

## 15. Component test cases

| ID | Case | Expected |
|---|---|---|
| U-A1 | Flow with one cat and one branch | Steps 1 and 3 skipped |
| U-A2 | Deep link `?catId&category=Vaccination` | Lands on the service step filtered to vaccination |
| U-A3 | Slots for a `Europe/London` branch viewed in `Asia/Dhaka` | Times in London time, labelled |
| U-A4 | Full day | Waitlist offer and the next day with slots |
| U-A5 | Hold `409` | Message; slots refetched; focus on the list |
| U-A6 | Countdown with a server clock 90 s ahead of the browser | Counts to the server's expiry, not the browser's |
| U-A7 | Countdown announcements | Live region at 5:00, 1:00 and 0:00 only |
| U-A8 | Countdown reaches 0 | Back to slots with the expiry message; confirm button gone |
| U-A9 | Back from confirm; route change | `DELETE` hold sent |
| U-A10 | Price shown with *"Pay at the clinic"*; no payment control anywhere in the flow | As stated |
| U-A11 | `.ics` download | Contains the summary, start and end in UTC, the branch address; no HTML |
| U-B1 | Detail with `canChange` true / false | Reschedule and cancel / the contact card with `tel:` link |
| U-B2 | Reschedule `409` | Message; current time unchanged in the UI |
| U-B3 | Waitlist offer detail | Countdown and confirm; expired → offer-ended text |
| U-C1 | Staff booking customer with no-shows | Badge with the count as text |
| U-C2 | Out-of-hours staff slot | Marked in text |
| U-D1 | Queue ordering and badges | As delivered by the server; priority as text |
| U-D2 | Poll while `StartDialog` is open with values | Values kept; data behind updated |
| U-D3 | `WalkInPanel` keyboard-only path | Completes with Tab/Enter/Space only; focus returns to search |
| U-D4 | Actions from `allowedTransitions` | Only those buttons rendered |
| U-E1 | Drag to a conflicting time; server `400 PerformerBusy` | Block snaps back; reason in a toast and a live region |
| U-E2 | Drag `409` stale | Stale message; refetch |
| U-E3 | Poll during a drag | Applied after the drop, not during |
| U-E4 | `MoveDialog` with keyboard | Same request as a drag |
| U-E5 | Calendar at 360 px | Agenda list, no horizontal scroll |
| U-E6 | Off-shift cells and closures | Distinguishable without colour |
| U-X1 | Every form: server `400` | Mapped to its field; `errorCode` not in the DOM |
| U-X2 | Dirty forms + navigate | Unsaved-changes confirm (the booking flow's confirm step included) |

## 16. End-to-end test cases

MSW first, then the real API; both runs recorded.

| ID | Case | Expected |
|---|---|---|
| E-A1 | **AC-1**: new customer registers (CAPTCHA), adds a cat, books a vaccination | Done page within the NFR-USA-1 budget measured by the test; the appointment in *Upcoming*; the confirmation in the inbox |
| E-A2 | Two browser contexts pick the same slot | One reaches confirm; the other sees *"Someone has just taken that time"* |
| E-A3 | Hold then wait past expiry (mock: 70 s) | Expiry message; slot selectable again |
| E-B1 | Reschedule before the cut-off; try after | Moved / contact card |
| E-B2 | Cancel | Status *Cancelled*; a cancellation message in the inbox (real API) |
| E-B3 | Full day → join waitlist → (real API) another customer cancels → clinic pass → claim from the inbox link | Booked |
| E-C1 | Reception books by phone for a customer with a no-show | Badge seen; booked |
| E-D1 | **NFR-USA-2**: register a walk-in for a new customer and queue it | Under 60 s measured by the test; card in the queue |
| E-D2 | Add an emergency | It goes first; later estimates rise on the next poll |
| E-D3 | Check in a booked cat; vet opens *My queue*; start; complete | Card moves through statuses |
| E-E1 | Drag an appointment to a free slot; to a busy one | Moved / snapped back with the reason |
| E-E2 | Approve a vet's leave (HO-ADM-002) | The appointment appears on *Flagged* with the reason; the banner on its detail |
| E-X1 | axe on every route in §8.1, including the countdown, a drag in progress (dialog path) and the calendar with errors | Zero serious/critical |
| E-X2 | Every route at 360 / 768 / 1024 / 1440 | No horizontal scroll, no clipped control |

---

## 17. Implementation order

1. Confirm prerequisites (§5) — else stop.
2. Types and API modules from HO-APT-001 §12.4; MSW handlers; `lib/format` additions and `useServerNow`.
3. Booking flow with countdown → my appointments → reschedule/cancel → waitlist and claim.
4. Front desk: today list, queue, walk-in panel.
5. Staff booking → appointment detail → flagged list → customer panel and cat link.
6. Calendar grid → drag and drop → Move dialog → agenda view.
7. U-series, E-series on MSW, then E-series against the real API per HO-APT-001 phase.

```bash
cd web && npm run lint && npm run typecheck && npm run test && npm run test:e2e && npm run build
```

---

## 18. Acceptance criteria

| # | Story | Criterion | Evidence |
|---|---|---|---|
| A1 | CLN-02 | Customers see only valid slots by service, date and vet, in the branch's time | U-A3, U-A4, E-A1 |
| A2 | CLN-03 AC1 | A held slot shows an honest countdown | U-A6, U-A7 |
| A3 | CLN-03 AC2 | On expiry the customer is told to choose again | U-A8, E-A3 |
| A4 | CLN-03 AC3 | A lost race is explained and recovers | U-A5, E-A2 |
| A5 | CLN-04 | Reschedule and cancel before the cut-off; contact details after | U-B1, U-B2, E-B1, E-B2 |
| A6 | CLN-05 | Walk-ins are queued with triage in under 60 s; estimated waits shown and refreshed | U-D1…U-D3, E-D1, E-D2 |
| A7 | CLN-07 | Calendar per vet and room; moves re-validated by the server with specific reasons; keyboard alternative | U-E1…U-E6, E-E1 |
| A8 | CLN-08 | No-show count visible at booking and on the customer | U-C1, E-C1 |
| A9 | CLN-09 | Join a waitlist and claim an offered slot | U-B3, E-B3 |
| A10 | CLN-10 | Check-in puts the cat on the vet's queue; only valid actions are offered | U-D4, E-D3 |
| A11 | ADM-03 AC2 | Flagged appointments are visible and actionable | E-E2 |
| A12 | ADR-073 | No payment step exists | U-A10 |
| A13 | AC-1 / NFR-USA-1 | A new customer books end to end within the budget | E-A1 |
| A14 | UI-6 / UI-1 | Zero serious/critical axe violations; 360–1440 px | E-X1, E-X2 |

---

## 19. Required gates

| # | Gate | Focus |
|---|---|---|
| 1 | ARCHITECTURE | §8 layering; no new dependency; no client-side availability or conflict logic; the scoped edits to HO-ACC-002, HO-CAT-002 and the layouts match §13 |
| 2 | SECURITY | The `/book` next-param entry keeps the open-redirect guard; the hold never persisted; reason-for-visit rendered as text; actions only from `allowedTransitions`; `.ics` generation escapes its fields |
| 3 | CODE_REVIEW | Time only through `lib/format` and `useServerNow`; polling never resets forms or drags; component size |
| 4 | TEST | `TC-HO-APT-002.md` first; mock and real runs separate; E-A1 and E-D1 measure time |
| 5 | ACCEPTANCE | Every §18 row |

---

## 20. BLOCKED conditions — stop and report

**20.1 A prerequisite is missing** (Node, HO-CAT-002 scaffold, HO-ACC-002, HO-ADM-002, HO-NTF-002). Do
not build a parallel shell, customer search or walk-in form.

**20.2 A screen seems to need client-side availability or conflict checks.** Ask the server; do not
compute it.

**20.3 A payment or deposit step is requested.** It contradicts the owner's instruction (ADR-073);
report it.

**20.4 The real API disagrees with HO-APT-001 §12.4.** Report it; do not shim.

**20.5 A screen needs an endpoint HO-APT-001 does not specify.** Report it; do not invent a mock field.

**20.6 A new dependency seems necessary** (§10). Raise it.
