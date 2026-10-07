using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Api.Filters;

/// <summary>
/// HO-ACC-001 §12.4 / ADR-026, extended by HO-ADM-001 §12.5. Registered <b>alongside</b>
/// <see cref="ConcurrencyExceptionFilter"/>, never widening it. A `23505` on the mobile or email
/// unique index maps to the <b>same</b> `400` the handler's own pre-check would have returned
/// (<see cref="AccountMessages"/> -- so the two paths cannot drift, §20.6); a primary-key collision
/// on the address book or preferences (their first-write race) maps to `409`. HO-ADM-001 adds: the
/// branch code, room name, service code, price/rate effective-date, content slug and policy
/// indexes -> the same `400` their own pre-checks return; the staff roster's (staff, branch)
/// uniqueness and a first-write race on a settings scope row -> `409`. A `23P01` (exclusion
/// violation) on the leave request's overlap-guard GiST constraint maps to the same `400` the
/// handler's own overlap pre-check returns (gate fix 2026-09-26). HO-FIL-001 adds: a `23505` on
/// `cat_photo`'s unique index on `file_id` (gate fix 2026-09-27, ARCH HIGH second pass) -> the same
/// `400 PhotoFileNotEligible` `AddCatPhotoCommandHandler`'s own pre-check returns. Any other
/// constraint is rethrown -- an unexpected `23505`/`23P01` is a bug, not something to paper over.
/// HO-APT-001 adds: on an action carrying <see cref="SlotClaimEndpointAttribute"/> only, a Postgres
/// deadlock (`40P01`) maps to the same `409 SlotNoLongerAvailable` a `23P01` on the appointment
/// exclusion constraints would (ARCHITECTURE gate second pass HIGH B, owner decision, scoped to
/// slot claims rather than every area -- a deadlock on an unmarked action is left unhandled).
/// </summary>
public sealed class UniqueConstraintExceptionFilter : IExceptionFilter
{
    private static readonly HashSet<string> MobileIndexes = ["ix_customer_account_mobile_number", "ix_staff_account_mobile_number"];
    private static readonly HashSet<string> EmailIndexes = ["ix_customer_account_email_address", "ix_staff_account_email_address"];
    private static readonly HashSet<string> FirstWriteRaceTables = ["customer_address_book", "customer_notification_preferences", "business_settings"];
    // HO-NTF-001 §12.5 (deviation from §13's file list, recorded in the implementation report):
    // "ix_notification_template_key" -- a first-save race on a template's own aggregate creation
    // (two admins customising the same not-yet-customised template at once) -> 409, same shape as
    // ix_staff_roster_staff_branch. ix_notification_request_dedup is handled INSIDE C3
    // (RequestNotificationCommandHandler) and never reaches this filter.
    private static readonly HashSet<string> ConflictIndexes = ["ix_staff_roster_staff_branch", "ix_notification_template_key"];

    // Gate-fix 2026-09-27 (HO-FIL-001-ARCHITECTURE.md second pass, ARCH HIGH): "one file, one cat"
    // has no state on StoredFileAggregate to check first (AddCatPhotoCommandHandler only asks the
    // side-effect-free IsEligibleForAttach) -- this index is the only enforcement, so its violation
    // must map to the exact same PhotoFileNotEligible 400 the handler's own pre-check returns, or a
    // concurrent attach of one file to two cats would tell the loser apart from an ordinary refusal.
    private static readonly Dictionary<string, (string Code, string Message, string Property)> CatBadRequestIndexes = new()
    {
        ["ix_cat_photo_file_id"] = (BusinessViolationCodes.PhotoFileNotEligible,
            "The uploaded file could not be attached to this cat. Upload it again and try immediately.", "FileId"),
    };

