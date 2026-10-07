using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using Microsoft.Extensions.Hosting;

namespace CatEcommerce.Infrastructure.Files;

/// <summary>
/// HO-FIL-001 §8.5: the Development-only scanner -- no <c>clamd</c> needed to run the solution
/// locally. Always <see cref="ScanOutcome.Clean"/>. The constructor itself throws outside
/// Development so it can never be constructed anywhere else, even by a wiring mistake -- the
/// enforcement mechanism for "no configuration flag skips the scan outside Development" (§14).
/// </summary>
public sealed class DevelopmentNoOpScanner : IMalwareScanner
{
    public DevelopmentNoOpScanner(IHostEnvironment environment)
    {
        if (!environment.IsDevelopment())
            throw new InvalidOperationException(
                $"{nameof(DevelopmentNoOpScanner)} must never be constructed outside Development (ADR-319).");
    }

    public Task<ScanResult> ScanAsync(Stream content, CancellationToken ct) =>
        Task.FromResult(new ScanResult(ScanOutcome.Clean, "DevelopmentNoOp"));
}
