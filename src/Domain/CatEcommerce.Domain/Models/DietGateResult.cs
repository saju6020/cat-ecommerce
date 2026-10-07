using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Models;

/// <summary>HO-PHM-001 §8.10's <c>IDietAuthorisationGate.CheckAsync</c> result (ADR-149).</summary>
public sealed record DietGateResult(DietGateOutcome Outcome, Guid? AuthorisationId, DateOnly? ValidTo, string Message);