    // HO-SHP-001 §12.5 -- messages come from ShopMessages, shared with each handler's own
    // pre-check (ShopMessages' own doc comment), so the sequential and concurrent-race paths for
    // the same rule cannot drift.
    private static readonly Dictionary<string, (string Code, string Message, string Property)> ShopBadRequestIndexes = new()
    {
        ["ix_product_slug"] = (BusinessViolationCodes.SlugAlreadyInUse, ShopMessages.SlugAlreadyInUse, "Slug"),
        ["ix_sku_code"] = (BusinessViolationCodes.SkuCodeAlreadyInUse, ShopMessages.SkuCodeAlreadyInUse, "Code"),
        ["ix_sku_barcode"] = (BusinessViolationCodes.BarcodeAlreadyInUse, ShopMessages.BarcodeAlreadyInUse, "Barcode"),
        ["ix_sku_price_sku_effective"] = (BusinessViolationCodes.DuplicatePriceEffectiveDate, ShopMessages.DuplicatePriceEffectiveDate, "EffectiveFrom"),
        ["ix_sourcing_origin_country"] = (BusinessViolationCodes.OriginAlreadyExists, ShopMessages.OriginAlreadyExists, "CountryCode"),
        ["ix_product_review_active_per_customer_product"] = (BusinessViolationCodes.AlreadyReviewed, ShopMessages.AlreadyReviewed, "ProductId"),
        ["ix_stock_alert_pending_per_customer_sku"] = (BusinessViolationCodes.StockAlertAlreadyRequested, ShopMessages.StockAlertAlreadyRequested, "SkuId"),
    };

    // HO-SHP-001 §12.5 -- a first-write race on a wishlist or the sales/stock copy -> 409 (no
    // sequential pre-check exists for these, since the race is the FIRST write of the id).
    private static readonly HashSet<string> ShopConflictTables = ["wishlist", "product_sales_stats"];


    // HO-APT-001 §12.5/§12.1 (ADR-068, F30): a 23P01 from one of the three exclusion constraints
    // means another hold/booking won the race. Staff routes get the specific resource; a customer
    // route collapses all three to the one SlotNoLongerAvailable code (§12.5) -- decided by request
    // path below, since the same constraint can be hit from either side.
    private static readonly Dictionary<string, (string Code, string Message, string Property)> AppointmentConflictIndexes = new()
    {
        ["ex_appointment_performer"] = (BusinessViolationCodes.PerformerBusy, "This performer already has an appointment at this time.", "PerformerId"),
        ["ex_appointment_room"] = (BusinessViolationCodes.RoomBusy, "This room already has an appointment at this time.", "RoomId"),
        ["ex_appointment_cat"] = (BusinessViolationCodes.CatAlreadyBooked, "This cat already has an appointment at this time.", "CatId"),
    };

    private static readonly Dictionary<string, (string Code, string Message, string Property)> AdministrationBadRequestIndexes = new()
    {
        ["ix_branch_code"] = (BusinessViolationCodes.BranchCodeAlreadyExists, "This branch code is already in use.", "Code"),
        ["ix_room_branch_name"] = (BusinessViolationCodes.RoomNameAlreadyExists, "A room with this name already exists at this branch.", "Name"),
        ["ix_clinic_service_code"] = (BusinessViolationCodes.ServiceCodeAlreadyExists, "This service code is already in use.", "Code"),
        ["ix_service_price_service_effective"] = (BusinessViolationCodes.DuplicatePriceEffectiveDate, "A price already exists with this effective date.", "EffectiveFrom"),
        ["ix_content_item_kind_slug"] = (BusinessViolationCodes.ContentSlugAlreadyExists, "This slug is already in use.", "Slug"),
        ["ix_tax_class_code"] = (BusinessViolationCodes.ServiceCodeAlreadyExists, "This tax class code is already in use.", "Code"),
        ["ix_tax_rate_class_effective"] = (BusinessViolationCodes.DuplicatePriceEffectiveDate, "A rate already exists with this effective date.", "EffectiveFrom"),
        ["ix_policy_document_key"] = (BusinessViolationCodes.PolicyKeyAlreadyExists, "This policy key is already in use.", "Key"),
        ["ix_policy_version_key_number"] = (BusinessViolationCodes.PolicyVersionAlreadyExists, "This policy already has a version with this number.", "VersionNumber"),
        ["ex_leave_request_staff_no_overlap"] = (BusinessViolationCodes.LeaveOverlapsExistingLeave, "This period overlaps an existing leave request.", "StartsOn"),
    };

