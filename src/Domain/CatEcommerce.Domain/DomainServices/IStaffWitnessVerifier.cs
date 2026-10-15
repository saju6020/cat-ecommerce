using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-PHM-001 §8.9 (ADR-146, BR-7) -- a witness is a PERSON, not a dropdown: the second staff member authenticates with their OWN identifier (mobile
/// or email) and password in the same request. Verified only if the account exists, is not locked, the password verifies, the account holds a role in
/// <c>ControlledDrugRules.WitnessRoles</c>, and it is not the person acting. EVERY failure is the same <c>WitnessNotVerified</c> -- never which part
/// failed. A wrong password counts toward the witness account's lockout exactly as a sign-in failure does and is audited with the masked identifier;
/// success is audited with the witness id and the source. No session is created. The password is on the command only and never reaches a log, an
/// event, audit capture or the outbox.
/// </summary>
public interface IStaffWitnessVerifier
{
    Task<WitnessVerification> VerifyAsync(
        string identifier, string password, Guid actingStaffId, ControlledDrugSourceType source, CancellationToken ct);
}
