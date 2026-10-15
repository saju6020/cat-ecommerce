namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-EMR-001 §8.5/§10.3 -- bounds of a diagnostic order and its append-only results.</summary>
public static class DiagnosticRules
{
    public const int MaxDescriptionLength = 500;
    public const int MaxRequestedAnalytes = 50;
    public const int MaxResultSets = 10;
    public const int MaxResultValuesPerSet = 100;
    public const int MaxAttachments = 20;
    public const int MaxAttachmentNoteLength = 500;
    public const int MaxResultNoteLength = 500;
    public const int MaxValueNameLength = 100;
    public const int MaxTextValueLength = 100;
    public const int MaxUnitLength = 30;
    public const int MaxNormalTextLength = 50;
    public const int MaxAnalyteCodeLength = 30;
    public const int MaxCancelReasonLength = 500;
}
