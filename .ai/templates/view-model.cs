using MongoDB.Bson.Serialization.Attributes;
using Platform.Infrastructure.Core.Models;

namespace {{RootNamespace}}.Read.ViewModels;

/// <summary>
/// Template for Read-side ViewModel.
/// Stored in MongoDB, updated by Read EventHandlers, and retrieved by Queries.
/// </summary>
[BsonIgnoreExtraElements]
public class [ResourceName]ViewModel : ViewModelBase
{
    // Id is inherited from ViewModelBase
    public string Name { get; set; } = default!;
    
    // Example: Method to map event payload to this view model (used by EventHandlers)
    // public void Map([ResourceName]CreatedEvent @event) 
    // {
    //     Id = @event.ResourceId;
    //     Name = @event.Name;
    // }
}
