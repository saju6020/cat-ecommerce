using System.Globalization;
using CatEcommerce.Domain.ValueObjects;

namespace CatEcommerce.Read.ViewModels;

/// <summary>HO-SHP-001 section 12.4 -- reuses HO-ADM-001's <c>MoneyViewModel(Amount, CurrencyCode)</c>
/// shape (<c>{ "amount": "1500.00", "currencyCode": "BDT" }</c>). <c>Amount</c> is a string so
/// trailing zeros survive JSON serialisation.</summary>
public static class ProductMoneyExtensions
{
    public static MoneyViewModel ToViewModel(this Money money) =>
        new(money.Amount.ToString("F2", CultureInfo.InvariantCulture), money.CurrencyCode);
}
