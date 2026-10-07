using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>File Storage (HO-FIL-001) table classification -- HO-XCT-001 §5.2 C-ALL-1, added in
/// the same change that adds the table. Gate-fix 2026-09-27 (ARCHITECTURE HIGH): this file was
/// missing entirely, leaving <c>files.stored_file</c> out of <c>RecordClassification.All</c>.</summary>
public static partial class RecordClassification
{
    private static readonly IReadOnlyList<TableClassification> FilesEntries =
    [
        // Media, not Clinical/Financial/Regulatory: the row is bookkeeping about an upload
        // (who, when, what type/size, which scanner) -- the clinical, financial or regulatory
        // significance of the bytes themselves belongs to whichever context's own record
        // references the file by id (HO-FIL-001 §2: "It knows nothing about cats, visits, stays or
        // products"). Purgeable, not Forbidden: §8.7's own purge/erasure (StoredFilePurgeSource,
        // IStoredFileEraser) is this context's normal, designed hard-delete path, not an exception
        // to a "never delete" rule -- LegalHold and RetainUntil are what actually holds a row back.
        new TableClassification("files", "stored_file", RecordClass.Media, HardDeletePolicy.Purgeable, "HO-FIL-001 §8.1/§8.7"),
    ];
}
