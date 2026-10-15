using CatEcommerce.Application.Commands.Visit;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Application.DataMappers;

/// <summary>HO-EMR-001 §11.3 -- visit command to domain DTO. Enumeration strings were already checked by
/// each command's own <c>MustBeValidEnumeration</c> rule (the same contract as <c>AppointmentMapper</c>).</summary>
public static class VisitMapper
{
    public static IReadOnlyList<VitalSign> ParseVitalSigns(IEnumerable<string> names) =>
        names.Select(AppointmentMapper.ParseEnum<VitalSign>).Distinct().ToList();

    public static VitalsDto ToDomainDto(this RecordVitalsCommand c, DateTimeOffset now) =>
        new(c.VitalsEntryId, (c.MeasuredOn ?? now).ToUniversalTime(), c.WeightKg, c.TemperatureC, c.HeartRateBpm, c.RespiratoryRatePerMin,
            c.BodyConditionScore, c.PainScore, ParseVitalSigns(c.ConfirmedOutOfRange), c.Note,
            c.UserContext.UserId, now, c.UserContext, c.CorrelationId);

    public static ConsultationDraftDto ToDomainDto(this SaveConsultationDraftCommand c, VisitStatus visitStatus, DateTimeOffset now) =>
        new(c.VisitId, c.UserContext.UserId, visitStatus, c.Subjective, c.Objective, c.Assessment, c.Plan, c.DischargeInstructions,
            c.ProblemCodeIds, now, c.UserContext, c.CorrelationId);

    public static VisitAddendumDto ToDomainDto(this AddVisitAddendumCommand c, Guid catId, VisitStatus visitStatus, DateTimeOffset now) =>
        new(c.VisitId, catId, visitStatus, c.Text, c.Reason, c.UserContext.UserId, now, c.UserContext, c.CorrelationId);
}
