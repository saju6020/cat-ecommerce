namespace CatEcommerce.Read.ViewModels;

/// <summary>HO-XCT-001 section 12.4 Q2. <c>Verdict</c> is <c>Pass</c>, <c>Fail</c> or <c>InsufficientData</c> (fewer than the minimum number of samples).</summary>
public sealed record WebVitalSummaryRow(string RouteTemplate, string ViewportClass, string Metric, int SampleCount, decimal P75, string Verdict);

public sealed record WebVitalBudgetsViewModel(int LcpMs, int InpMs, decimal Cls);

public sealed record WebVitalsSummaryViewModel(DateTimeOffset From, DateTimeOffset To, WebVitalBudgetsViewModel Budgets, IReadOnlyList<WebVitalSummaryRow> Rows);
