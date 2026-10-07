# Appointments — UX

The clickable design for backlog §3 *Clinic — Appointments* (US-CLN-01 … US-CLN-10), built against
[`HO-APT-002`](../../handover/HO-APT-002.md), the frontend handover. Fifth area, after
[Cat Profiles](../cat-profile/README.md), [Accounts & Identity](../accounts-identity/README.md),
[Administration](../administration/README.md) and [Notifications](../notifications/README.md).

## Where to review it

Review locally: http://127.0.0.1:8080/appointments/index.html (served from docs/design)

There is no hosted canvas for this area yet. The index page links every artboard with a one-line
description of what it shows.

## Where the source is

The `.dc.html` files in this folder are the **source of truth**. Each loads `./support.js` (a copy
of `docs/design/_shared/support.js`), which renders the markup, swaps the placeholder staff top bar
for the shared sidebar and header, and makes the artboard fluid. `index.css` is the shared index
stylesheet. Neither is edited here.

## The screens

Five rows, twenty-four artboards — at the top of the range so far, because this handover covers
ten stories across two audiences (customers booking online, reception running the day) and has
more acceptance-critical states than any earlier area: a countdown, a lost race, a waitlist offer,
a cut-off, a queue, a drag with server refusals. US-CLN-01 (the service editor) and US-CLN-06
(messages in the inbox) have no screen of their own, as HO-APT-002 §3.2 says.

| Row | File | Screen | Shows |
|---|---|---|---|
| Online booking — US-CLN-02, 03, 09 | `BookService.dc.html` | Service step | Deep link from a vaccination reminder: cat step skipped, list filtered to Vaccination, each price marked "Pay at the clinic" |
| | `BookSlots.dc.html` | Date & time step | ⭐ 14-day strip with closed days written out, vet filter ("Any vet" default), slot buttons grouped morning/afternoon/evening, the branch zone stated once and on every group |
| | `BookSlotStates.dc.html` | Slot step states | Four panels: lost race (409 → message, refreshed list, focus on the list), hold ended, full day (next day with times + Join the waitlist), and the leave-and-release confirmation (UI-3) |
| | `WaitlistJoin.dc.html` | Join the waitlist | Prefilled dialog, optional preferred vet, the 30-minute claim window; beside it the `SlotsStillAvailable` and already-waitlisted outcomes |
| | `BookConfirm.dc.html` | Confirm | ⭐ Summary, optional reason for visit (500, plain text), the hold countdown and its three announcements; no payment control anywhere |
| | `BookDone.dc.html` | Booked | Details, where the confirmation went, View appointment, Add to calendar (.ics), the cut-off rule |
| | `BookMobile360.dc.html` | Booking at 360px | The slot step single-column: a 7-day grid, full-width slot buttons, no horizontal scroll (UI-1) |
| My appointments & waitlist — US-CLN-04, 06, 09 | `MyAppointments.dc.html` | My appointments | Upcoming with status in words (one already past its cut-off), the waitlist section with Offered / Waiting / Lapsed and Withdraw |
| | `MyAppointmentDetail.dc.html` | Detail — cancel | Reschedule and Cancel while `canChange`; the cancel confirmation lists its consequences |
| | `MyAppointmentReschedule.dc.html` | Reschedule | The slot picker with this appointment's vet preselected, "Move to …? Your current time will be released.", and the 409 path that leaves the current time unchanged (U-B2) |
| | `MyAppointmentLocked.dc.html` | Detail — after the cut-off | ⭐ No change buttons at all; the clinic contact card with `tel:` and email links (US-CLN-04 AC2) |
| | `ClaimOffer.dc.html` | Claim an offered time | The waitlist offer with its countdown and Confirm booking, beside "This offer has ended." |
| Front desk — US-CLN-05, 08, 10 | `FrontDeskToday.dc.html` | Today | The day's bookings; Check in with a triage choice; No-show only after the 15-minute grace, and before it a note saying when; the no-show confirmation "This counts against …" |
| | `FrontDeskQueue.dc.html` | Queue & walk-in | ⭐ Cards in the server's order, priority as text, compact cat header, walk-in vs "Booked 10:15", "About 35 min" with its one-sentence explanation open; the keyboard-first walk-in panel with the customer's no-show badge |
| | `FrontDeskStart.dc.html` | Start a walk-in | `StartDialog`: vet and room, the server's refusal inside the dialog and on the field, values kept across a refresh |
| Calendar — US-CLN-07 | `Calendar.dc.html` | Day by vet | ⭐ In-repo CSS grid, off-shift and leave striped **and** labelled, the closure as a labelled band, flag and high-risk markers in words, Flagged (2) link |
| | `CalendarMoveConfirm.dc.html` | Drop and confirm | The dropped block at its target, the source left as an outline, the confirmation stating old and new, the "Moving…" pending state |
| | `CalendarMoveRefused.dc.html` | Move refused, by room | ⭐ Snapped back with "Not moved", the server's reason in a toast and live region (US-CLN-07 AC2), the stale-version 409, other refusal reasons |
| | `MoveDialog.dc.html` | Move… dialog | The keyboard and small-screen path: date, vet, room, then free times from the server; same confirmation as a drag |
| | `CalendarAgenda360.dc.html` | Agenda at 360px | The grid as a time-ordered list below 768px, closures as text rows, Move… on every item |
| Staff booking, detail & flags — US-CLN-07, 08, ADM-03 | `StaffBooking.dc.html` | Staff booking | ⭐ "2 no-shows" at the point of booking (US-CLN-08), the full cat header (UI-7), vet required, out-of-hours times marked "Outside opening hours" |
| | `StaffAppointmentDetail.dc.html` | Appointment detail | Needs-rescheduling banner with its reason, status history, only the server-allowed actions, the cancel dialog's required-reason error |
| | `FlaggedAppointments.dc.html` | Flagged list | Future flagged appointments with phone and reason; Reschedule opens Move…; the empty state |
| | `CustomerAppointmentsPanel.dc.html` | Customer page panel | The Appointments panel added to HO-ACC-002's customer page: no-show count, upcoming, last visit, Book appointment |

