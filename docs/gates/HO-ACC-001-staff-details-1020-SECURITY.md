GATE: SECURITY
HANDOVER: HO-ACC-001 (owner follow-up 2026-10-20: staff details page shows sign-in identifiers; administrator edits name/mobile/email)
COMMITS: uncommitted working tree on feat/staff-details-1020 (base 1fff8ae0)
DATE: 2026-10-20
STATUS: PASS

FINDINGS
- [LOW] src/Domain/CatEcommerce.Domain/Aggregates/Account/StaffAccountAggregate.cs:84-117 — UpdateDetails does not rotate SecurityStamp (by owner design). Concrete consequence: a staff password-reset token is bound only to the stamp (PasswordResetTokenAggregate.EnsureUsable, ResetPasswordCommandHandler.cs:48/69), so a token already sent to the OLD email/mobile stays usable for its 15-minute lifetime (AccountOptions.PasswordResetTokenLifetimeMinutes) after the administrator corrects the identifier; likewise a session opened by whoever held the wrong identifier survives the correction. Low because staff reset delivery is not used in production (page.tsx:78) and the administrator can end sessions with Set password (rotates stamp). Fix if wanted, without touching sessions: on an identifier change, revoke the account's outstanding PasswordResetToken rows (or pin the token to the destination it was sent to); optionally say in the edit panel "if the old identifier was someone else's, also set a new password".
- [LOW] src/Application/CatEcommerce.Application/CommandHandlers/Account/UpdateStaffAccountDetailsCommandHandler.cs:25-72 — an administrator can change another administrator's (or their own) sign-in identifiers. Not a new privilege: Set password already lets any administrator take over any staff account, and both actions are audited with the acting administrator. No guard needed; noted for completeness (a sole administrator mistyping their own identifiers is recoverable while their session stays open).

Checked and clean:
- Authorization: [RequireAction(StaffAccounts.UpdateDetails)] Administrator-only + ADR-009 handler re-check; 403 for Reception and 404 for unknown id covered by StaffEndpointTests. Route is guid-constrained; CSRF applies (tests sign in with CSRF).
- Uniqueness race: the pre-check excludes self; a concurrent duplicate hits ix_staff_account_mobile_number / ix_staff_account_email_address, which UniqueConstraintExceptionFilter.cs:372-383 maps to 400 MobileNumberAlreadyRegistered / EmailAddressAlreadyRegistered (same shape as the pre-check). Lost updates guarded by the xmin row version.
- Input: MobileNumber/EmailAddress value objects normalise (email trimmed, lower-cased); FullName length 200 in validator and aggregate; no-op edit raises no event.
- PII: application logs carry only StaffAccountId/CorrelationId; before/after identifiers go only to the audit row (actor = administrator), consistent with the existing audit handlers.
- Web: no new XSS sinks (React text rendering only); MSW mock mirrors server rules.

SCOPE CHECK
- files in allowed scope: 23/23 (Accounts area backend, tests, staff-admin web components/hooks/mocks)
- files outside allowed scope: none
