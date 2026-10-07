namespace CatEcommerce.Shared.Common.Constants;

/// <summary>File Storage (HO-FIL-001 §10.2) violation codes.</summary>
public static partial class BusinessViolationCodes
{
    public const string FileTooLarge = nameof(FileTooLarge);
    public const string FileTypeNotAllowed = nameof(FileTypeNotAllowed);
    public const string FileContentMismatch = nameof(FileContentMismatch);
    public const string FileRejected = nameof(FileRejected);
    public const string FileScanUnavailable = nameof(FileScanUnavailable);
    public const string FileObjectMissing = nameof(FileObjectMissing);
    public const string FileNotFound = nameof(FileNotFound);
    public const string FilePurposeNotAllowedForCaller = nameof(FilePurposeNotAllowedForCaller);
    public const string InvalidFileName = nameof(InvalidFileName);
    public const string FileLegallyHeld = nameof(FileLegallyHeld);

    // Gate-fix 2026-09-27 (CODE_REVIEW LOW): a Files-scoped name for an invalid FilePurpose query
    // value, deliberately not the generic `InvalidEnumValue` name ho/CAT-001-gates independently
    // coins for the same defect in CatsController -- see HO-FIL-001.md §1 Amended and
    // FilesController.TryParseEnum's own doc comment.
    public const string FilesInvalidPurpose = nameof(FilesInvalidPurpose);
}
