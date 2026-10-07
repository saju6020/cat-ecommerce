using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Administration;

public sealed class GetPublicServicesQuery : Query<IReadOnlyList<PublicServiceViewModel>>;

public sealed class GetAdminServicesQuery : Query<IReadOnlyList<AdminServiceViewModel>>;

public sealed class GetAdminServiceQuery : Query<AdminServiceViewModel?>
{
    public required Guid ServiceId { get; init; }
}

public sealed class GetServicePricesQuery : Query<IReadOnlyList<ServicePriceViewModel>>
{
    public required Guid ServiceId { get; init; }
}

public sealed class GetTaxClassesQuery : Query<IReadOnlyList<TaxClassViewModel>>;

public sealed class GetTaxRatesQuery : Query<IReadOnlyList<TaxRateViewModel>>
{
    public required Guid TaxClassId { get; init; }
}

public sealed class GetSettingsQuery : Query<IReadOnlyList<SettingViewModel>>
{
    public Guid? BranchId { get; init; }
}
