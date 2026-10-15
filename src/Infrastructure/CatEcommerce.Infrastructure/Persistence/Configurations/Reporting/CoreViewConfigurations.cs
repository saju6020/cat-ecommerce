using CatEcommerce.Infrastructure.Persistence.ReportingViews;

namespace CatEcommerce.Infrastructure.Persistence.Configurations.Reporting;

public sealed class RvStaffConfiguration() : ReportViewConfiguration<RvStaffRow>("rv_staff");
public sealed class RvCustomerConfiguration() : ReportViewConfiguration<RvCustomerRow>("rv_customer");
public sealed class RvBranchConfiguration() : ReportViewConfiguration<RvBranchRow>("rv_branch");
public sealed class RvStaffBranchConfiguration() : ReportViewConfiguration<RvStaffBranchRow>("rv_staff_branch");
