namespace CatEcommerce.Domain.ValueObjects;

/// <summary>
/// HO-EMR-001 §8.2 -- the visit's SOAP note: Subjective, Objective, Assessment, Plan and the
/// home-care <see cref="DischargeInstructions"/>, all <b>plain text</b> (never stored or rendered as
/// markup). While the visit is open this <i>is</i> the draft record (ADR-122); it changes only on an
/// explicit save. <see cref="NoteSavedOn"/> is <c>null</c> until the first save (US-CLN-20: a template
/// is offered only while no note has ever been saved).
/// </summary>
public sealed record SoapNote
{
    public string Subjective { get; init; } = "";
    public string Objective { get; init; } = "";
    public string Assessment { get; init; } = "";
    public string Plan { get; init; } = "";
    public string DischargeInstructions { get; init; } = "";
    public DateTimeOffset? NoteSavedOn { get; init; }
    public Guid? NoteSavedBy { get; init; }
    public Guid? AppliedTemplateId { get; init; }
    public int? AppliedTemplateVersion { get; init; }

    public static SoapNote Empty => new();

    public bool HasBeenSaved => NoteSavedOn is not null;
}
