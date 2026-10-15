namespace CatEcommerce.Read.ViewModels;

/// <summary>HO-SRG-001 §12.4 -- <c>GET /api/staff/surgery-protocols</c>. <c>IsDefault</c> is true while the service has no protocol of its own.</summary>
public sealed record SurgeryProtocolViewModel(
    Guid ServiceId, string ServiceName, string Category, bool IsDefault, string FastingInstructions, int FastingHours, string PostOpInstructions,
    IReadOnlyList<SurgeryProtocolFollowUpViewModel> FollowUps, DateTimeOffset? UpdatedOn, int Version);

public sealed record SurgeryProtocolFollowUpViewModel(string Kind, int DayOffset, int WindowFromDay, int WindowToDay);
