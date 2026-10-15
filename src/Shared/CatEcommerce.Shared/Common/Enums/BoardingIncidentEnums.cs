namespace CatEcommerce.Shared.Common.Enums;

// HO-BRD-001 §10.1 -- incidents, vet approvals and stay media.

public sealed class IncidentKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly IncidentKind Injury = new(1, nameof(Injury));
    public static readonly IncidentKind Illness = new(2, nameof(Illness));
    public static readonly IncidentKind EscapeAttempt = new(3, nameof(EscapeAttempt));
    public static readonly IncidentKind Fight = new(4, nameof(Fight));
    public static readonly IncidentKind RefusalToEat = new(5, nameof(RefusalToEat));
    public static readonly IncidentKind Other = new(6, nameof(Other));
}

public sealed class IncidentSeverity(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly IncidentSeverity Low = new(1, nameof(Low));
    public static readonly IncidentSeverity Medium = new(2, nameof(Medium));
    public static readonly IncidentSeverity High = new(3, nameof(High));
}

public sealed class IncidentStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly IncidentStatus Open = new(1, nameof(Open));
    public static readonly IncidentStatus Resolved = new(2, nameof(Resolved));
}

public sealed class AcknowledgementChannel(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly AcknowledgementChannel Portal = new(1, nameof(Portal));
    public static readonly AcknowledgementChannel Staff = new(2, nameof(Staff));
}

public sealed class VetApprovalStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly VetApprovalStatus Pending = new(1, nameof(Pending));
    public static readonly VetApprovalStatus Approved = new(2, nameof(Approved));
    public static readonly VetApprovalStatus Declined = new(3, nameof(Declined));
}

public sealed class StayMediaStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly StayMediaStatus Visible = new(1, nameof(Visible));
    public static readonly StayMediaStatus Hidden = new(2, nameof(Hidden));
    public static readonly StayMediaStatus Purged = new(3, nameof(Purged));
}
