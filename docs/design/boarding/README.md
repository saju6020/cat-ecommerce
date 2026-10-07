# Boarding — UX

The clickable design for backlog §7 *Boarding — Cat Care Centre*, built against
[`HO-BRD-002`](../../handover/HO-BRD-002.md), the frontend handover. Fifth area, after
[Cat Profiles](../cat-profile/README.md), [Accounts & Identity](../accounts-identity/README.md),
[Administration](../administration/README.md) and [Notifications](../notifications/README.md).

## Where to review it

Review locally: http://127.0.0.1:8080/boarding/index.html (served from docs/design)

There is no hosted canvas for this area yet.

## Where the source is

The `.dc.html` files in this folder are the **source of truth**. Each loads `./support.js`, a copy of
[`_shared/support.js`](../_shared/support.js) — the local runtime that renders the artboards and
replaces each staff screen's `.topnav` with the shared collapsible sidebar and header. Customer
screens keep the plain `PurrCare` bar they were drawn with.

## The screens

Seven rows, thirty-three artboards — the largest area so far, proportional to HO-BRD-002, which
covers 21 stories and is two products over one API (§2). The rows follow the handover's §3.2 table.

| Row | File | Screen | Shows |
|---|---|---|---|
| Booking flow (owner) — US-BRD-02…07 | `BookingResults.dc.html` | Dates and unit types | ⭐ Stay search with windows and nights; per-type itemised quote with the discount as its own line; a too-small type disabled with why |
| | `BookingFullWaitlist.dc.html` | No units free | Three nearest alternatives; the waitlist dialog ("first come, first served") |
| | `BookingCats.dc.html` | Choose cats | Over capacity — the capacity stated, Continue disabled with the reason |
| | `BookingVaccinationBlock.dc.html` | Vaccination block | ⭐ Mittens blocked (rabies overdue), Book a vaccination, "We've kept your booking — Resume" |
| | `BookingCareProfile.dc.html` | Care profiles | ⭐ Per-cat tabs, completeness badges, prefilled from the profile, two field errors with values kept |
| | `BookingReviewConfirm.dc.html` | Review and reserve | The UI-3 confirm sentence naming unit, cats, dates, total and deposit deadline |
| | `BookingReserved.dc.html` | Reserved | ⭐ "Not yet confirmed", deposit, countdown, how to pay offline |
| | `BookingMobile360.dc.html` | Gate at 360px | Single column, 44px targets |
| My stays (owner) — US-BRD-04, 06, 12–14, 17 | `MyStays.dc.html` | My stays | Enquiry with Resume, empty tab written out, waitlist with Withdraw |
| | `PortalStayOverview.dc.html` | Stay overview | The three deposit-state wordings; cancel confirm stating the deposit outcome |
| | `PortalDiary.dc.html` | Diary | ⭐ Days newest first, media placeholder, empty day |
| | `PortalIncidentApproval.dc.html` | Incident + vet approval | Acknowledge and its after-state; limit / used / remaining; Approve confirm |
| | `PortalSummary.dc.html` | Summary and charges | Weight change, per-cat record, recalculated charges ending in a credit |
| Front desk (staff) — US-BRD-04, 06, 19 | `BoardingDashboard.dc.html` | Dashboard | ⭐ All eleven sections, each with a count, link and empty state; live-region incident |
| | `StayList.dc.html` | Stays | Status tabs; every flag written out |
| | `StaffBookingOverride.dc.html` | New stay — override | Administrator override: mandatory reason, "recorded against you" |
| | `StayWorkspace.dc.html` | Deposit and confirm | ⭐ Record in Billing → Confirm; `DepositNotYetReceived`; pending-approval and confirmed variants |
| Check-in / check-out (staff) — US-BRD-08, 16 | `CheckInVaccinationHold.dc.html` | Vaccination hold | ⭐ Overdue rabies holds check-in even after a booking override |
| | `CheckInBelongingsSignature.dc.html` | Belongings + signature | No-upload note; pad and typed-name path side by side |
| | `CheckInIncomplete.dc.html` | Check-in incomplete | Server's missing items mapped to cat and step; nothing half-saved |
| | `CheckOutEarly.dc.html` | Early check-out | ⭐ Recalculation and credit shown before submit; unreturned item needs a note |
| Care (staff) — US-BRD-09…11, 13 | `TaskBoard.dc.html` | Task board | ⭐ One-tap Done, overdue chip, in-flight Undo, rolled-back failure, cat headers first |
| | `ObservationSheet.dc.html` | Observation + skip | "Not eating — flagged" from the server; skip needs a reason |
| | `IncidentQueue.dc.html` | Incident queue | Escalated chip, Call owner, Record call surviving a refresh |
| | `IncidentFormResolve.dc.html` | Record / resolve | "Medium and high message the owner immediately"; resolution required |
| Board and changes (staff) — US-BRD-02, 14, 15, 18, 20 | `OccupancyBoard.dc.html` | Occupancy board | ⭐ Units × 14 nights, patterned maintenance and turnaround, day list below 768px |
| | `ExtendRelocate.dc.html` | Extend / relocate | Relocation options from the server; reason required; confirm names both units |
| | `IsolationPanel.dc.html` | Isolation | Confirm names the move; entry log; no isolation unit free |
| | `ReferralCharges.dc.html` | Referral and charges | Headroom as text, charge lines, final invoice after check-out, cycle invoice |
| Admin and daycare — US-BRD-01, 03, 09, 21 | `AdminUnits.dc.html` | Units | Nightly rate read-only from the rate card; `MaintenanceOverlapsStays` |
| | `AdminRateCards.dc.html` | Rate cards + routine | Scheduled / Current / Past, no edit; backdating refused; daycare card; routine |
| | `DaycareBooking.dc.html` | Book daycare | Both prices; not offered / full / vaccination-blocked states |
| | `DaycareDayList.dc.html` | Daycare day list | Check in/out, no-show, cancel; charge after check-out |

