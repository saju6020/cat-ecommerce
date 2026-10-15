using CatEcommerce.Application.CommandHandlers.Validators;
using CatEcommerce.Application.Commands.Cat;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Time.Testing;

namespace CatEcommerce.Application.Tests.Validators;

/// <summary>Bug fix 2026-10-10: the "cannot be in the future" pre-check used the UTC date, so staff in
/// Asia/Dhaka (UTC+6) were refused today's local date from 00:00 to 06:00. 2026-10-09T19:00Z is
/// 2026-10-10 01:00 in Dhaka.</summary>
public sealed class CatDateBoundTests
{
    private static readonly DateOnly LocalToday = new(2026, 10, 10);
    private static FakeTimeProvider DhakaEarlyMorning() => new(DateTimeOffset.Parse("2026-10-09T19:00:00Z"));

    [Fact]
    public void Weight_TodayInDhaka_IsAccepted_DayAfterIsRejected()
    {
        var validator = new RecordCatWeightCommandValidator(DhakaEarlyMorning());
        Assert.DoesNotContain(validator.Validate(Weight(LocalToday)).Errors, e => e.PropertyName == nameof(RecordCatWeightCommand.RecordedOn));
        Assert.Contains(validator.Validate(Weight(LocalToday.AddDays(1))).Errors, e => e.PropertyName == nameof(RecordCatWeightCommand.RecordedOn));
    }

    [Fact]
    public void Neuter_TodayInDhaka_IsAccepted_FarFutureRejected()
    {
        var validator = new RecordCatNeuterStatusCommandValidator(DhakaEarlyMorning());
        Assert.DoesNotContain(validator.Validate(Neuter(LocalToday)).Errors, e => e.PropertyName == nameof(RecordCatNeuterStatusCommand.NeuteredOn));
        Assert.Contains(validator.Validate(Neuter(LocalToday.AddDays(1))).Errors, e => e.PropertyName == nameof(RecordCatNeuterStatusCommand.NeuteredOn));
    }

    [Fact]
    public void Deceased_TodayInDhaka_IsAccepted_FarFutureRejected()
    {
        var validator = new MarkCatDeceasedCommandValidator(DhakaEarlyMorning());
        Assert.DoesNotContain(validator.Validate(new MarkCatDeceasedCommand { CatId = Guid.NewGuid(), DeceasedOn = LocalToday }).Errors, e => e.PropertyName == nameof(MarkCatDeceasedCommand.DeceasedOn));
        Assert.Contains(validator.Validate(new MarkCatDeceasedCommand { CatId = Guid.NewGuid(), DeceasedOn = LocalToday.AddDays(1) }).Errors, e => e.PropertyName == nameof(MarkCatDeceasedCommand.DeceasedOn));
    }

    [Fact]
    public void RegisterAndUpdate_DateOfBirthToday_IsAccepted_FarFutureRejected()
    {
        var register = new RegisterCatCommandValidator(DhakaEarlyMorning());
        Assert.DoesNotContain(register.Validate(Register(LocalToday)).Errors, e => e.PropertyName == nameof(RegisterCatCommand.DateOfBirth));
        Assert.Contains(register.Validate(Register(LocalToday.AddDays(1))).Errors, e => e.PropertyName == nameof(RegisterCatCommand.DateOfBirth));

        var update = new UpdateCatProfileCommandValidator(DhakaEarlyMorning());
        Assert.DoesNotContain(update.Validate(Update(LocalToday)).Errors, e => e.PropertyName == nameof(UpdateCatProfileCommand.DateOfBirth));
        Assert.Contains(update.Validate(Update(LocalToday.AddDays(1))).Errors, e => e.PropertyName == nameof(UpdateCatProfileCommand.DateOfBirth));
    }

    private static RegisterCatCommand Register(DateOnly dob) => new()
    {
        CatId = Guid.NewGuid(), OwnerId = Guid.NewGuid(), Name = "Mittens", Species = "Cat", Sex = CatSex.Female,
        DateOfBirth = dob, DateOfBirthPrecision = DateOfBirthPrecision.Exact
    };

    private static UpdateCatProfileCommand Update(DateOnly dob) => new()
    {
        CatId = Guid.NewGuid(), Name = "Mittens", Sex = CatSex.Female,
        DateOfBirth = dob, DateOfBirthPrecision = DateOfBirthPrecision.Exact
    };

    private static RecordCatWeightCommand Weight(DateOnly on) => new() { WeightRecordId = Guid.NewGuid(), CatId = Guid.NewGuid(), Kilograms = 4m, RecordedByStaffId = Guid.NewGuid(), RecordedOn = on };
    private static RecordCatNeuterStatusCommand Neuter(DateOnly on) => new() { CatId = Guid.NewGuid(), NeuterStatus = NeuterStatus.Neutered, NeuteredOn = on };
}
