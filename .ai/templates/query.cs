using {{RootNamespace}}.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace {{RootNamespace}}.Read.Queries.[ContextName];

/// <summary>
/// Template for CQRS Query definition (Read side).
/// Must extend Query&lt;TViewModel&gt;. Contains only criteria/parameters.
/// </summary>
public class Get[ResourceName]Query : Query<[ResourceName]ViewModel?>
{
    public Guid Id { get; set; }
}
