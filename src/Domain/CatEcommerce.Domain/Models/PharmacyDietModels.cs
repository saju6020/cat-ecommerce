namespace CatEcommerce.Domain.Models;

// HO-PHM-001 §8.10 -- diet authorisation DTOs.

public sealed record DietAuthorisationDto(
    Guid CatId,
    Guid CustomerId,
    Guid ProductId,
    string ProductNameSnapshot,
    Guid VisitId,
    Guid AuthorisedBy,
    DateOnly ValidFrom,
    DateOnly ValidTo,
    string? Notes,
    DateOnly Today,
    int MaxDays,
    DateTimeOffset Now,
    PharmacyActor Actor);
