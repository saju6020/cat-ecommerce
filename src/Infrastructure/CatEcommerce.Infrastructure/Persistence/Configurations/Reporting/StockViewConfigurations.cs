using CatEcommerce.Infrastructure.Persistence.ReportingViews;

namespace CatEcommerce.Infrastructure.Persistence.Configurations.Reporting;

public sealed class RvStockLedgerConfiguration() : ReportViewConfiguration<RvStockLedgerRow>("rv_stock_ledger");
public sealed class RvBatchConfiguration() : ReportViewConfiguration<RvBatchRow>("rv_batch");
public sealed class RvStockLocationConfiguration() : ReportViewConfiguration<RvStockLocationRow>("rv_stock_location");
public sealed class RvConsignmentConfiguration() : ReportViewConfiguration<RvConsignmentRow>("rv_consignment");
public sealed class RvSkuConfiguration() : ReportViewConfiguration<RvSkuRow>("rv_sku");
public sealed class RvStockPositionConfiguration() : ReportViewConfiguration<RvStockPositionRow>("rv_stock_position");
public sealed class RvReorderPolicyConfiguration() : ReportViewConfiguration<RvReorderPolicyRow>("rv_reorder_policy");
public sealed class RvOnOrderConfiguration() : ReportViewConfiguration<RvOnOrderRow>("rv_on_order");
