namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-ADM-001 §10.1 / FR-CLN-1.</summary>
public sealed class ServiceCategory(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ServiceCategory Consultation = new(1, nameof(Consultation));
    public static readonly ServiceCategory Vaccination = new(2, nameof(Vaccination));
    public static readonly ServiceCategory Spay = new(3, nameof(Spay));
    public static readonly ServiceCategory Neuter = new(4, nameof(Neuter));
    public static readonly ServiceCategory Dental = new(5, nameof(Dental));
    public static readonly ServiceCategory Grooming = new(6, nameof(Grooming));
    public static readonly ServiceCategory Emergency = new(7, nameof(Emergency));
    public static readonly ServiceCategory FollowUp = new(8, nameof(FollowUp));
}
