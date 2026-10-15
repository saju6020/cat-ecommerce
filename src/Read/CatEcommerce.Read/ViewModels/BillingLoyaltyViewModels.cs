namespace CatEcommerce.Read.ViewModels;

public sealed record LoyaltyEntryViewModel(
    Guid EntryId, string Kind, int Points, Guid? InvoiceId, string? InvoiceNumber, Guid? CreditNoteId, int Shortfall, DateTimeOffset OccurredOn);

public sealed record LoyaltyViewModel(bool Enabled, int Balance, PagedResult<LoyaltyEntryViewModel> Entries);
