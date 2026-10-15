namespace CatEcommerce.Read.ViewModels;

public sealed record CashUpLineViewModel(
    Guid Id, string Kind, string? Method, string? Collector, string? RefundMethod, MoneyViewModel Amount, bool CountsInCash, bool CarriedIn, string? Label);

public sealed record CashUpCarriedInViewModel(Guid Id, string Kind, string? Label, MoneyViewModel Amount);

public sealed record CashUpExpectedViewModel(
    MoneyViewModel Cash, int CashCount, MoneyViewModel Card, int CardCount, MoneyViewModel Transfer, int TransferCount, MoneyViewModel CashRefunds,
    MoneyViewModel CashReversals, MoneyViewModel CarriedInTotal, IReadOnlyList<CashUpCarriedInViewModel> CarriedIn, MoneyViewModel ExpectedCash);

public sealed record CashUpViewModel(
    DateOnly BusinessDate, string Status, int Version, CashUpExpectedViewModel Expected, IReadOnlyList<CashUpLineViewModel> Lines, MoneyViewModel? Counted,
    MoneyViewModel? Variance, string? Explanation, int PendingApprovals, Guid? CountedBy, DateTimeOffset? CountedOn, Guid? ClosedBy, string? ClosedByName,
    DateTimeOffset? ClosedOn);
