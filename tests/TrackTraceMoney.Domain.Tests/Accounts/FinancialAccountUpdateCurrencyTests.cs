using TrackTraceMoney.Domain.Accounts;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.Domain.Tests.Accounts;

/// <summary>
/// Covers <see cref="FinancialAccount.UpdateCurrency"/> (edit/delete slice spec §1.1) — the mutator
/// itself performs no reference-check (that's the Application-layer lifecycle service's job via
/// <c>CanChangeCurrencyAsync</c>), so this only proves the mutator's own mechanical behavior: it sets
/// <see cref="FinancialAccount.Currency"/> and never touches <see cref="FinancialAccount.Balance"/>.
/// Exercised via <see cref="CashAccount"/> since <see cref="FinancialAccount"/> is abstract.
/// </summary>
public sealed class FinancialAccountUpdateCurrencyTests
{
    [Fact]
    public void UpdateCurrency_ChangesCurrency()
    {
        var account = new CashAccount("Wallet", CurrencyCode.USD, openingBalance: 100m);

        account.UpdateCurrency(CurrencyCode.MXN);

        Assert.Equal(CurrencyCode.MXN, account.Currency);
    }

    [Fact]
    public void UpdateCurrency_DoesNotMutateBalance()
    {
        var account = new CashAccount("Wallet", CurrencyCode.USD, openingBalance: 100m);

        account.UpdateCurrency(CurrencyCode.MXN);

        Assert.Equal(100m, account.Balance);
    }
}
