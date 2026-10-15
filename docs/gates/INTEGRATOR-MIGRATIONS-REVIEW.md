GATE: CODE_REVIEW (combined architecture + security + code follow-up)
HANDOVER: INTEGRATOR-MIGRATIONS (migration 20261009055613_HardenClinicalAndBillingTriggers)
COMMITS: 46ce13fa, 84050bae (base 4a4acb88)
DATE: 2026-10-09
STATUS: PASS

FINDINGS
- [LOW] .ai/decisions/adr.md ADR-118 — wording is "no clinical row is ever deleted"; the migration is stricter (visit_id never changes on a child row; soft-delete flag refused outright; billing TRUNCATE refused). Amendment note belongs there (backlog EMR row 40).
- [LOW] src/Infrastructure/CatEcommerce.Infrastructure/Billing/BillingMigrationSql.cs:NoTruncate — the DO loop covers only tables existing when it runs; future billing tables need the trigger added by their own migration. Not silent: XI-7 enumerates billing tables from pg_class and fails if one accepts TRUNCATE.

CHECKS
- Up() replaces visit_frozen, visit_child_frozen, consent_record_guard via CREATE OR REPLACE; new diagnostic_order_guard + trigger; Down() restores the prior bodies (compared with AddClinicalRecordImmutability and AddConsents: identical) and drops the new trigger/function and the billing triggers. DROP TRIGGER IF EXISTS before CREATE makes the billing loop re-runnable.
- visit_child_frozen: OLD is only read inside the UPDATE branch; INSERT and DELETE paths are safe.
- No src path sets IsMarkedToDelete, changes visit_id/cat_id/branch_id, or truncates clinic/billing tables. XCT-001 (HO-XCT-001 sections 4, 8.2, F36) classifies clinical and financial rows as never hard-deleted, end = review flag only, and requires statement-level TRUNCATE triggers; no conflict with erasure or purge (held, not erased).
- Error codes consistent: CR001 for clinic (as existing triggers), P0001 for billing (as BillingMigrationSql).
- Tests assert SQLSTATE refusal and that legitimate edits (in-place child update, status to Resulted, withdraw, flag false to false) still work. Targeted run against catecommerce_intmig: all G/I-H6/XI-7/U-X3 tests pass; 2 unrelated Shop I_G3_* tests matched by my filter substring failed (notification delivery, no link to triggers; not investigated further).

SCOPE CHECK
- files in allowed scope: 11/11
- files outside allowed scope: none
