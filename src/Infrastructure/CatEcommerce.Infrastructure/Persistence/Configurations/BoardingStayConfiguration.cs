using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Boarding;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Entities;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>
/// HO-BRD-001 section 8.1/12.1 -- the stay and its bounded children, schema <c>boarding</c>. The two exclusion constraints (<c>ex_stay_allocation_unit</c> on
/// <c>stay_unit_allocation</c>, <c>ex_stay_cat</c> on <c>stay_cat</c>) are raw SQL in the <c>AddBoardingStays</c> migration -- EF does not model them -- and read
/// the <c>is_active</c> column the aggregate maintains (F31). Every predicate is built from <c>Enumeration.Value</c>, never a literal (U-X3).
/// </summary>
public sealed class BoardingStayConfiguration : IEntityTypeConfiguration<BoardingStayAggregate>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<BoardingStayAggregate> builder)
    {
        builder.ToTable("stay", "boarding");
        builder.ConfigureAggregateBase();

        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(x => x.Channel).HasColumnName("channel").HasEnumerationValue().IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasEnumerationValue().IsRequired();
        builder.Property(x => x.ArrivalOn).HasColumnName("arrival_on").HasColumnType("date").IsRequired();
        builder.Property(x => x.DepartureOn).HasColumnName("departure_on").HasColumnType("date").IsRequired();
        builder.Property(x => x.OccupiedFrom).HasColumnName("occupied_from").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.OccupiedTo).HasColumnName("occupied_to").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.RequestedUnitType).HasColumnName("requested_unit_type").HasEnumerationValue().IsRequired();
        builder.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasColumnType("char(3)").IsRequired();
        builder.Property(x => x.RequiresIsolation).HasColumnName("requires_isolation").IsRequired();

        builder.Property(x => x.RateBasis).HasColumnName("rate_basis")
            .HasConversion(v => v == null ? (int?)null : v.Value, v => v == null ? null : Enumeration.FromValue<StayRateBasis>(v.Value));
        builder.Property(x => x.QuoteTotal).HasColumnName("quote_total").HasColumnType("numeric(12,2)").IsRequired();
        builder.Property(x => x.DiscountTotal).HasColumnName("discount_total").HasColumnType("numeric(12,2)").IsRequired();
        builder.Property(x => x.DepositRequired).HasColumnName("deposit_required").HasColumnType("numeric(12,2)").IsRequired();
        builder.Property(x => x.DepositDueBy).HasColumnName("deposit_due_by").HasColumnType("timestamptz");
        builder.Property(x => x.DepositState).HasColumnName("deposit_state").HasEnumerationValue().IsRequired();

        builder.Property(x => x.ReservedOn).HasColumnName("reserved_on").HasColumnType("timestamptz");
        builder.Property(x => x.ConfirmedOn).HasColumnName("confirmed_on").HasColumnType("timestamptz");
        builder.Property(x => x.ConfirmedBy).HasColumnName("confirmed_by");
        builder.Property(x => x.CheckedInOn).HasColumnName("checked_in_on").HasColumnType("timestamptz");
        builder.Property(x => x.CheckedOutOn).HasColumnName("checked_out_on").HasColumnType("timestamptz");
        builder.Property(x => x.CancelledOn).HasColumnName("cancelled_on").HasColumnType("timestamptz");
        builder.Property(x => x.CancelledBy).HasColumnName("cancelled_by");
        builder.Property(x => x.CancellationReason).HasColumnName("cancellation_reason")
            .HasConversion(v => v == null ? (int?)null : v.Value, v => v == null ? null : Enumeration.FromValue<BoardingCancellationReason>(v.Value));
        builder.Property(x => x.CancellationNote).HasColumnName("cancellation_note").HasMaxLength(StayRules.MaxCancellationNoteLength);
        builder.Property(x => x.DepositForfeited).HasColumnName("deposit_forfeited").IsRequired();
        builder.Property(x => x.NoShowRecordedOn).HasColumnName("no_show_recorded_on").HasColumnType("timestamptz");
        builder.Property(x => x.NoShowRecordedBy).HasColumnName("no_show_recorded_by");
        builder.Property(x => x.NeedsAttention).HasColumnName("needs_attention").IsRequired();
        builder.Property(x => x.AttentionReason).HasColumnName("attention_reason")
            .HasConversion(v => v == null ? (int?)null : v.Value, v => v == null ? null : Enumeration.FromValue<StayAttentionReason>(v.Value));

        builder.OwnsOne(x => x.RateCardSnapshot, snap =>
        {
            snap.Property(p => p.CardId).HasColumnName("rate_card_id");
            snap.Property(p => p.UnitType).HasColumnName("rate_card_unit_type").HasEnumerationValue();
            snap.Property(p => p.NightlyRate).HasColumnName("rate_card_nightly").HasColumnType("numeric(12,2)");
            snap.Property(p => p.WeeklyRate).HasColumnName("rate_card_weekly").HasColumnType("numeric(12,2)");
            snap.Property(p => p.MonthlyRate).HasColumnName("rate_card_monthly").HasColumnType("numeric(12,2)");
            snap.Property(p => p.AdditionalCatNightlyRate).HasColumnName("rate_card_additional_cat").HasColumnType("numeric(12,2)");
            snap.Property(p => p.TaxClassId).HasColumnName("rate_card_tax_class_id");
            snap.Property(p => p.CurrencyCode).HasColumnName("rate_card_currency_code").HasColumnType("char(3)");
        });

        builder.OwnsOne(x => x.PricingSnapshot, snap =>
        {
            snap.Property(p => p.LongStayThresholdNights).HasColumnName("pricing_long_stay_threshold_nights");
            snap.Property(p => p.LongStayDiscountPercent).HasColumnName("pricing_long_stay_discount_percent").HasColumnType("numeric(5,2)");
            snap.Property(p => p.EarlyCheckoutChargedNights).HasColumnName("pricing_early_checkout_charged_nights");
            snap.Property(p => p.EarlyCheckoutRefundAsCredit).HasColumnName("pricing_early_checkout_refund_as_credit");
        });

        builder.OwnsMany(x => x.Cats, cat =>
        {
            cat.ToTable("stay_cat", "boarding");
            cat.WithOwner().HasForeignKey("stay_id");
            cat.HasKey(c => c.Id);
            cat.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
            cat.Property(c => c.CatId).HasColumnName("cat_id").IsRequired();
            cat.Property(c => c.CatName).HasColumnName("cat_name").HasMaxLength(100).IsRequired();
            cat.Property(c => c.OccupiedFrom).HasColumnName("occupied_from").HasColumnType("timestamptz").IsRequired();
            cat.Property(c => c.OccupiedTo).HasColumnName("occupied_to").HasColumnType("timestamptz").IsRequired();
            cat.Property(c => c.IsActive).HasColumnName("is_active").IsRequired();

            cat.Property(c => c.GateStatus).HasColumnName("gate_status").HasEnumerationValue().IsRequired();
            cat.Property(c => c.GateReasons).HasColumnName("gate_reasons").HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, Json),
                    v => (IReadOnlyList<GateReason>)(JsonSerializer.Deserialize<List<GateReason>>(v, Json) ?? new List<GateReason>()),
                    new ValueComparer<IReadOnlyList<GateReason>>(
                        (a, b) => JsonSerializer.Serialize(a, Json) == JsonSerializer.Serialize(b, Json),
                        v => JsonSerializer.Serialize(v, Json).GetHashCode(),
                        v => (IReadOnlyList<GateReason>)JsonSerializer.Deserialize<List<GateReason>>(JsonSerializer.Serialize(v, Json), Json)!));
            cat.Property(c => c.GateEvaluatedOn).HasColumnName("gate_evaluated_on").HasColumnType("date");
            cat.Property(c => c.GateOverriddenBy).HasColumnName("gate_overridden_by");
            cat.Property(c => c.GateOverriddenOn).HasColumnName("gate_overridden_on").HasColumnType("timestamptz");
            cat.Property(c => c.GateOverrideReason).HasColumnName("gate_override_reason").HasMaxLength(StayRules.MaxReasonLength);

            cat.Property(c => c.HasCareProfile).HasColumnName("has_care_profile").IsRequired();
            cat.Property(c => c.Litter).HasColumnName("litter_type")
                .HasConversion(v => v == null ? (int?)null : v.Value, v => v == null ? null : Enumeration.FromValue<LitterType>(v.Value));
            cat.Property(c => c.LitterNote).HasColumnName("litter_note").HasMaxLength(CareProfileRules.MaxLitterNoteLength);
            cat.Property(c => c.VetVisitAuthorised).HasColumnName("vet_visit_authorised").IsRequired();
            cat.Property(c => c.VetSpendingLimit).HasColumnName("vet_spending_limit").HasColumnType("numeric(12,2)").IsRequired();
            cat.Property(c => c.EmergencyContactName).HasColumnName("emergency_contact_name").HasMaxLength(CareProfileRules.MaxContactNameLength);
            cat.Property(c => c.EmergencyContactPhone).HasColumnName("emergency_contact_phone").HasMaxLength(CareProfileRules.MaxContactPhoneLength);
            cat.Property(c => c.EmergencyContactRelationship).HasColumnName("emergency_contact_relationship").HasMaxLength(CareProfileRules.MaxContactRelationshipLength);
            cat.Property(c => c.CareNotes).HasColumnName("care_notes").HasMaxLength(CareProfileRules.MaxNotesLength);
            cat.Property(c => c.VetAuthorityChangeReason).HasColumnName("vet_authority_change_reason").HasMaxLength(StayRules.MaxReasonLength);
            cat.Property(c => c.CheckedInOn).HasColumnName("checked_in_on").HasColumnType("timestamptz");
            cat.Property(c => c.CheckInWeightKg).HasColumnName("check_in_weight_kg").HasColumnType("numeric(5,2)");
            cat.Property(c => c.CheckInHealthObservation).HasColumnName("check_in_health_observation").HasMaxLength(1000);
            cat.Property(c => c.CheckInIdentityConfirmed).HasColumnName("check_in_identity_confirmed").IsRequired();
            cat.Property(c => c.CheckInVaccinationProofVerified).HasColumnName("check_in_vaccination_proof_verified").IsRequired();
            cat.Property(c => c.NoBelongings).HasColumnName("no_belongings").IsRequired();
            cat.Property(c => c.CheckInSignerName).HasColumnName("check_in_signer_name").HasMaxLength(BoardingSignatureRules.MaxSignerNameLength);
            cat.Property(c => c.CheckInSignatureMethod).HasColumnName("check_in_signature_method")
                .HasConversion(v => v == null ? (int?)null : v.Value, v => v == null ? null : Enumeration.FromValue<SignatureMethod>(v.Value));
            cat.Property(c => c.CheckInSignatureStrokes).HasColumnName("check_in_signature_strokes").HasColumnType("jsonb");
            cat.Property(c => c.CheckInSignedOn).HasColumnName("check_in_signed_on").HasColumnType("timestamptz");
            cat.Property(c => c.CheckedOutOn).HasColumnName("checked_out_on").HasColumnType("timestamptz");
            cat.Property(c => c.CheckOutWeightKg).HasColumnName("check_out_weight_kg").HasColumnType("numeric(5,2)");
            cat.Property(c => c.CheckOutSignerName).HasColumnName("check_out_signer_name").HasMaxLength(BoardingSignatureRules.MaxSignerNameLength);
            cat.Property(c => c.CheckOutSignatureMethod).HasColumnName("check_out_signature_method")
                .HasConversion(v => v == null ? (int?)null : v.Value, v => v == null ? null : Enumeration.FromValue<SignatureMethod>(v.Value));
            cat.Property(c => c.CheckOutSignatureStrokes).HasColumnName("check_out_signature_strokes").HasColumnType("jsonb");
            cat.Property(c => c.CheckOutSignedOn).HasColumnName("check_out_signed_on").HasColumnType("timestamptz");
            cat.Property(c => c.SuspectedInfectious).HasColumnName("suspected_infectious").IsRequired();
            cat.Property(c => c.InfectiousFlaggedBy).HasColumnName("infectious_flagged_by");
            cat.Property(c => c.InfectiousFlaggedOn).HasColumnName("infectious_flagged_on").HasColumnType("timestamptz");
            cat.Property(c => c.InfectiousNote).HasColumnName("infectious_note").HasMaxLength(StayRules.MaxReasonLength);
            cat.Ignore(c => c.Profile);
            cat.Ignore(c => c.IsOverridden);

            cat.OwnsMany(c => c.Feedings, f =>
            {
                f.ToTable("stay_cat_feeding", "boarding");
                f.WithOwner().HasForeignKey("stay_cat_id");
                f.HasKey(p => p.Id);
                f.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();
                f.Property(p => p.Time).HasColumnName("local_time").HasColumnType("time").IsRequired();
                f.Property(p => p.Portion).HasColumnName("portion").HasMaxLength(CareProfileRules.MaxPortionLength).IsRequired();
                f.Property(p => p.FoodSource).HasColumnName("food_source").HasEnumerationValue().IsRequired();
                f.Property(p => p.FoodDescription).HasColumnName("food_description").HasMaxLength(CareProfileRules.MaxFoodDescriptionLength).IsRequired();
                f.Property(p => p.HouseFoodSkuId).HasColumnName("house_food_sku_id");
                f.HasIndex("stay_cat_id").HasDatabaseName("ix_stay_cat_feeding_stay_cat");
            });
            cat.Navigation(c => c.Feedings).UsePropertyAccessMode(PropertyAccessMode.Field);

            cat.OwnsMany(c => c.Medications, m =>
            {
                m.ToTable("stay_cat_medication", "boarding");
                m.WithOwner().HasForeignKey("stay_cat_id");
                m.HasKey(p => p.Id);
                m.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();
                m.Property(p => p.Name).HasColumnName("name").HasMaxLength(CareProfileRules.MaxMedicationNameLength).IsRequired();
                m.Property(p => p.Dose).HasColumnName("dose").HasMaxLength(CareProfileRules.MaxDoseLength).IsRequired();
                m.Property(p => p.Route).HasColumnName("route").HasMaxLength(CareProfileRules.MaxRouteLength).IsRequired();
                m.Property(p => p.Times).HasColumnName("times").HasColumnType("time[]").IsRequired();
                m.Property(p => p.Instructions).HasColumnName("instructions").HasMaxLength(CareProfileRules.MaxInstructionsLength);
                m.Property(p => p.FromOn).HasColumnName("from_on").HasColumnType("date");
                m.Property(p => p.ToOn).HasColumnName("to_on").HasColumnType("date");
                m.HasIndex("stay_cat_id").HasDatabaseName("ix_stay_cat_medication_stay_cat");
            });
            cat.Navigation(c => c.Medications).UsePropertyAccessMode(PropertyAccessMode.Field);

            cat.OwnsMany(c => c.Belongings, b =>
            {
                b.ToTable("stay_cat_belonging", "boarding");
                b.WithOwner().HasForeignKey("stay_cat_id");
                b.HasKey(p => p.Id);
                b.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();
                b.Property(p => p.Kind).HasColumnName("kind").HasEnumerationValue().IsRequired();
                b.Property(p => p.Description).HasColumnName("description").HasMaxLength(CareProfileRules.MaxBelongingDescriptionLength).IsRequired();
                b.Property(p => p.FileIds).HasColumnName("file_ids").HasColumnType("uuid[]").IsRequired();
                b.Property(p => p.Returned).HasColumnName("returned");
                b.Property(p => p.ReturnNote).HasColumnName("return_note").HasMaxLength(StayRules.MaxReasonLength);
                b.HasIndex("stay_cat_id").HasDatabaseName("ix_stay_cat_belonging_stay_cat");
            });
            cat.Navigation(c => c.Belongings).UsePropertyAccessMode(PropertyAccessMode.Field);

            cat.HasIndex(c => c.CatId).HasDatabaseName("ix_stay_cat_cat");
            cat.HasIndex("stay_id").HasDatabaseName("ix_stay_cat_stay");
        });
        builder.Navigation(x => x.Cats).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(x => x.Allocations, allocation =>
        {
            allocation.ToTable("stay_unit_allocation", "boarding");
            allocation.WithOwner().HasForeignKey("stay_id");
            allocation.HasKey(a => a.Id);
            allocation.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();
            allocation.Property(a => a.UnitId).HasColumnName("unit_id").IsRequired();
            allocation.Property(a => a.UnitType).HasColumnName("unit_type").HasEnumerationValue().IsRequired();
            allocation.Property(a => a.OccupiedFrom).HasColumnName("occupied_from").HasColumnType("timestamptz").IsRequired();
            allocation.Property(a => a.OccupiedTo).HasColumnName("occupied_to").HasColumnType("timestamptz").IsRequired();
            allocation.Property(a => a.IsActive).HasColumnName("is_active").IsRequired();
            allocation.Property(a => a.Reason).HasColumnName("reason").HasMaxLength(StayRules.MaxReasonLength);
            allocation.Property(a => a.CreatedBy).HasColumnName("created_by").IsRequired();
            allocation.Property(a => a.CreatedOn).HasColumnName("created_on").HasColumnType("timestamptz").IsRequired();
            allocation.HasIndex(a => new { a.UnitId, a.OccupiedFrom }).HasDatabaseName("ix_stay_unit_allocation_unit_from");
            allocation.HasIndex("stay_id").HasDatabaseName("ix_stay_unit_allocation_stay");
        });
        builder.Navigation(x => x.Allocations).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(x => x.RateUnits, unit =>
        {
            unit.ToTable("stay_rate_unit", "boarding");
            unit.WithOwner().HasForeignKey("stay_id");
            unit.HasKey(u => u.Id);
            unit.Property(u => u.Id).HasColumnName("id").ValueGeneratedNever();
            unit.Property(u => u.Sequence).HasColumnName("sequence").IsRequired();
            unit.Property(u => u.Kind).HasColumnName("kind").HasEnumerationValue().IsRequired();
            unit.Property(u => u.FromNight).HasColumnName("from_night").IsRequired();
            unit.Property(u => u.ToNight).HasColumnName("to_night").IsRequired();
            unit.Property(u => u.Amount).HasColumnName("amount").HasColumnType("numeric(12,2)").IsRequired();
            unit.Property(u => u.Discount).HasColumnName("discount").HasColumnType("numeric(12,2)").IsRequired();
            unit.Property(u => u.Description).HasColumnName("description").HasMaxLength(300).IsRequired();
            unit.Ignore(u => u.Nights);
            unit.Ignore(u => u.Net);
            unit.HasIndex("stay_id").HasDatabaseName("ix_stay_rate_unit_stay");
        });
        builder.Navigation(x => x.RateUnits).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(x => x.StatusHistory, history =>
        {
            history.ToTable("stay_status_change", "boarding");
            history.WithOwner().HasForeignKey("stay_id");
            history.HasKey(h => h.Id);
            history.Property(h => h.Id).HasColumnName("id").ValueGeneratedNever();
            history.Property(h => h.From).HasColumnName("from_status").HasEnumerationValue().IsRequired();
            history.Property(h => h.To).HasColumnName("to_status").HasEnumerationValue().IsRequired();
            history.Property(h => h.ChangedOn).HasColumnName("changed_on").HasColumnType("timestamptz").IsRequired();
            history.Property(h => h.ChangedBy).HasColumnName("changed_by").IsRequired();
            history.Property(h => h.Sequence).HasColumnName("sequence").IsRequired();
            history.HasIndex("stay_id").HasDatabaseName("ix_stay_status_change_stay");
        });
        builder.Navigation(x => x.StatusHistory).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(x => x.AttentionClearedOn).HasColumnName("attention_cleared_on").HasColumnType("timestamptz");
        builder.Property(x => x.AttentionClearedBy).HasColumnName("attention_cleared_by");
        builder.Property(x => x.AttentionClearedNote).HasColumnName("attention_cleared_note").HasMaxLength(StayRules.MaxReasonLength);
        builder.Property(x => x.ChargeRevision).HasColumnName("charge_revision").IsRequired().HasDefaultValue(0);
        builder.Property(x => x.CreditDue).HasColumnName("credit_due").HasColumnType("numeric(12,2)").IsRequired().HasDefaultValue(0m);

        builder.OwnsMany(x => x.Referrals, referral =>
        {
            referral.ToTable("stay_referral", "boarding");
            referral.WithOwner().HasForeignKey("stay_id");
            referral.HasKey(r => r.Id);
            referral.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
            referral.Property(r => r.CatId).HasColumnName("cat_id").IsRequired();
            referral.Property(r => r.Reason).HasColumnName("reason").HasMaxLength(IncidentRules.MaxReferralReasonLength).IsRequired();
            referral.Property(r => r.Urgency).HasColumnName("urgency").HasEnumerationValue().IsRequired();
            referral.Property(r => r.ReferredBy).HasColumnName("referred_by").IsRequired();
            referral.Property(r => r.ReferredOn).HasColumnName("referred_on").HasColumnType("timestamptz").IsRequired();
            referral.Property(r => r.VisitId).HasColumnName("visit_id");
            referral.Property(r => r.ReturnedOn).HasColumnName("returned_on").HasColumnType("timestamptz");
            referral.Ignore(r => r.IsOpen);
            referral.HasIndex("stay_id").HasDatabaseName("ix_stay_referral_stay");
        });
        builder.Navigation(x => x.Referrals).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(x => x.Commitments, commitment =>
        {
            commitment.ToTable("stay_vet_commitment", "boarding");
            commitment.WithOwner().HasForeignKey("stay_id");
            commitment.HasKey(c => c.Id);
            commitment.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
            commitment.Property(c => c.ChargeId).HasColumnName("charge_id").IsRequired();
            commitment.Property(c => c.CatId).HasColumnName("cat_id").IsRequired();
            commitment.Property(c => c.Amount).HasColumnName("amount").HasColumnType("numeric(12,2)").IsRequired();
            commitment.Property(c => c.RecordedOn).HasColumnName("recorded_on").HasColumnType("timestamptz").IsRequired();
            commitment.HasIndex(c => c.ChargeId).IsUnique().HasDatabaseName("ix_stay_vet_commitment_charge");
            commitment.HasIndex("stay_id").HasDatabaseName("ix_stay_vet_commitment_stay");
        });
        builder.Navigation(x => x.Commitments).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(x => x.Approvals, approval =>
        {
            approval.ToTable("stay_vet_approval", "boarding");
            approval.WithOwner().HasForeignKey("stay_id");
            approval.HasKey(a => a.Id);
            approval.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();
            approval.Property(a => a.CatId).HasColumnName("cat_id").IsRequired();
            approval.Property(a => a.ReferralId).HasColumnName("referral_id");
            approval.Property(a => a.Amount).HasColumnName("amount").HasColumnType("numeric(12,2)").IsRequired();
            approval.Property(a => a.Description).HasColumnName("description").HasMaxLength(IncidentRules.MaxVetApprovalDescriptionLength).IsRequired();
            approval.Property(a => a.Status).HasColumnName("status").HasEnumerationValue().IsRequired();
            approval.Property(a => a.RequestedBy).HasColumnName("requested_by").IsRequired();
            approval.Property(a => a.RequestedOn).HasColumnName("requested_on").HasColumnType("timestamptz").IsRequired();
            approval.Property(a => a.DecidedOn).HasColumnName("decided_on").HasColumnType("timestamptz");
            approval.Property(a => a.DecidedBy).HasColumnName("decided_by");
            approval.Property(a => a.DecidedVia).HasColumnName("decided_via")
                .HasConversion(v => v == null ? (int?)null : v.Value, v => v == null ? null : Enumeration.FromValue<AcknowledgementChannel>(v.Value));
            approval.Property(a => a.DecisionNote).HasColumnName("decision_note").HasMaxLength(IncidentRules.MaxApprovalNoteLength);
            approval.HasIndex("stay_id").HasDatabaseName("ix_stay_vet_approval_stay");
        });
        builder.Navigation(x => x.Approvals).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(x => x.Surgeries, surgery =>
        {
            surgery.ToTable("stay_surgery", "boarding");
            surgery.WithOwner().HasForeignKey("stay_id");
            surgery.HasKey(s => s.Id);
            surgery.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
            surgery.Property(s => s.CatId).HasColumnName("cat_id").IsRequired();
            surgery.Property(s => s.StartsOn).HasColumnName("starts_on").HasColumnType("timestamptz").IsRequired();
            surgery.Property(s => s.DischargedOn).HasColumnName("discharged_on").HasColumnType("timestamptz");
            surgery.HasIndex("stay_id").HasDatabaseName("ix_stay_surgery_stay");
        });
        builder.Navigation(x => x.Surgeries).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(x => x.Nights);
        builder.Ignore(x => x.CurrentAllocation);

        builder.HasIndex(x => new { x.BranchId, x.ArrivalOn }).HasDatabaseName("ix_stay_branch_arrival");
        builder.HasIndex(x => new { x.BranchId, x.DepartureOn }).HasDatabaseName("ix_stay_branch_departure");
        builder.HasIndex(x => new { x.BranchId, x.Status }).HasDatabaseName("ix_stay_branch_status");
        builder.HasIndex(x => new { x.CustomerId, x.ArrivalOn }).HasDatabaseName("ix_stay_customer_arrival");
        // The lapse pass reads only reservations: a filtered index (the value comes from the enumeration, never a literal).
        builder.HasIndex(x => new { x.Status, x.DepositDueBy }).HasDatabaseName("ix_stay_reserved_deposit_due")
            .HasFilter($"status = {BoardingStayStatus.Reserved.Value}");
    }
}