## The design system

Built on the PurrCare design system as the four earlier areas use it — the same tokens, hand-copied
into each artboard's `:root`, and the same components: `Table`, `Tabs`, `Dialog` (with the one
`shadow-modal` exception), `Badge`, `Field`. Nothing new was added to the system. Patterns this area
leans on hardest:

- **Compact `CatProfileHeader`** (UI-7) — avatar, name, high-risk badge with the handling note, and
  allergy chips, repeated on every task card group, incident, diary, dialog and stay header that is
  about a cat.
- **Badge** with an icon *and* a word for every state — deposit, gate, severity, overdue, status.
- **Side-by-side state panels** labelled in small caps ("Variant · …", "After … — the confirmation")
  wherever one screen has several acceptance-critical states.

## Rules the design encodes

1. **No payment control anywhere** (ADR-FE-106). The owner sees the amount, the pay-by time and how
   to pay offline (cash or card at the front desk, bank transfer). Staff record payments in Billing
   and then press Confirm stay; the UI never calls a stay confirmed before the server does.
2. **An overdue core vaccination blocks boarding and says so** (BR-1). At booking the gate names the
   vaccine and date and links to booking one; at check-in the hold stands even when an administrator
   overrode the gate at booking — the override reserves a unit, it doesn't admit a cat.
3. **A cat's high-risk warning and allergies are visible on every screen about that cat.**
4. **The caretaker's budget is one tap** (NFR-USA-3). Done is a 48px button, no dialog, no note;
   Undo only while the save is in flight; a failure rolls the card back with the server's reason.
5. **The server decides money and state.** Quotes, discounts, recalculations, credits, overdue and
   "not eating" are shown as the server's result, never derived in the browser (ADR-FE-105).
6. **Confirmations name the consequence** — reserve, cancel (deposit outcome), override, flag
   infectious (the move), relocate (both units), approve (the charge), skip, resolve.
7. **Disabled controls say why**, and missing data is written out ("No allergies recorded", "No
   entries yet for this day", "No refunds due").
8. **Every time names its zone** (Asia/Dhaka); units sit next to numbers ("4.2 kg", "৳2,000 / night").
9. **Nothing uploads** (ADR-FE-104). Photo fields are references with an honest note.
10. **The board never scrolls sideways at 360px** — it becomes a day list (UI-1, ADR-FE-102).

## Known gaps, deliberately visible

- **No live interaction.** Every state is its own artboard or a labelled side panel.
- **Owners were reconciled for this area.** Cat Profiles shows Mittens owned by Nusrat Jahan and
  Biscuit by Rakib Hasan; here Farhana Yasmin, the established customer, owns Mittens and Biscuit
  (plus a third cat, Mishti, for the capacity case). Noodle stays with Farhana Akter and Pepper with
  Imran Chowdhury, as in Cat Profiles.
- **The storyline spans dates.** Booking happens in late September (Mittens blocked), the stay runs
  9–23 Oct after her 2 Oct vaccination, staff screens are dated Mon 12 Oct, and check-out is an early
  one on 19 Oct. Pepper is the check-in hold, because her rabies expires on 09 Oct.
- **Other areas' screens are stand-ins.** Billing's record-payment dialog (HO-BIL-002), the
  consultation screen (HO-EMR-002) and house-food issue (HO-IMP-002) aren't written; these designs
  show the handover's interim path — a link to Billing with the reference, "listed here only", an
  empty state.
- **Prices, units and people are illustrative** — no backend exists yet for this area.
- **No calendar, date-picker or signature library** is drawn as a real component (§10); date fields
  are plain inputs and the signature pad is a simple drawn box.
