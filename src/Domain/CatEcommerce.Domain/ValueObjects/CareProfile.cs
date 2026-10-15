using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.ValueObjects;

/// <summary>One scheduled feed (FR-BRD-6). <see cref="HouseFoodSkuId"/> is a Shop SKU Inventory may issue against (BR-3) -- optional.</summary>
public sealed record FeedingPlan(TimeOnly Time, string Portion, FoodSource FoodSource, string FoodDescription, Guid? HouseFoodSkuId);

/// <summary>One medication with its dose times and the optional days it runs on; <see cref="FromOn"/>/<see cref="ToOn"/> lie within the stay.</summary>
public sealed record MedicationPlan(string Name, string Dose, string Route, IReadOnlyList<TimeOnly> Times, string? Instructions, DateOnly? FromOn, DateOnly? ToOn);

public sealed record EmergencyContact(string Name, string Phone, string? Relationship);

/// <summary>
/// HO-BRD-001 section 8.7 (US-BRD-05, FR-BRD-6): the per-cat care profile -- feeding schedule and food source, medications with times, litter preference,
/// whether the owner authorises the clinic to examine and treat, the veterinary spending limit, an emergency contact and behaviour notes. A household of
/// three cats has three profiles on one reservation. The stay stores what is submitted; the front end prefills medications and notes from the cat profile.
/// </summary>
public sealed record CareProfile(
    IReadOnlyList<FeedingPlan> Feedings,
    IReadOnlyList<MedicationPlan> Medications,
    LitterType Litter,
    string? LitterNote,
    bool VetVisitAuthorised,
    decimal VetSpendingLimit,
    EmergencyContact EmergencyContact,
    string? Notes);
