# Customer Portal — UX

The clickable design for backlog §15 *Customer Portal*, built against
[`HO-POR-002`](../../handover/HO-POR-002.md), the frontend handover, with
[`HO-POR-001`](../../handover/HO-POR-001.md) §12.4 as the data contract. It follows
[Cat Profiles](../cat-profile/README.md), [Accounts & Identity](../accounts-identity/README.md),
[Administration](../administration/README.md) and [Notifications](../notifications/README.md).

## Where to review it

Review locally: http://127.0.0.1:8080/portal/index.html (served from docs/design)

There is no hosted canvas for this area. `index.html` lists every screen with a one-line description.

## Where the source is

The `.dc.html` files in this folder are the source of truth. Each one loads `./support.js`, the local
runtime copied unchanged from `../_shared/`, so it opens from a static server.

## The screens

Four rows, fourteen artboards. The handover's scope is small: one page (the portal home) and one
action (re-order). Most of the boards show states. The stay diary (US-POR-03) is **not** here: README
C-6 moved it to HO-BRD-002, and the stays tile links to Boarding's diary page.

| Row | File | Screen | Shows |
|---|---|---|---|
| Portal home — US-POR-01 | `Dashboard.dc.html` | Portal home | ⭐ All eight tiles on one screen (E-A2). Each tile summarises its area and links to that area's page. There is no pay button |
| | `DashboardNewCustomer.dc.html` | New customer | The no-cats invitation with **Add a cat**. Every other tile states its true empty in words (U-A1, E-A1) |
| | `DashboardDegraded.dc.html` | Sections unavailable | ⭐ Balances and visits are `Unavailable`. Each is explained with **Try again**, and neither shows as empty or zero (U-A3, E-A3) |
| | `DashboardLoading.dc.html` | Loading | Fixed-height placeholders, so there is no layout shift (U-A9) |
| Tile states — US-POR-01 | `CatTileStates.dc.html` | Cats tile | ⭐ Due soon, Overdue + high risk, Current (no Book now), vaccination status unavailable, 13 cats (`hasMore`), and a cats section that is unavailable and shows **no** invitation (U-A2, U-A5, U-A6, U-A8) |
| | `BalancesStates.dc.html` | Balances tile | BDT and USD shown separately with no total, how to pay offline and branch contacts, Nothing to pay, and unavailable (U-A3, U-A4) |
| | `TileStates.dc.html` | Other tiles | An appointment in a Europe/London branch labelled with that zone (U-A7), stays with and without a diary link, and a suspended subscription explained in text (U-A11) |
| Re-order — US-POR-02 | `ReorderButtonStates.dc.html` | Re-order button | Ready, busy (U-B1), disabled with the reason while awaiting payment, and the button on HO-ORD-002's order detail page |
| | `ReorderAllAdded.dc.html` | All added | Result dialog over the dashboard with **View basket** (E-B1) |
| | `ReorderMixed.dc.html` | Mixed result | ⭐ Added and Not added lists that cover all ten outcomes, with a reduced quantity and a price change noted (U-B2, E-B2) |
| | `ReorderNothingAdded.dc.html` | Nothing added | *"Nothing could be added"* with View basket still offered (U-B3). A second re-order of the same order shows "Already in your basket" on every line (E-B3) |
| | `ReorderErrors.dc.html` | Request failures | Plain messages for order not found, not re-orderable, and basket changed. No code reaches the page (U-B4) |
| Narrow width — UI-1 | `DashboardMobile360.dc.html` | Home at 360 px | Tiles stacked, nav behind Menu, inbox badge kept, no horizontal scroll (E-X2) |
| | `ReorderMobile360.dc.html` | Re-order at 360 px | The result dialog at phone width |

## The design system

This area uses the PurrCare tokens (surface, ink, accent, warning, danger, Newsreader and Public
Sans) hand-transcribed into each artboard, as the other areas do. No new component was added.

- **Customer header.** Plain `PurrCare` brand, portal nav with **Home** first, the inbox icon with
  its unread badge, avatar, name and Sign out. It combines the headers of Accounts & Identity,
  Notifications and Appointments, so the portal reads as one app.
