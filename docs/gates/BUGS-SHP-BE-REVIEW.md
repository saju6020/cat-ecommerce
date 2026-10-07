GATE: COMBINED (ARCHITECTURE + SECURITY + CODE_REVIEW)
HANDOVER: BUGS-SHP-BE
COMMITS: master...HEAD of ho/BUGS-SHP-BE
DATE: 2026-10-07
STATUS: PASS

Rows covered: 8, 13, 15, 18, 19, 20, 21, 29-37, 39, 41, S5. No migrations in the diff; module
boundaries hold. Targeted tests (ShopBacklogFixes*, ShopOptionsValidator, SalesStats, StockAlertGuard,
FoodRecommendation) run green in Domain/Application/Infrastructure/Integration projects.

FINDINGS
| id | severity | location | finding |
|----|----------|----------|---------|
| BSB1 | MEDIUM | ProductReviewAggregate.cs:84-95, ClearShopDataOnCustomerAnonymisedHandler | Anonymisation clears text of a PendingModeration review but leaves it PendingModeration, so a moderator can still Publish it: a public review with empty body and no title (rating only). Fix: withdraw or reject pending reviews on anonymisation (or refuse Publish when Body is empty). |
| BSB2 | MEDIUM | StockAlertCommandHandlers.cs:140-149 (row 34) | A lapsed prescription authorisation returns success without a write, so the alert stays Pending and is returned by GetDueStockAlerts on every pass, re-checked forever and occupying a candidate slot (row 33 starvation, narrowed by the 10-page budget, but not removed). Fix: close the alert (new closure reason) or add a "not due until" backoff. Interim flag is off, so latent. |
| BSB3 | LOW | GetFoodRecommendationsQueryHandler.cs:66,152-170 (row 20) | Prescription candidates beyond the first 50 (or SKUs beyond 200) are silently treated as not authorised; an authorised prescription food ranked lower is never recommended. Acceptable bound, but undocumented in the API; consider logging when the cap bites. |
| BSB4 | LOW | ShopBusinessDate.cs:17-22 (row 8) | Shop read side now reads the Administration BranchAggregate directly (one extra query per call). Precedent exists (Scheduling, Notifications), but it is a cross-context read; a branch-calendar read service would be cleaner. |
| BSB5 | LOW | ControlCharacters.ContainsDisallowed use at ProductReviewAggregate.cs:177 (row 36) | The helper allows \n, \r and \t, so a review Title can contain line breaks. Consider rejecting them for a single-line field. |
| BSB6 | LOW | GetCatalogueQueryHandler.cs:19 | `timeProvider` constructor parameter now unread (CS9113 warning) after row 41. Remove it. |
| BSB7 | LOW | WishlistAndStockAlertQueryHandlers.cs:157 (row 33) | Keyset predicate uses Guid.CompareTo; integration tests pass, but Guid ordering in Postgres (uuid byte order) is the same one used by ThenBy, so it is consistent. Noted only to confirm it was checked. |
| BSB8 | LOW | S5 scope | Server side only (LimitedMarkup.cs). Client `LimitedMarkup.tsx` / `isSafeLink` fixes the row names are not in this BE diff and must land with the FE handover. |

SCOPE CHECK
- no migrations; DI: new ShopOptionsValidator registered with ValidateOnStart; new IDietAuthorisationGate.CheckManyAsync is a default interface method (existing fakes unchanged).
- files outside allowed scope: none observed
