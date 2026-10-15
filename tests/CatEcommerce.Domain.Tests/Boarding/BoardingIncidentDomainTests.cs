using CatEcommerce.Domain.Aggregates.Boarding;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;
using Xunit;

namespace CatEcommerce.Domain.Tests.Boarding;

/// <summary>HO-BRD-001 Phase F -- U-F1 .. U-F8: the incident and stay-media aggregates.</summary>
public sealed class BoardingIncidentDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 10, 0, 0, TimeSpan.Zero);
    private static readonly UserContext Staff = new(Guid.NewGuid(), Guid.NewGuid());

    private static BoardingIncidentAggregate Recorded(IncidentSeverity severity)
    {
        var incident = new BoardingIncidentAggregate(Guid.NewGuid());
        incident.Record(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), IncidentKind.Injury, severity, "Scratch", "Cleaned", Now.AddMinutes(-5), Now, Staff, Guid.NewGuid());
        return incident;
    }

    [Theory]
    [InlineData("Low", false)]
    [InlineData("Medium", true)]
    [InlineData("High", true)]
    public void U_F1_OnlyMediumAndHighIncidentsNotifyTheOwner(string severity, bool notified)
    {
        var incident = Recorded(severity == "Low" ? IncidentSeverity.Low : severity == "Medium" ? IncidentSeverity.Medium : IncidentSeverity.High);

        Assert.Equal(notified, incident.OwnerNotified);
        Assert.Equal(IncidentStatus.Open, incident.Status);
    }

    [Fact]
    public void U_F2_AFutureIncident_OrAMissingDescription_IsRefused()
    {
        var incident = new BoardingIncidentAggregate(Guid.NewGuid());

        Assert.Throws<DomainRuleViolationException>(() =>
            incident.Record(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), IncidentKind.Fight, IncidentSeverity.Low, "x", "y", Now.AddHours(1), Now, Staff, Guid.NewGuid()));
        Assert.Throws<DomainRuleViolationException>(() =>
            incident.Record(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), IncidentKind.Fight, IncidentSeverity.Low, " ", "y", Now.AddMinutes(-1), Now, Staff, Guid.NewGuid()));
    }

    [Fact]
    public void U_F3_AcknowledgementIsIdempotent_TheFirstChannelStands()
    {
        var incident = Recorded(IncidentSeverity.High);

        incident.Acknowledge(Now.AddMinutes(3), AcknowledgementChannel.Portal);
        incident.Acknowledge(Now.AddMinutes(9), AcknowledgementChannel.Staff);

        Assert.Equal(Now.AddMinutes(3), incident.OwnerAcknowledgedOn);
        Assert.Equal(AcknowledgementChannel.Portal, incident.AcknowledgedVia);
    }

    [Fact]
    public void U_F4_AHighUnacknowledgedIncident_EscalatesOnlyAfterTheConfiguredMinutes_AndOnlyOnce()
    {
        var incident = Recorded(IncidentSeverity.High);

        incident.Escalate(Now.AddMinutes(29), 30, Staff, Guid.NewGuid());
        Assert.Null(incident.EscalatedOn);

        incident.Escalate(Now.AddMinutes(30), 30, Staff, Guid.NewGuid());
        incident.Escalate(Now.AddMinutes(45), 30, Staff, Guid.NewGuid());
        Assert.Equal(Now.AddMinutes(30), incident.EscalatedOn);
        Assert.True(incident.NeedsPhoneCall);
    }

    [Theory]
    [InlineData("Low")]
    [InlineData("Medium")]
    public void U_F5_OnlyAHighIncidentEscalates(string severity)
    {
        var incident = Recorded(severity == "Low" ? IncidentSeverity.Low : IncidentSeverity.Medium);

        incident.Escalate(Now.AddHours(5), 30, Staff, Guid.NewGuid());

        Assert.Null(incident.EscalatedOn);
    }

    [Fact]
    public void U_F6_AnAcknowledgedIncident_NeverEscalates()
    {
        var incident = Recorded(IncidentSeverity.High);
        incident.Acknowledge(Now.AddMinutes(5), AcknowledgementChannel.Portal);

        incident.Escalate(Now.AddHours(2), 30, Staff, Guid.NewGuid());

        Assert.Null(incident.EscalatedOn);
    }

    [Fact]
    public void U_F7_APhoneCallNeedsAnOutcome_AndReachingTheOwnerAcknowledges()
    {
        var incident = Recorded(IncidentSeverity.High);
        incident.Escalate(Now.AddMinutes(31), 30, Staff, Guid.NewGuid());

        Assert.Throws<DomainRuleViolationException>(() => incident.RecordPhoneCall(Now.AddMinutes(40), " ", true, Staff));
        incident.RecordPhoneCall(Now.AddMinutes(40), "Spoke to the owner", true, Staff);

        Assert.False(incident.NeedsPhoneCall);
        Assert.Equal(AcknowledgementChannel.Staff, incident.AcknowledgedVia);
    }

    [Fact]
    public void U_F8_ResolvingNeedsAResolution_AndHidingOrPurgingMediaIsOneWay()
    {
        var incident = Recorded(IncidentSeverity.Medium);
        Assert.Throws<DomainRuleViolationException>(() => incident.Resolve(Now.AddHours(1), " ", Staff));
        incident.Resolve(Now.AddHours(1), "Healed", Staff);
        Assert.Equal(IncidentStatus.Resolved, incident.Status);

        var media = new StayMediaAggregate(Guid.NewGuid());
        media.Add(Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid(), "Nap", new DateOnly(2026, 11, 3), Now, Staff);
        Assert.Throws<DomainRuleViolationException>(() => media.Hide(" "));
        media.Hide("Blurry");
        media.MarkPurged();
        media.Hide("again");

        Assert.Equal(StayMediaStatus.Purged, media.Status);
    }

    [Fact]
    public void U_F9_AMediaCaptionOverTheLimit_IsRefused_WithTheMediaCode()
    {
        var media = new StayMediaAggregate(Guid.NewGuid());

        var error = Assert.Throws<DomainRuleViolationException>(() =>
            media.Add(Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid(), new string('x', IncidentRules.MaxCaptionLength + 1), new DateOnly(2026, 11, 3), Now, Staff));

        Assert.Equal(BusinessViolationCodes.InvalidMediaReference, error.ErrorCode);
    }
}
