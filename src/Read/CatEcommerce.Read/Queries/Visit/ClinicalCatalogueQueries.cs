using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Visit;

// HO-EMR-001 §12.6 -- catalogue list/detail queries.

public sealed class GetDiagnosisCodesQuery : Query<PagedResult<DiagnosisCodeViewModel>>
{
    public string? Search { get; init; }
    public bool ActiveOnly { get; init; }
}

public sealed class GetDiagnosisCodeQuery : Query<DiagnosisCodeViewModel?>
{
    public required Guid DiagnosisCodeId { get; init; }
}

public sealed class GetLabAnalytesQuery : Query<PagedResult<LabAnalyteViewModel>>
{
    public string? Search { get; init; }
    public bool ActiveOnly { get; init; }
}

public sealed class GetLabAnalyteQuery : Query<LabAnalyteViewModel?>
{
    public required Guid LabAnalyteId { get; init; }
}

public sealed class GetNoteTemplatesQuery : Query<PagedResult<NoteTemplateViewModel>>
{
    public Guid? ServiceId { get; init; }
    public bool ActiveOnly { get; init; }
}

public sealed class GetNoteTemplateQuery : Query<NoteTemplateViewModel?>
{
    public required Guid NoteTemplateId { get; init; }
}
