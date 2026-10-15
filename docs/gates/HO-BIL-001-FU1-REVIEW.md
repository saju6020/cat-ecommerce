GATE: SECURITY + CODE_REVIEW + ARCHITECTURE (combined follow-up review, gate-scope rule)
HANDOVER: HO-BIL-001-FU1 (backlog BIL rows 1, 5, 8(a); origin HO-BIL-001-ACCEPTANCE M1, L3, L1)
COMMITS: c6f58a37 6b37268f 12255e8e (range e2e85e7a..12255e8e, branch ho/BIL-001-FU1)
DATE: 2026-10-07
STATUS: PASS

CHECKS RUN
- `dotnet test` Domain 1490/1490, Application 335/335, Infrastructure 164/164 passed. No integration tests were run and no database was touched.
- Adversarial fuzz in a scratch console (deleted afterwards). It used reflection on `Normalise`/`CardWindows` to get the exact masked
  index ranges and counted every card digit shown in clear other than the card's last four. 20,000 cases per shape, each in ASCII and
  with about one digit in three swapped for a supplementary-plane mathematical digit. Card lengths were 13, 15, 16 and 19 in mixed
  layouts (4-4-5, 4-6-5, 4-4-4-4-3, 6-13, unbroken), and separators were mixed within a card (space, `- . / _ , ; | :`, NBSP, en dash,
  ZWSP, tab, double space, parentheses). Results:
  - 0 leaks, 0 exceptions and 0 split surrogates for: prefix + card; card + suffix; prefix + card + two suffixes; two prefixes + card;
    two cards in one run; prefix + two cards; mobile number before or after the card; INV-/CN- document numbers either side; one
    stray digit glued at the start of the run; prefix + card masked and then truncated to 64.
  - Pre-fix guard, same `TRF <4-12 digits> <card>` shape: 4,013 of 20,000 (20%) leak a 5-digit run. This agrees with the developer's 27%
    and the ACCEPTANCE gate's 29%, which used other separator mixes.
  - Refusal path: `LooksLikeCardNumber` old vs new gives 0 differences over 200,000 ASCII inputs (cards and non-cards, with prefixes,
    suffixes and stray front digits). The only behaviour change is the intended supplementary-plane stray-digit fix.
  - False positives: on random 2-3 group references, the share of inputs altered is the same before and after (3,552 / 20,000 each).
    On 3-group references, merging hides more digits than before in 173 / 20,000 (0.9%): 51,886 digits hidden in total against
    50,555. This is acceptable over-masking. It does not change backlog row 9 (BankTransactionId), which stays open.
  - Performance: the new code runs in linear time (20,000 groups 48 ms; 4,000 single digits 50 spaces apart 138 ms). The old
    descending-`last` loop was quadratic (3,000 groups took 38.5 s), so the `break` at `> MaxDigits` also removes a latent CPU cost.

REVIEW NOTES
- Correctness argument, confirmed by the fuzz: whenever the card is a window of whole groups, it is inside one merged stretch. That
  stretch ends at or after the card's end, and only the stretch's last four digits are shown. So no card digit other than the card's
  own last four can be shown (fewer, if trailing digits join). Two windows that only touch are not merged (`start < to`), so two
  adjacent cards each keep their own last four.
- Index mapping is intact: `Normalise` stays one char in, one char out, and stretch ends are group ends, which include the trailing
  filler. `StartsOf` steps past the filler, so a surrogate pair is never split by the mask (0 ill-formed outputs).
- Mask-then-truncate order (`BankStatementAggregate.cs:72-74`) is correct: truncation can only remove text, never expose a card digit.
- CourierCode guard (`CodRemittanceAggregate.cs:45`) uses `BillingFreeText.RefuseCardNumber`, the same helper and the same
  `ReferenceLooksLikeCardNumber` code as the payment collector (`PaymentAggregate.cs:336`). `CodRemittanceDto` has no courier name, so
  no other free-text field is left unguarded. The length check runs first (`CollectorRequired`), which is fine: an over-long value is
  refused either way.
- Property test: the seed is fixed (20261007). The coincidence skip (the prefix already contains one of the card's 5-runs) fires about
  0.06% of the time and does not hide real leaks.
- Scope: Billing domain, the Billing-only `CardNumberGuard` (every caller is in Billing), Billing tests and the BIL backlog. No
  migration, no API or contract change, no new dependency.

FINDINGS
- [LOW] src/Shared/CatEcommerce.Shared/Common/Helpers/CardNumberGuard.cs:146,173-180: the stray-digit start is tried only in the first
  group of a run. A single digit glued to a card that is not at the start of the run is neither masked nor refused. Example:
  `TRF 4574 54868861179406010` is stored in clear. In the fuzz, 86% of `TRF <ref> <d><card>` cases show the whole card. This is the
  same behaviour as before the fix (0 refusal differences), so it is not a regression. Backlog row 8(b) covers only "two or more glued
  digits", so this variant is not recorded anywhere. Fix: also try `group.Start + 1` (past the filler) in every group, or fold this case
  into row 8(b).
- [LOW] CardNumberGuard.cs:129-130 (doc comment): "A merged stretch ends where its last window ends, so its last four digits are the
  card's last four" is not always true. When the card's tail and a following group form a Luhn window, the stretch shows that group's
  digits instead: `4111 1111 1111 1111 00002` becomes `••••0002`. This over-masks and does not leak, but the comment should say "at
  most the card's last four".
- [LOW] tests/CatEcommerce.Domain.Tests/Common/CardNumberMaskPrefixTests.cs:91: `Survives` checks only 5-runs that start at card
  digits 0-7, and the comment's reason ("a 5-run that ends at the end of the card includes a masked digit") does not cover runs that
  start at digits 8-10. The "••••+last four" check catches those in practice. The property also generates only 16-digit cards, with
  prefixes and no suffixes. Suggest: count every card digit shown in clear by position (as this review's harness did), and add
  13/15/19-digit cards, suffix groups and a second card.
- [LOW] docs/backlog/BIL.md:32,36,39: the edit replaced the Source cell with an empty cell (`... ||`). Rows 1, 5 and 8 lost
  "ACCEPTANCE M1", "ACCEPTANCE L3" and "ACCEPTANCE L1, L2". Restore the Source column.
- [LOW] (pre-existing, not in this diff) src/Domain/CatEcommerce.Domain/Aggregates/Billing/BankStatementAggregate.cs:92: `Truncate`
  cuts on UTF-16 units, so a supplementary-plane character (a mathematical digit, or any emoji) at the cut leaves a lone high surrogate
  (409 of 20,000 supplementary-plane fuzz cases at length 64). This is not a card leak. Depending on how Npgsql encodes the text, it
  may be stored as U+FFFD or make the import fail. Not verified against a database. Fix: step back one char if the cut lands between a
  surrogate pair.

Backlog (MEDIUM/LOW, non-blocking): the five LOW items above go to docs/backlog/BIL.md.

SCOPE CHECK
- files in allowed scope: 5/5 (CardNumberGuard.cs, CodRemittanceAggregate.cs, ReconciliationTests.cs, CardNumberMaskPrefixTests.cs,
  docs/backlog/BIL.md)
- files outside allowed scope: none
