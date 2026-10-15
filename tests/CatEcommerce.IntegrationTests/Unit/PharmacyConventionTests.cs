using System.Reflection;
using CatEcommerce.Api.Background;
using CatEcommerce.Application.CommandHandlers.Validators;
using CatEcommerce.Application.Commands.Prescription;
using CatEcommerce.Infrastructure.Configuration;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Infrastructure.Core.Commands;
using Xunit;

namespace CatEcommerce.IntegrationTests.Unit;

/// <summary>HO-PHM-001 §15 U-X2 and U-X4 -- a witness password never leaves the command, and the worker survives a throwing pass.</summary>
public sealed class PharmacyConventionTests
{
    private const string Secret = "S3cret-witness-pass-phrase!";

    // ── U-X2 ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void U_X2_EveryCommandCarryingAWitnessPassword_OverridesToString_WithoutIt()
    {
        var carriers = typeof(RecordControlledDrugCorrectionCommand).Assembly.GetTypes()
            .Where(t => typeof(Command).IsAssignableFrom(t) && !t.IsAbstract && t.GetProperty("WitnessPassword") is not null).ToList();

        Assert.True(carriers.Count >= 3, "The correction, dispense and reverse commands carry a witness password.");

        foreach (var type in carriers)
        {
            var toString = type.GetMethod(nameof(ToString), BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes)!;
            Assert.True(toString.DeclaringType == type, $"{type.Name} must override ToString() so the password cannot reach a log.");

            var instance = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type);
            type.GetProperty("WitnessPassword")!.SetValue(instance, Secret);
            type.GetProperty("WitnessIdentifier")!.SetValue(instance, "01712345678");
            Assert.DoesNotContain(Secret, instance.ToString());
            Assert.DoesNotContain("01712345678", instance.ToString());
        }
    }

    [Fact]
    public void U_X2b_ValidatorMessages_NeverEchoTheWitnessPassword()
    {
        var corrections = new RecordControlledDrugCorrectionCommandValidator().Validate(new RecordControlledDrugCorrectionCommand
        {
            EntryId = Guid.NewGuid(), CorrectsEntryId = Guid.Empty, QuantityDelta = 1.234m, Reason = "", WitnessIdentifier = "", WitnessPassword = Secret + new string('x', 2000), BranchId = Guid.Empty,
            UserContext = new Platform.Infrastructure.Common.Security.UserContext(Guid.NewGuid(), Guid.NewGuid()), CorrelationId = Guid.NewGuid()
        });

        var dispensing = new DispensePrescriptionCommandValidator().Validate(new DispensePrescriptionCommand
        {
            DispensingId = Guid.Empty, PrescriptionId = Guid.Empty, Lines = [], WitnessIdentifier = "x", WitnessPassword = Secret + new string('x', 2000), BranchId = Guid.Empty,
            UserContext = new Platform.Infrastructure.Common.Security.UserContext(Guid.NewGuid(), Guid.NewGuid()), CorrelationId = Guid.NewGuid()
        });

        var messages = corrections.Errors.Concat(dispensing.Errors).Select(e => e.ErrorMessage).ToList();
        Assert.NotEmpty(messages);
        Assert.DoesNotContain(messages, m => m.Contains(Secret));
    }

    // ── U-X4 ────────────────────────────────────────────────────────────────────────────────────

    private sealed class CountingLogger<T> : ILogger<T>
    {
        public int Errors;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Error) Interlocked.Increment(ref Errors);
        }
    }

    [Fact]
    public async Task U_X4_APassThatThrows_IsLogged_AndTheNextPollStillRuns()
    {
        var polls = 0;
        var services = new ServiceCollection();
        services.AddScoped<PharmacyWorkCycle>(_ =>
        {
            Interlocked.Increment(ref polls);
            throw new InvalidOperationException("boom");
        });

        await using var provider = services.BuildServiceProvider();
        var logger = new CountingLogger<PharmacyWorker>();
        var options = Options.Create(new PharmacyOptions { Worker = new PharmacyWorkerOptions { Enabled = true, IntervalSeconds = 1 } });
        var worker = new PharmacyWorker(provider.GetRequiredService<IServiceScopeFactory>(), options, logger);

        await worker.StartAsync(CancellationToken.None);
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (Volatile.Read(ref polls) < 2 && DateTime.UtcNow < deadline) await Task.Delay(100);
        await worker.StopAsync(CancellationToken.None);

        Assert.True(polls >= 2, $"The worker polled {polls} time(s); a throwing pass must not stop the next one.");
        Assert.True(logger.Errors >= 2);
    }

    [Fact]
    public async Task U_X4b_ADisabledWorker_RunsNothing()
    {
        var polls = 0;
        var services = new ServiceCollection();
        services.AddScoped<PharmacyWorkCycle>(_ =>
        {
            Interlocked.Increment(ref polls);
            throw new InvalidOperationException("must not run");
        });

        await using var provider = services.BuildServiceProvider();
        var worker = new PharmacyWorker(
            provider.GetRequiredService<IServiceScopeFactory>(), Options.Create(new PharmacyOptions { Worker = new PharmacyWorkerOptions { Enabled = false } }), new CountingLogger<PharmacyWorker>());

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(300);
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(0, polls);
    }
}
