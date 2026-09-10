using TrackTraceMoney.Application.Reporting;
using TrackTraceMoney.Domain.Accounts;
using TrackTraceMoney.Domain.Enums;
using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.Application.Tests.Reporting;

/// <summary>
/// Regression coverage for the Phase 2 checkpoint review finding: the App layer's <c>DashboardViewModel</c>
/// (no test project exists for App-layer ViewModels in this codebase, so it can't be exercised directly
/// here) used to build its currency lookup from <c>IFinancialAccountRepository.GetActiveAsync()</c> only,
/// so a transaction posted earlier in the month against an account the user has since deactivated would
/// silently vanish from the dashboard's spend total. <see cref="SpendingCalculator"/>'s lookup-miss skip
/// is a deliberate defensive choice (see its own doc comment) — the actual bug was always in what the
/// caller handed it. These tests exercise the same two production building blocks the fix relies on
/// (<see cref="AccountCurrencyMapBuilder.Build"/> feeding <see cref="SpendingCalculator.Calculate"/>)
/// directly, to prove a deactivated account's currency still resolves when the map is built from ALL
/// accounts, and to document what happens (the original bug) when it's built from active accounts only.
/// </summary>
public sealed class SpendingCalculatorTests
{
    [Fact]
    public void Calculate_CurrencyMapBuiltFromAllAccounts_StillCountsExpenseAgainstADeactivatedAccount()
    {
        var account = new CashAccount("Old Wallet", CurrencyCode.USD, openingBalance: 0m);
        account.Deactivate();
        var categoryId = Guid.NewGuid();
        var expense = new Expense(DateOnly.FromDateTime(DateTime.Today), 40m, account.Id, categoryId);

        // Mirrors the fixed DashboardViewModel: the currency map is built from ALL accounts, not just
        // active ones, so a deactivated account's historical transactions still resolve correctly.
        var currencyMap = AccountCurrencyMapBuilder.Build([account], []);

        var summary = new SpendingCalculator().Calculate([expense], currencyMap);

        Assert.Equal(40m, summary.TotalSpentByCurrency[CurrencyCode.USD]);
        Assert.Equal(40m, summary.GetSpentForCategory(categoryId, CurrencyCode.USD));
    }

    [Fact]
    public void Calculate_CurrencyMapBuiltFromActiveAccountsOnly_SilentlyDropsExpenseAgainstADeactivatedAccount()
    {
        // Documents the exact bug the fix above guards against: if a caller ever goes back to building
        // the map from active accounts only, the same expense silently disappears from the total with
        // no error — SpendingCalculator's lookup-miss skip is deliberate (see its doc comment), which is
        // exactly why the CALLER, not the calculator, must be the one handing it a complete map.
        var account = new CashAccount("Old Wallet", CurrencyCode.USD, openingBalance: 0m);
        account.Deactivate();
        var categoryId = Guid.NewGuid();
        var expense = new Expense(DateOnly.FromDateTime(DateTime.Today), 40m, account.Id, categoryId);

        var activeAccountsOnly = new[] { account }.Where(a => a.IsActive);
        var currencyMap = AccountCurrencyMapBuilder.Build(activeAccountsOnly, []);

        var summary = new SpendingCalculator().Calculate([expense], currencyMap);

        Assert.Empty(summary.TotalSpentByCurrency);
        Assert.Equal(0m, summary.GetSpentForCategory(categoryId, CurrencyCode.USD));
    }
}
