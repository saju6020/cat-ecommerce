using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Visit;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;
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
public sealed class UniqueConstraintExceptionFilter : IExceptionFilter, IAsyncExceptionFilter
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

    // HO-IMP-001 §12.5 -- the same 400 each handler's own pre-check returns (InventoryMessages is shared, HO-ACC-001 §20.6, so the two
    // paths cannot drift). ix_stock_alert_dedup / ix_stock_alert_open_pair are swallowed by C15 and never reach here; the immutability
    // triggers are deliberately not mapped (reaching them is a bug, and a 500 is correct).
    private static readonly Dictionary<string, (string Code, string Message, string Property)> InventoryBadRequestIndexes = new()
    {
        ["ix_stock_location_branch_code"] = (BusinessViolationCodes.StockLocationCodeAlreadyExists, InventoryMessages.StockLocationCodeAlreadyExists, "Code"),
        ["ix_stock_location_one_quarantine"] = (BusinessViolationCodes.QuarantineLocationAlreadyExists, InventoryMessages.QuarantineLocationAlreadyExists, "Kind"),
        ["ix_supplier_code"] = (BusinessViolationCodes.SupplierCodeAlreadyExists, InventoryMessages.SupplierCodeAlreadyExists, "Code"),
        ["ix_stock_count_one_active"] = (BusinessViolationCodes.CountLocationBusy, InventoryMessages.CountLocationBusy, "LocationId"),
        ["ix_batch_receipt_lot"] = (BusinessViolationCodes.DuplicateReceiptLine, InventoryMessages.DuplicateReceiptLine, "Lines"),
    };

    // HO-IMP-001 §12.5 (ADR-099, ADR-101) -- a 23514 NAMES its constraint, so the race outcomes map the same way a unique index does: the
    // projection's CHECK is the last-unit control (409 InsufficientStock); the two usability-trigger refusals are 409 BatchExpired and
    // 409 BatchNotUsable. The text is InventoryMessages' -- the same the port returns when it refuses first. The immutability and position-guard
    // triggers are deliberately NOT mapped: reaching them is a bug, and a 500 is correct.
    private static readonly Dictionary<string, (string Code, string Message, string Property)> InventoryCheckConflicts = new()
    {
        ["ck_stock_position_balance"] = (BusinessViolationCodes.InsufficientStock, InventoryMessages.InsufficientStock, "Quantity"),
        ["tg_stock_batch_expired"] = (BusinessViolationCodes.BatchExpired, InventoryMessages.BatchExpiredGeneric, "BatchId"),
        ["tg_stock_batch_not_usable"] = (BusinessViolationCodes.BatchNotUsable, InventoryMessages.BatchNotUsable, "BatchId"),
    };

    // HO-IMP-001 §12.5 -- a first-write race on a storage profile (its id IS the product id) -> 409.
    private static readonly HashSet<string> InventoryConflictTables = ["product_storage_profile"];

    // HO-BIL-001 §12.5 -- messages come from BillingMessages, shared with each handler pre-check, so the sequential
    // and concurrent paths for the same rule cannot drift. ix_invoice_number / ix_invoice_sequence are deliberately
    // NOT mapped (a numbering bug must be loud, never papered over), and neither are the Billing trigger violations
    // (P0001 -- a code defect). The idempotency indexes are answered by BillingIdempotencyReplayFilter.
    private static readonly Dictionary<string, (int Status, string Code, string Message, string Property)> BillingMappings = new()
    {
        ["ix_charge_source"] = (409, BusinessViolationCodes.ChargeAlreadyRecorded, BillingMessages.ChargeAlreadyRecorded, "SourceId"),
        ["ix_invoice_line_charge"] = (409, BusinessViolationCodes.ChargeAlreadyInvoiced, BillingMessages.ChargeAlreadyInvoiced, "ChargeIds"),
        ["ix_bank_statement_source"] = (400, BusinessViolationCodes.StatementDuplicate, BillingMessages.StatementDuplicate, "SourceSha256"),
        ["ix_bank_match_line"] = (409, BusinessViolationCodes.BankLineAlreadyMatched, BillingMessages.BankLineAlreadyMatched, "StatementLineId"),
        ["ix_bank_match_payment"] = (409, BusinessViolationCodes.PaymentAlreadyMatched, BillingMessages.PaymentAlreadyMatched, "PaymentId"),
    };

    // A first-write race on a Billing row whose id is known in advance (a profile, a loyalty account) -> 409.
    private static readonly HashSet<string> BillingFirstWriteTables = ["billing_profile", "loyalty_account"];

    // HO-ORD-001 §12.5 -- messages come from OrderMessages, shared with each handler pre-check, so the sequential and concurrent paths for the
    // same rule cannot drift. Unknown constraints rethrow.
    private static readonly Dictionary<string, (int Status, string Code, string Message, string Property)> OrderingMappings = new()
    {
        ["ix_discount_code_code"] = (400, BusinessViolationCodes.DiscountCodeAlreadyExists, OrderMessages.DiscountCodeAlreadyExists, "Code"),
        ["ix_delivery_zone_name"] = (400, BusinessViolationCodes.DeliveryZoneNameAlreadyExists, OrderMessages.DeliveryZoneNameAlreadyExists, "Name"),
        ["ix_courier_name"] = (400, BusinessViolationCodes.CourierNameAlreadyExists, OrderMessages.CourierNameAlreadyExists, "Name"),
        // Two first adds raced (or two devices): the unique index kept ONE active cart; the loser retries and finds it (HO-ORD-001 §12.5).
        ["ix_cart_active_customer"] = (409, BusinessViolationCodes.DuplicateSubmission, OrderMessages.CartChangedOnAnotherDevice, "CustomerId"),
        ["ix_cart_guest_token"] = (409, BusinessViolationCodes.DuplicateSubmission, OrderMessages.CartChangedOnAnotherDevice, "GuestToken"),
        // The same checkout submitted twice in parallel with one key: the unique backstop kept ONE order; the loser is told it was already submitted (§12.5).
        ["ix_sales_order_customer_idempotency"] = (409, BusinessViolationCodes.DuplicateSubmission, OrderMessages.DuplicateSubmission, "IdempotencyKey"),
    };

    // A first-write race on the bank-transfer account's one fixed id -> 409.
    private static readonly HashSet<string> OrderingFirstWriteTables = ["bank_transfer_account"];


    // HO-APT-001 §12.5/§12.1 (ADR-068, F30): a 23P01 from one of the three exclusion constraints
    // means another hold/booking won the race. Staff routes get the specific resource; a customer
    // route collapses all three to the one SlotNoLongerAvailable code (§12.5) -- decided by request
    // path below, since the same constraint can be hit from either side.
    private static readonly Dictionary<string, (string Code, string Message, string Property)> AppointmentConflictIndexes = new()
    {
        ["ex_appointment_performer"] = (BusinessViolationCodes.PerformerBusy, "This performer already has an appointment at this time.", "PerformerId"),
        ["ex_appointment_room"] = (BusinessViolationCodes.RoomBusy, "This room already has an appointment at this time.", "RoomId"),
        ["ex_appointment_cat"] = (BusinessViolationCodes.CatAlreadyBooked, "This cat already has an appointment at this time.", "CatId"),

        // HO-SRG-001 section 11.7 G8 (ADR-159): the staff-time constraint also protects an assistant -- on an appointment route it means the performer is busy.
        ["ex_appointment_staff_time"] = (BusinessViolationCodes.PerformerBusy, "This performer already has an appointment at this time.", "PerformerId"),
    };

    // HO-SRG-001 section 12.5: on a surgery route (/api/staff/surgeries/**) the same four constraints name the specific resource.
    private static readonly Dictionary<string, (string Code, string Message, string Property)> SurgeryConflictIndexes = new()
    {
        ["ex_appointment_room"] = (BusinessViolationCodes.TheatreBusy, SurgeryMessages.TheatreBusy, "TheatreId"),
        ["ex_appointment_performer"] = (BusinessViolationCodes.SurgeonBusy, "The surgeon has just been booked elsewhere. Check availability again.", "SurgeonId"),
        ["ex_appointment_staff_time"] = (BusinessViolationCodes.StaffBusy, SurgeryMessages.StaffBusy, "AssistantId"),
        ["ex_appointment_cat"] = (BusinessViolationCodes.CatAlreadyBooked, "This cat already has an appointment at this time.", "CatId"),
        // Two simultaneous corrections of one entry (the "latest of its chain" pre-check cannot see the other): the loser is refused, the chain never forks. A repeated entry id that raced
        // past the handler's replay check is the SAME caller re-sending (any other holder is refused by the handler's read, with DuplicateAnaesthesiaEntry), so it gets its own outcome:
        // retry the same id, which replays. The handler cannot re-read here: a failed SaveChanges leaves the request's context holding the rolled-back rows.
        ["ix_anaesthesia_log_entry_corrects"] = (BusinessViolationCodes.AnaesthesiaEntryAlreadyCorrected, SurgeryMessages.AnaesthesiaEntryAlreadyCorrected, "CorrectsEntryId"),
        ["PK_anaesthesia_log_entry"] = (BusinessViolationCodes.AnaesthesiaEntryRaced, SurgeryMessages.AnaesthesiaEntryRaced, "EntryId"),
    };

    // HO-EMR-001 §12.5 -- one file is attached once across photos, lab attachments and consent forms; a race for the
    // same file gets the same FileAlreadyAttached 400 the handlers' own pre-check returns. A second active
    // note template for a service gets the same 400 as the handler's check.
    private static readonly Dictionary<string, (string Code, string Message, string Property)> ConsultationBadRequestIndexes = new()
    {
        // A client-supplied treatment line id that already exists on ANOTHER visit (the replay check only looks at this visit).
        ["PK_visit_treatment"] = (BusinessViolationCodes.TreatmentLineAlreadyRecorded, "That treatment line id was already used. Send a new lineId for a new treatment.", "LineId"),
        ["ix_visit_photo_file_id"] = (BusinessViolationCodes.FileAlreadyAttached, "This file is already attached to a clinical record.", "FileId"),
        ["ix_diagnostic_attachment_file_id"] = (BusinessViolationCodes.FileAlreadyAttached, "This file is already attached to a clinical record.", "FileId"),
        ["ix_consent_record_file_id"] = (BusinessViolationCodes.FileAlreadyAttached, "This file is already attached to a clinical record.", "FileId"),
        ["ix_note_template_service_active"] = (BusinessViolationCodes.NoteTemplateAlreadyExistsForService, "This service already has an active note template. Edit it or deactivate it first.", "ClinicServiceId"),
        ["ix_diagnosis_code_code"] = (BusinessViolationCodes.DiagnosisCodeAlreadyExists, "A diagnosis code with this code already exists. Choose another code.", "Code"),
        ["ix_lab_analyte_code"] = (BusinessViolationCodes.LabAnalyteAlreadyExists, "A lab analyte with this code already exists. Choose another code.", "Code"),
    };

    // HO-PHM-001 §12.5 -- the same answers each handler's own pre-check returns. The seal-chain index is never surfaced to HTTP (the worker handles it), and a
    // register trigger's RAISE EXCEPTION is deliberately NOT mapped: reaching one is a programming error and a logged 500 is correct (the immutability
    // interceptor should have stopped it first).
    private static readonly Dictionary<string, (int Status, string Code, string Message, string Property)> PharmacyMappings = new()
    {
        ["ix_diet_authorisation_active"] = (400, BusinessViolationCodes.DietAuthorisationAlreadyActive, "This cat already has an active authorisation for that food. Extend it instead.", "ProductId"),
        ["ix_formulary_medicine_code"] = (400, BusinessViolationCodes.FormularyCodeAlreadyExists, "A medicine with this code already exists. Choose another code.", "Code"),
        ["ix_formulary_medicine_sku"] = (400, BusinessViolationCodes.SkuAlreadyLinkedToMedicine, "One of those SKUs is already linked to another medicine.", "LinkedSkuIds"),
        ["ix_dispensing_supply"] = (409, BusinessViolationCodes.StaleVersion, "This supply has just been dispensed by someone else. Reload and try again.", "SupplyNumber"),
    };

    // HO-BRD-001 section 12.5 (ADR-219, renumbered ADR-352): the exclusion constraints are the occupancy control, so a lost race gets the same answer the
    // handlers' own pre-check returns (BoardingMessages is shared, HO-ACC-001 section 20.6). ix_care_task_generation is swallowed by the generator
    // and never reaches this filter. Unknown constraints rethrow.
    private static readonly Dictionary<string, (int Status, string Code, string Message, string Property)> BoardingMappings = new()
    {
        ["ex_stay_allocation_unit"] = (409, BusinessViolationCodes.UnitNoLongerAvailable, BoardingMessages.UnitNoLongerAvailable, "UnitId"),
        ["ex_stay_cat"] = (409, BusinessViolationCodes.CatAlreadyBoarding, BoardingMessages.CatAlreadyBoarding, "CatIds"),
        ["ex_unit_maintenance"] = (400, BusinessViolationCodes.MaintenanceOverlapsMaintenance, BoardingMessages.MaintenanceOverlapsMaintenance, "StartsOn"),
        ["ix_boarding_unit_branch_code"] = (400, BusinessViolationCodes.UnitCodeAlreadyExists, BoardingMessages.UnitCodeAlreadyExists, "Code"),
        ["ix_boarding_rate_card_type_effective"] = (400, BusinessViolationCodes.DuplicateRateCardEffectiveDate, BoardingMessages.DuplicateRateCardEffectiveDate, "EffectiveFrom"),
        ["ix_boarding_daycare_rate_card_effective"] = (400, BusinessViolationCodes.DuplicateRateCardEffectiveDate, BoardingMessages.DuplicateRateCardEffectiveDate, "EffectiveFrom"),
        ["ix_boarding_waitlist_active"] = (400, BusinessViolationCodes.AlreadyWaitlisted, BoardingMessages.AlreadyWaitlisted, "ArrivalOn"),
        ["ix_stay_media_file_id"] = (400, BusinessViolationCodes.InvalidMediaReference, "That file is already part of a stay diary.", "FileId"),
        ["ix_daycare_slot"] = (409, BusinessViolationCodes.DaycareFull, BoardingMessages.DaycareFull, "LocalDate"),
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

    /// <summary>
    /// HO-EMR-001 §12.5. Asynchronous because one mapping needs a read: <c>ix_visit_appointment</c> (<c>23505</c>) means
    /// another request opened the same appointment's visit first, and the caller is answered with <b>that</b>
    /// visit -- <c>200 { visitId }</c>, exactly what the idempotent sequential path returns. MVC runs this member
    /// in preference to the synchronous <see cref="OnException"/>, which every other mapping still uses.
    /// </summary>
    public async Task OnExceptionAsync(ExceptionContext context)
    {
        if (await TryAnswerOpenVisitRaceAsync(context)) return;
        if (await TryRetryDraftRaceAsync(context)) return;

        OnException(context);
    }

    private static async Task<bool> TryAnswerOpenVisitRaceAsync(ExceptionContext context)
    {
        if (UnwrapDbUpdateException(context.Exception) is not { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ix_visit_appointment" } } dbUpdate)
            return false;

        if (dbUpdate.Entries.Select(e => e.Entity).OfType<VisitAggregate>().FirstOrDefault() is not { } lost) return false;

        var services = context.HttpContext.RequestServices;
        var dispatcher = services.GetRequiredService<IInMemoryDispatcher>();
        var userContext = services.GetRequiredService<IUserContextProvider>().GetUserContext();

        var winner = await dispatcher.GetResultAsync(new GetVisitByAppointmentQuery
        {
            AppointmentId = lost.AppointmentId, BranchId = lost.BranchId, UserContext = userContext, CorrelationId = Guid.NewGuid()
        });

        if (winner.Result.VisitId is not { } visitId) return false;

        context.Result = new OkObjectResult(new { visitId });
        context.ExceptionHandled = true;
        return true;
    }

    /// <summary>
    /// HO-EMR-001 §12.5: <c>ix_consultation_draft_visit_author</c> -- the same author's two first writes raced. The loser's insert is
    /// dropped from the change tracker and its own content is dispatched <b>once</b> more, now as an overwrite of the winner's row. A
    /// second failure is not retried: it propagates as the unexpected constraint it then is.
    /// </summary>
    private static async Task<bool> TryRetryDraftRaceAsync(ExceptionContext context)
    {
        if (UnwrapDbUpdateException(context.Exception) is not { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ix_consultation_draft_visit_author" } } dbUpdate)
            return false;

        if (dbUpdate.Entries.Select(e => e.Entity).OfType<ConsultationDraftAggregate>().FirstOrDefault() is not { } lost) return false;

        var services = context.HttpContext.RequestServices;
        var branch = services.GetRequiredService<IActiveBranchAccessor>().GetActiveBranchId().BranchId;
        if (branch is null) return false;

        services.GetRequiredService<CatEcommerce.Infrastructure.Persistence.CatEcommerceDbContext>().ChangeTracker.Clear();

        var response = await services.GetRequiredService<IInMemoryDispatcher>().SendAsync(new CatEcommerce.Application.Commands.Visit.SaveConsultationDraftCommand
        {
            VisitId = lost.VisitId, DraftId = Guid.NewGuid(), BranchId = branch.Value, Subjective = lost.Subjective, Objective = lost.Objective,
            Assessment = lost.Assessment, Plan = lost.Plan, DischargeInstructions = lost.DischargeInstructions, ProblemCodeIds = lost.ProblemCodeIds,
            UserContext = services.GetRequiredService<IUserContextProvider>().GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        context.Result = response.IsSuccess ? new NoContentResult() : new BadRequestObjectResult(response.ValidationResult);
        context.ExceptionHandled = true;
        return true;
    }

    public void OnException(ExceptionContext context)
    {
        // HO-XCT-001 §12.5 (ADR-299): a refused hard delete -- the EF guard or the database trigger -- is 409 RecordIsRetained.
        if (TryMapRecordIsRetained(context)) return;

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
            else if (context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<BoardingClaimEndpointAttribute>() is not null)
            {
                // HO-BRD-001 section 12.5: the same GiST deadlock characteristic on the unit and cat constraints of a reserve / extend / relocate.
                context.Result = Conflict(BusinessViolationCodes.UnitNoLongerAvailable, BoardingMessages.UnitNoLongerAvailable, "UnitId", "BoardingStayAggregate");
                context.ExceptionHandled = true;
            }

            return;
        }

        // HO-SRG-001 section 12.5 (F31, ADR-162/165): an immutability trigger raised P0001 'immutable: ...' -- the second line behind the aggregates.
        // Any other P0001 is a defect and is rethrown.
        if (pg.SqlState == PostgresErrorCodes.RaiseException)
        {
            if (pg.MessageText.StartsWith("immutable:", StringComparison.Ordinal))
            {
                context.Result = Conflict(BusinessViolationCodes.RecordIsImmutable, SurgeryMessages.RecordIsImmutable, "Id", "Surgery");
                context.ExceptionHandled = true;
            }

            return;
        }

        if (pg.SqlState == PostgresErrorCodes.CheckViolation)
        {
            if (pg.ConstraintName is not null && InventoryCheckConflicts.TryGetValue(pg.ConstraintName, out var checkMapping))
            {
                context.Result = Conflict(checkMapping.Code, checkMapping.Message, checkMapping.Property, "Inventory");
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

        if (pg.ConstraintName is not null && SurgeryConflictIndexes.TryGetValue(pg.ConstraintName, out var surgeryMapping)
            && context.HttpContext.Request.Path.StartsWithSegments("/api/staff/surgeries", StringComparison.OrdinalIgnoreCase))
        {
            context.Result = Conflict(surgeryMapping.Code, surgeryMapping.Message, surgeryMapping.Property, "SurgeryAggregate");
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

        if (pg.ConstraintName == "ix_surgery_campaign_code")
        {
            context.Result = BadRequest(BusinessViolationCodes.CampaignCodeAlreadyExists, "A campaign with this code already exists.", "Code", "SurgeryCampaignAggregate");
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

        if (pg.ConstraintName is not null && ConsultationBadRequestIndexes.TryGetValue(pg.ConstraintName, out var consultationMapping))
        {
            context.Result = BadRequest(consultationMapping.Code, consultationMapping.Message, consultationMapping.Property, "Consultation");
            context.ExceptionHandled = true;
            return;
        }

        if (pg.ConstraintName is not null && ShopBadRequestIndexes.TryGetValue(pg.ConstraintName, out var shopMapping))
        {
            context.Result = BadRequest(shopMapping.Code, shopMapping.Message, shopMapping.Property, "Product");
            context.ExceptionHandled = true;
            return;
        }

        if (pg.ConstraintName is not null && InventoryBadRequestIndexes.TryGetValue(pg.ConstraintName, out var inventoryMapping))
        {
            context.Result = BadRequest(inventoryMapping.Code, inventoryMapping.Message, inventoryMapping.Property, "Inventory");
            context.ExceptionHandled = true;
            return;
        }

        if (pg.ConstraintName is not null && BillingMappings.TryGetValue(pg.ConstraintName, out var billing))
        {
            context.Result = billing.Status == 409
                ? Conflict(billing.Code, billing.Message, billing.Property, "Billing")
                : BadRequest(billing.Code, billing.Message, billing.Property, "Billing");
            context.ExceptionHandled = true;
            return;
        }

        if (pg.ConstraintName is not null && OrderingMappings.TryGetValue(pg.ConstraintName, out var ordering))
        {
            context.Result = ordering.Status == 409
                ? Conflict(ordering.Code, ordering.Message, ordering.Property, "Order")
                : BadRequest(ordering.Code, ordering.Message, ordering.Property, "Order");
            context.ExceptionHandled = true;
            return;
        }

        if (pg.ConstraintName is not null && PharmacyMappings.TryGetValue(pg.ConstraintName, out var pharmacy))
        {
            context.Result = pharmacy.Status == 409
                ? Conflict(pharmacy.Code, pharmacy.Message, pharmacy.Property, "Pharmacy")
                : BadRequest(pharmacy.Code, pharmacy.Message, pharmacy.Property, "Pharmacy");
            context.ExceptionHandled = true;
            return;
        }

        if (pg.ConstraintName is not null && BoardingMappings.TryGetValue(pg.ConstraintName, out var boarding))
        {
            context.Result = boarding.Status == 409
                ? Conflict(boarding.Code, boarding.Message, boarding.Property, "BoardingStayAggregate")
                : BadRequest(boarding.Code, boarding.Message, boarding.Property, "BoardingStayAggregate");
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

        if (pg.TableName is not null && (FirstWriteRaceTables.Contains(pg.TableName) || ShopConflictTables.Contains(pg.TableName) || InventoryConflictTables.Contains(pg.TableName) || BillingFirstWriteTables.Contains(pg.TableName) || OrderingFirstWriteTables.Contains(pg.TableName)))
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
    /// <summary>HO-XCT-001 §12.5. <see cref="CatEcommerce.Infrastructure.Retention.ProtectedRecordDeletionException"/> (raised by the EF guard before SQL) and a
    /// Postgres <c>P0001</c> whose message starts "Hard delete of" (the retention trigger) both answer <c>409 RecordIsRetained</c> in plain language. Any other
    /// <c>P0001</c> is not handled here. Logged at Error with the table name: reaching it means some code path tried to delete a retained record.</summary>
    private static bool TryMapRecordIsRetained(ExceptionContext context)
    {
        string? table = null;
        for (var current = context.Exception; current is not null; current = current.InnerException)
        {
            if (current is CatEcommerce.Infrastructure.Retention.ProtectedRecordDeletionException refused)
            {
                table = $"{refused.Schema}.{refused.Table}";
                break;
            }

            if (current is PostgresException { SqlState: "P0001" } pg && pg.MessageText.StartsWith("Hard delete of", StringComparison.Ordinal))
            {
                table = pg.MessageText;
                break;
            }
        }

        if (table is null) return false;

        context.HttpContext.RequestServices.GetService<ILoggerFactory>()?.CreateLogger<UniqueConstraintExceptionFilter>()
            .LogError("Refused a hard delete of a retained record ({Table}). CorrelationId: {TraceId}", table, context.HttpContext.TraceIdentifier);

        context.Result = Conflict(BusinessViolationCodes.RecordIsRetained, OperationsMessages.RecordIsRetained, "Record", "Operations");
        context.ExceptionHandled = true;
        return true;
    }

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
