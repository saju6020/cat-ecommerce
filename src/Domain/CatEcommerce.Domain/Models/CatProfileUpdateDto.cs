using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

public sealed record CatProfileUpdateDto(
    string Name,
    CatSex Sex,
    DateOnly? DateOfBirth,
    DateOfBirthPrecision DateOfBirthPrecision,
    string? Breed,
    string? ColourAndMarkings,
    DateOnly Today,
    UserContext UserContext,
    Guid CorrelationId);