    public void OnException(ExceptionContext context)
    {
        // Gate fix (flaky I-C2, 2026-09-28, reproduced against a freshly migrated DB): EF Core's
        // ExecutionStrategy wraps an exception it classifies as transient -- which a Postgres
        // deadlock is -- in an outer InvalidOperationException ("likely due to a transient
        // failure") whenever retry-on-failure is not enabled (it is not, here), even though this
        // DbContext never opted into that strategy. `context.Exception is DbUpdateException` alone
        // therefore stopped matching for exactly the failure shape this filter exists to catch,
        // and the 20-concurrent-holds race fell through to the generic empty-body 500. Unwrap
        // through however many layers wrap it, rather than assuming the shape is always flat.
        if (UnwrapDbUpdateException(context.Exception) is not { InnerException: PostgresException pg }) return;

        // Under heavy concurrent INSERTs against a GiST exclusion constraint, Postgres can report
        // a deadlock (40P01) for some of the losing transactions instead of a clean exclusion
        // violation (23P01) -- a documented characteristic of exclusion constraints under
        // contention (the constraint itself is not wrong, §20.3), most visible on a cold
        // connection pool / cold query-plan cache, i.e. exactly a freshly migrated database's first
        // requests. Postgres does not attach a constraint or table name to a deadlock's diagnostic
        // fields, so it cannot be routed through the constraint-name dictionaries below.
        //
        // ARCHITECTURE gate second pass, HIGH B (owner decision, 2026-09-28): an unconditional
        // mapping here made every deadlock in every area -- leave requests, settings, billing later
        // -- into a silent 409 instead of a diagnosable 500, which is this filter's own stated rule
        // broken ("an unexpected 23505/23P01 is a bug, not something to paper over"; the same
        // applies to 40P01). Scoped instead to the five slot-claiming actions
        // (`[SlotClaimEndpoint]`, HO-APT-001 §11.4's C1/C4/C6/C7 -- C15 has no HTTP endpoint), where
        // a deadlock means exactly what a 23P01 on the appointment exclusion constraints means:
        // another hold or booking won the race for this slot. Everywhere else a deadlock is left
        // unhandled and becomes the logged 500 it always was.
        if (pg.SqlState == PostgresErrorCodes.DeadlockDetected)
        {
            if (context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<SlotClaimEndpointAttribute>() is not null)
            {
                context.Result = Conflict(BusinessViolationCodes.SlotNoLongerAvailable, ClinicMessages.SlotNoLongerAvailable, "StartsOn", "AppointmentAggregate");
                context.ExceptionHandled = true;
            }

            return;
        }

        if (pg.SqlState != PostgresErrorCodes.UniqueViolation && pg.SqlState != PostgresErrorCodes.ExclusionViolation) return;

        // Gate fix 2026-09-26 (ARCH LOW): §8.8 says accepting the same policy version twice is a
        // no-op 204, and the handler's own sequential pre-check already returns exactly that --
        // two truly concurrent accepts must agree, not have the race path return a 400
        // PolicyAlreadyAccepted the sequential path never would.
        if (pg.ConstraintName == "ix_policy_acceptance_customer_key_number")
        {
            context.Result = new NoContentResult();
            context.ExceptionHandled = true;
            return;
        }

        if (pg.ConstraintName is not null && MobileIndexes.Contains(pg.ConstraintName))
        {
            context.Result = BadRequest(BusinessViolationCodes.MobileNumberAlreadyRegistered, AccountMessages.MobileInUse, "MobileNumber", "Account");
            context.ExceptionHandled = true;
            return;
        }

        if (pg.ConstraintName is not null && EmailIndexes.Contains(pg.ConstraintName))
        {
            context.Result = BadRequest(BusinessViolationCodes.EmailAddressAlreadyRegistered, AccountMessages.EmailInUse, "EmailAddress", "Account");
            context.ExceptionHandled = true;
            return;
        }

        if (pg.ConstraintName is not null && CatBadRequestIndexes.TryGetValue(pg.ConstraintName, out var catMapping))
        {
            context.Result = BadRequest(catMapping.Code, catMapping.Message, catMapping.Property, "StoredFile");
            context.ExceptionHandled = true;
            return;
        }

        if (pg.ConstraintName is not null && AppointmentConflictIndexes.TryGetValue(pg.ConstraintName, out var apptMapping))
        {
            var isCustomerRoute = context.HttpContext.Request.Path.StartsWithSegments("/api/customers", StringComparison.OrdinalIgnoreCase);
            var (code, message, property) = isCustomerRoute
                ? (BusinessViolationCodes.SlotNoLongerAvailable, ClinicMessages.SlotNoLongerAvailable, "StartsOn")
                : apptMapping;

            context.Result = Conflict(code, message, property, "AppointmentAggregate");
            context.ExceptionHandled = true;
            return;
        }

        if (pg.ConstraintName == "ix_waitlist_active_per_cat")
        {
            context.Result = BadRequest(BusinessViolationCodes.AlreadyWaitlisted, "Already on the waitlist for this cat, service and date.", "CatId", "WaitlistEntryAggregate");
            context.ExceptionHandled = true;
            return;
        }

        if (pg.ConstraintName is not null && AdministrationBadRequestIndexes.TryGetValue(pg.ConstraintName, out var mapping))
        {
            context.Result = BadRequest(mapping.Code, mapping.Message, mapping.Property, "Administration");
            context.ExceptionHandled = true;
            return;
        }

        if (pg.ConstraintName is not null && ShopBadRequestIndexes.TryGetValue(pg.ConstraintName, out var shopMapping))
        {
            context.Result = BadRequest(shopMapping.Code, shopMapping.Message, shopMapping.Property, "Product");
            context.ExceptionHandled = true;
            return;
        }

        if (pg.ConstraintName is not null && ConflictIndexes.Contains(pg.ConstraintName))
        {
            context.Result = new ConflictObjectResult(new
            {
                error = "This record was changed by someone else. Reload and try again."
            });
            context.ExceptionHandled = true;
            return;
        }

        if (pg.TableName is not null && (FirstWriteRaceTables.Contains(pg.TableName) || ShopConflictTables.Contains(pg.TableName)))
        {
            context.Result = new ConflictObjectResult(new
            {
                error = "This record was changed by someone else. Reload and try again."
            });
            context.ExceptionHandled = true;
            return;
        }

        // Any other constraint -> rethrow (do nothing; ExceptionHandled stays false).
    }

    /// <summary>Walks the exception chain for the first <see cref="DbUpdateException"/>: usually
    /// <c>context.Exception</c> itself, but sometimes wrapped in an outer
    /// <see cref="InvalidOperationException"/> the base <c>ExecutionStrategy</c> adds for an
    /// exception it classifies as transient (gate fix, see <see cref="OnException"/>).</summary>
    private static DbUpdateException? UnwrapDbUpdateException(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbUpdateException dbUpdateException) return dbUpdateException;
        }

        return null;
    }

    private static BadRequestObjectResult BadRequest(string code, string message, string property, string resourceName)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property, errorCode: code, resourceName: resourceName);
        return new BadRequestObjectResult(validationResponse);
    }

    private static ConflictObjectResult Conflict(string code, string message, string property, string resourceName)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property, errorCode: code, resourceName: resourceName);
        return new ConflictObjectResult(validationResponse);
    }
}
