using CatEcommerce.Infrastructure.Persistence.ReportingViews;

namespace CatEcommerce.Infrastructure.Persistence.Configurations.Reporting;

public sealed class RvClinicAppointmentConfiguration() : ReportViewConfiguration<RvClinicAppointmentRow>("rv_clinic_appointment");
public sealed class RvCatVaccinationConfiguration() : ReportViewConfiguration<RvCatVaccinationRow>("rv_cat_vaccination");
public sealed class RvCatParasiteTreatmentConfiguration() : ReportViewConfiguration<RvCatParasiteTreatmentRow>("rv_cat_parasite_treatment");
public sealed class RvCatLastBranchConfiguration() : ReportViewConfiguration<RvCatLastBranchRow>("rv_cat_last_branch");
public sealed class RvCareReminderSentConfiguration() : ReportViewConfiguration<RvCareReminderSentRow>("rv_care_reminder_sent");
public sealed class RvSurgeryConfiguration() : ReportViewConfiguration<RvSurgeryRow>("rv_surgery");
public sealed class RvVisitOpenConfiguration() : ReportViewConfiguration<RvVisitOpenRow>("rv_visit_open");
