using CatEcommerce.Infrastructure.Persistence.ReportingViews;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicReportViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ReportingViewSql.Load("rv_clinic_appointment.v1", ("HOLD_STATUSES", ReportingViewSql.List(AppointmentStatus.Held, AppointmentStatus.HoldExpired, AppointmentStatus.HoldReleased))));
            migrationBuilder.Sql(ReportingViewSql.Load("rv_cat_vaccination.v1"));
            migrationBuilder.Sql(ReportingViewSql.Load("rv_cat_parasite_treatment.v1"));
            migrationBuilder.Sql(ReportingViewSql.Load("rv_cat_last_branch.v1", ("HOLD_STATUSES", ReportingViewSql.List(AppointmentStatus.Held, AppointmentStatus.HoldExpired, AppointmentStatus.HoldReleased))));
            migrationBuilder.Sql(ReportingViewSql.Load("rv_care_reminder_sent.v1", ("CARE_KINDS", ReportingViewSql.List(NotificationSubjectKind.CatVaccinationDue, NotificationSubjectKind.CatDewormingDue)), ("SENT_STATUSES", ReportingViewSql.List(NotificationStatus.Sent, NotificationStatus.InboxDelivered))));
            migrationBuilder.Sql(ReportingViewSql.Load("rv_surgery.v1", ("SURGERY_SOURCE", ControlledDrugSourceType.Surgery.Value.ToString())));
            migrationBuilder.Sql(ReportingViewSql.Load("rv_visit_open.v1"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ReportingViewSql.Drop("rv_visit_open"));
            migrationBuilder.Sql(ReportingViewSql.Drop("rv_surgery"));
            migrationBuilder.Sql(ReportingViewSql.Drop("rv_care_reminder_sent"));
            migrationBuilder.Sql(ReportingViewSql.Drop("rv_cat_last_branch"));
            migrationBuilder.Sql(ReportingViewSql.Drop("rv_cat_parasite_treatment"));
            migrationBuilder.Sql(ReportingViewSql.Drop("rv_cat_vaccination"));
            migrationBuilder.Sql(ReportingViewSql.Drop("rv_clinic_appointment"));
        }
    }
}
