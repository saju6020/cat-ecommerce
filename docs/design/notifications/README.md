# Notifications — UX

The clickable design for backlog §13 *Notifications*, built against
[`HO-NTF-002`](../../handover/HO-NTF-002.md), the frontend handover. Fourth area, after
[Cat Profiles](../cat-profile/README.md), [Accounts & Identity](../accounts-identity/README.md) and
[Administration](../administration/README.md).

## Where to review it

**Clickable canvas:** <https://claude.ai/artifact/Thpo6bEandj3nJRXGWD2As>

That is the version to look at — nine artboards, four rows, matching HO-NTF-002 §3.2's screen
table. It is private to the owner; others need it shared from the page's Share menu.

If the link is ever lost: `/artifacts` in the Claude Code terminal lists artifacts you own (`o`
opens, `c` copies the link), or browse <https://claude.ai/code/artifacts>.

## Where the source is

The `.dc.html` files in this folder are the **source of truth**, committed so the design survives
independently of the hosted link. They do not open standalone in a browser — each one loads
`./support.js` from the canvas runtime, which is not in this repo. Read them as source; view them
on the canvas.

## The screens

Four rows, nine artboards — the smallest area so far, proportionate to the backlog: two of
Notifications' six stories (US-NTF-03, 05) have no screen of their own, and the admin surface here
is one editor, not a whole section.

| Row | File | Screen | Shows |
|---|---|---|---|
| Inbox & preferred channel — US-NTF-01 | `InboxAndBadge.dc.html` | Portal inbox | ⭐ the unread `Badge` Count variant in the header, unread/read rows, one item expanded to its plain-text body |
| | `PreferredChannel.dc.html` | Account → Notifications | ⭐ the channel radio group above HO-ACC-002's preference matrix, in the **same** form and Save — a channel with no destination shown disabled with a reason and a link to fix it |
| Message templates — US-NTF-02 | `TemplateList.dc.html` | Template list | Transactional vs Marketing, Customised vs Default |
| | `TemplateEditor.dc.html` | Template editor | ⭐ channel tabs, the variable palette, the sandboxed email preview, and the save confirmation's exact wording |
| | `TemplatePreviewStates.dc.html` | Preview states | The SMS segment/Unicode counter, the in-app preview mirroring `InboxItem`, and the inline named-variable error shown on the panel **and** the field, without blocking typing |
| Unsubscribe — US-NTF-04 | `Unsubscribe.dc.html` | Public unsubscribe | Three states side by side: the confirm (nothing sent on load), success, and the invalid-link state |
| Notification log & resend — US-NTF-06 | `NotificationLog.dc.html` | Staff notifications | ⭐ the queue summary tiles and the "SMS not connected" warning as a status banner, not an interrupting alert; the required date-range filter |
| | `NotificationLogMobile.dc.html` | Log at 360px | The same rows as cards below 768px (UI-1) |
| | `NotificationDetail.dc.html` | Detail & resend | ⭐ the redaction notice, the attempts table, the chain links, and the resend dialog — shown alongside the disabled Resend button and its reason for a message that can't be resent |

## The design system

Built on the [PurrCare design system](https://claude.ai/artifact/Y2WzXo2hnfc7a9ac7GHNvN). This
area needed one small, clearly reusable addition rather than a new component: **`Badge` gained a
Count variant** — a number on a nav icon, for the inbox's unread badge — documented as the one
deliberate exception to "icon and word both," because it sits on an icon that already carries its
own accessible name. Everything else in this canvas is built from the system's existing nine
pieces: `Tabs` for the channel tabs, `Table` for the log, `Dialog` for resend and save
confirmations, `Banner`'s shape for the SMS warning status line, `Field` for every filter and
form.

As with the prior two areas, token values are hand-transcribed into each artboard rather than
installed live on the canvas.

## Rules the design encodes

1. **Message content is always plain text.** `InboxAndBadge.dc.html` and `NotificationLog.dc.html`
   never render HTML for a body or subject — the one exception, `TemplateEditor.dc.html`'s email
   preview, is sandboxed and shown nowhere else (ADR-FE-020, ADR-FE-022).
2. **The client never renders a template.** `TemplatePreviewStates.dc.html`'s preview panel is
   drawn as if it came back from the server — the design deliberately doesn't show any
   client-side `{{variable}}` substitution logic, because there isn't any (ADR-FE-023).
3. **A resend states its destination and channel before it happens.** The dialog in
   `NotificationDetail.dc.html` names both; a message that can't be resent shows why, in a
   sentence, instead of hiding the button.
4. **A provider outage reads as a status, not a blocking error.** The SMS warning in
   `NotificationLog.dc.html` is a banner the page carries, not a dialog someone has to dismiss —
   the work underneath it is still usable.
5. **The unsubscribe page never acts on load.** `Unsubscribe.dc.html`'s first panel is a question,
   not a confirmation — the token is read and the address bar cleared, but nothing is submitted
   until the person clicks (ADR-FE-021).
6. **What can't be shown is not silently missing.** The redacted password-reset body in
   `NotificationDetail.dc.html` says why it's gone, matching the "None recorded" principle from
   the first canvas.

## Known gaps, deliberately visible

- **No live interaction**, same as the prior two areas — every state is its own artboard.
- **No SMS or email provider is connected**, which the design shows as a fact
  (`NotificationLog.dc.html`'s warning banner) rather than hiding — matching HO-NTF-001 OQ-1, still
  open.
- **The design system isn't installed live on the canvas** — see above.
- **Template, log and inbox data are illustrative**, not real API responses — no backend exists
  yet for this area to call.