## The design system

Built on the PurrCare design system, with the same hand-transcribed tokens as the earlier areas
(`--accent #15605a`, warning and danger pairs, Newsreader for headings, Public Sans for text). No new
component was added. This area uses the existing pieces — `Tabs` (front desk, my appointments),
`Table` (Today, flagged list), `Dialog` (every confirmation), `Badge` (status, priority, no-show
count, all with a word), `Field` (every form), `Banner` (needs rescheduling, the contact card) —
plus three area-specific layouts that stay in this folder rather than the system:

- **Slot buttons** — a labelled group of real buttons, never a canvas.
- **Hold countdown** — a large `mm:ss` timer, amber with an icon under one minute.
- **Calendar grid** — a CSS grid whose off-shift, leave and closure fills are each a different
  stripe *and* a written label.

**Customer layer** (added 25 Sep 2026). Customer screens add the warmer PurrCare customer tokens
on top of the usual set: `--surface-page:#fbf6ef`, `--warm:#e8894a` (fill and decoration only,
never text), `--warm-ink:#8a3f14` (7.5:1 on white), `--warm-soft:#fdeee2`, and `--radius-md` of
14px. Teal stays the colour for actions, and staff screens are unchanged. Booking and *My
appointments* screens take the tokens only.

## Rules the design encodes

1. **Time belongs to the branch.** Every time and date names Asia/Dhaka or sits under a line that
   does — including for a customer whose phone is elsewhere (ADR-FE-014, U-A3).
2. **The server decides.** Slots, conflicts and allowed actions are drawn as if they came from the
   API: no screen implies the page worked out availability, and action buttons appear only where
   `allowedTransitions` / `canChange` allow (ADR-FE-028). A missing action is explained, not
   left as a dead button (`MyAppointmentLocked`, `FrontDeskToday`'s "No-show from 10:45").
3. **The countdown is honest.** It runs on the server's clock, is announced three times only, and
   its end sends the customer back to the slot list with a sentence, not a dead page (ADR-FE-025).
4. **No payment step.** Prices appear for information with "Pay at the clinic"; the only payment
   words anywhere are cash, card and bank transfer at the clinic (ADR-073, U-A10).
5. **Confirmations name the consequence** — cancel, reschedule, move ("from 10:30 with Dr Rahman to
   12:30 with Dr Jahan"), no-show ("This counts against Rahim Uddin"), and leaving a held time.
6. **Status and priority are never colour alone.** Every pill has a word and most an icon;
   off-shift, leave and closures differ by pattern and label (UI-6, U-E6).
7. **Every drag has a keyboard path.** `MoveDialog` is the same request as a drag, and at 360px it
   is the only path (ADR-FE-026).
8. **Missing data is written out** — "None recorded", "No weight recorded", "No appointments in
   Theatre", "None left in the evening".

## Handover ambiguities resolved here

- **Hold length and claim window** come from HO-APT-001 (`booking.slotHoldMinutes` 10,
  `booking.waitlistClaimMinutes` 30, `booking.changeCutoffHours` 4, `clinic.noShowGraceMinutes`
  15); slot times sit on its 15-minute grid.
- **Reschedule 409 (U-B2)** reuses the booking flow's "Someone has just taken that time" wording and
  adds that the current appointment is unchanged.
- **The "My queue" toggle** is for vets; the queue artboard is drawn as reception, so it is noted
  rather than shown.
- **Room view refusals**: the refused move is drawn in By room view to show that the server still
  catches a vet clash there.

## Known gaps, deliberately visible

- **No live interaction.** Every state is its own artboard or a side-by-side panel; the drag itself
  is shown as its before (drop + confirm) and after (refused) states.
- **No week view artboard.** The toolbar shows Week; its layout is the same grid with one column
  per resource per day and is not drawn separately.
- **The walk-in panel's inline "New customer" form and cat quick-add** belong to HO-ACC-002 and
  HO-CAT-002 and are referenced, not redrawn.
- **Data is illustrative**, not real API responses — no backend exists yet for this area.
- **Date fields are plain text inputs** standing in for whatever in-repo control the build settles
  on, because HO-APT-002 §10 forbids a date-picker library.