- **Badge** carries every status as an icon plus a word (Due soon, Overdue, Current, Booked, Checked
  in, Awaiting payment, Delivered, Cancelled, Open).
- **Dialog** is used for the re-order result, with the `shadow-modal` exception Administration
  recorded.
- **Tile.** A card with a title and a "go to the owning page" link. It is the design-side shape of
  the handover's shared `DashboardSection`.

**Customer layer** (added 25 Sep 2026). Customer screens add the warmer PurrCare customer tokens
on top of the usual set: `--surface-page:#fbf6ef`, `--warm:#e8894a` (fill and decoration only,
never text), `--warm-ink:#8a3f14` (7.5:1 on white), `--warm-soft:#fdeee2`, and `--radius-md` of
14px. Teal stays the colour for actions, and staff screens are unchanged. The dashboards open with
a warm greeting band showing the cats’ avatars and what needs doing next, and each cat in *My
cats* has a round avatar. `DashboardNewCustomer` uses the line-art cat in place of the generic
icon. `BalancesStates` keeps the neutral tokens because it is financial.

## Rules the design encodes

1. **An unread section is never shown as empty.** `Unavailable` shows as a message with Try again,
   never "No cats", "Nothing to pay" or "0" (ADR-260, ADR-FE-012, ADR-FE-120).
2. **The no-cats invitation appears only when the cats section was read and is empty.**
3. **No payment surface.** Balances show what is owed per currency and how to pay offline (cash,
   in-store card, bank transfer). The only links go to invoices (SRS §1.2, ADR-FE-122).
4. **Money is never summed across currencies.** BDT and USD appear as two amounts, each with its
   unit next to it.
5. **Times name their zone.** Appointment times are in the branch's IANA zone and labelled, for
   example "(Asia/Dhaka)" or "(Europe/London)".
6. **Status is never colour alone.** Every badge has an icon and a word.
7. **A disabled control says why, next to the control.** Re-order on an order awaiting payment and
   Book now on a Current cat both do this.
8. **Health warnings travel with the cat.** The high-risk flag shows on the cat row. Allergies are
   not in the dashboard response (ADR-262), so each cat row links to the record where the allergy
   banner lives.
9. **No clinical content on the dashboard.** Visits show only the date, service, vet and whether a
   summary exists. Prescriptions show a count and an end date, never a medicine or dose.
10. **Re-order is decided by the server.** The dialog reports each line's outcome in plain words.
    Nothing is guessed on the client, and a repeat re-order never duplicates the basket.

## Known gaps, deliberately visible

- **The handover file is truncated.** `HO-POR-002.md` (committed at `529ff4f`) jumps from §2 to
  §11.5. §3.2's screen table, §8.5 (links) and §11.1–11.4 (including the exact §11.2 error
  messages) are missing. The boards are derived from §2, §12's fixtures, §15/§16's test cases, §18
  and HO-POR-001. The three error messages and the ten outcome messages are proposed wording.
- **Cross-area links point to assumed file names.** Those areas are being designed in parallel:
  `../appointments/MyAppointments.dc.html`, `../appointments/BookService.dc.html`,
  `../boarding/MyStays.dc.html`, `../boarding/PortalDiary.dc.html`, `../orders/MyOrders.dc.html`,
  `../orders/OrderDetail.dc.html`, `../orders/Cart.dc.html`, `../shop/Catalogue.dc.html`,
  `../shop/ProductPage.dc.html`, `../billing/MyBilling.dc.html`,
  `../consultation/PortalVisitSummary.dc.html`, `../pharmacy/MyPrescriptions.dc.html` and
  `../subscriptions/MySubscriptions.dc.html`.
- **No allergy indicator on the dashboard.** Adding one needs a new field such as `hasAllergies`;
  the handover (§20.2) forbids inventing it.
- **No cat photo.** Photos are not servable yet (OQ-10).
- **No live interaction.** Each state is its own artboard.
