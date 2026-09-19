using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrackTraceMoney.Domain.Accounts;
using TrackTraceMoney.Domain.CreditAccounts;
using TrackTraceMoney.Domain.Enums;
using TrackTraceMoney.Infrastructure.Persistence;
using TrackTraceMoney.Infrastructure.Seeding;

namespace TrackTraceMoney.Infrastructure.Tests.Seeding;

/// <summary>
/// Real-SQLite coverage for <see cref="FinancialInstitutionBackfillService"/> — this project's first
/// genuine data migration (see the financial-institution-card-network-slice-spec's Decision 2), so it
/// gets its own dedicated test category rather than being folded into an existing one.
///
/// "Pre-migration-shaped" legacy rows (old free-text column populated, new FK column null) are simulated
/// via a raw SQL <c>UPDATE</c> run immediately after normal entity construction/save, because
/// CreditCard/Loan/TermDeposit/InvestmentFund's own public constructors can no longer produce that shape
/// (<c>institutionId</c> is a required, non-nullable constructor parameter now — see each entity's own
/// remarks). This mirrors exactly what a real legacy row looks like: it was written before this slice's
/// migration added the <c>InstitutionId</c> column, so that column is simply <see langword="null"/> for
/// it — the same state EF's private parameterless constructor materializes it into when the row is read
/// back, which is the scenario this backfill service exists to handle.
/// </summary>
public sealed class FinancialInstitutionBackfillServiceTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"tracktracemoney-tests-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _provider;

    public FinancialInstitutionBackfillServiceTests()
    {
        var services = new ServiceCollection();
        services.AddTrackTraceMoneyInfrastructure(_ => $"Data Source={_dbPath}");
        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrackTraceMoneyDbContext>();
        context.Database.EnsureCreated();
    }

    private async Task<Guid> SeedLegacyCreditCardAsync(string issuerName)
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrackTraceMoneyDbContext>();

        var card = new CreditCard("Test Card", CurrencyCode.USD, Guid.NewGuid(), creditLimit: 1000m, statementCutOffDay: 1, paymentDueDay: 15);
        context.Set<CreditCard>().Add(card);
        await context.SaveChangesAsync();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE CreditAccounts SET Issuer = {issuerName}, InstitutionId = NULL WHERE Id = {card.Id}");

        return card.Id;
    }

    private async Task<Guid> SeedLegacyLoanAsync(string institutionName)
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrackTraceMoneyDbContext>();

        var loan = new Loan(
            "Test Loan", CurrencyCode.USD, Guid.NewGuid(), LoanKind.PersonalLoan,
            originalAmount: 1000m, currentBalance: 500m, interestRate: 0.1m, LoanRateType.Fixed,
            monthlyInstallment: 100m, nextPaymentDate: new DateOnly(2026, 10, 1), requiredPayment: 100m);
        context.Set<Loan>().Add(loan);
        await context.SaveChangesAsync();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE CreditAccounts SET Institution = {institutionName}, InstitutionId = NULL WHERE Id = {loan.Id}");

        return loan.Id;
    }

    private async Task<Guid> SeedLegacyTermDepositAsync(string institutionName)
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrackTraceMoneyDbContext>();

        var termDeposit = new TermDeposit(
            "Test CD", CurrencyCode.USD, Guid.NewGuid(), initialPrincipal: 1000m, openingBalance: 1000m,
            rate: 0.05m, TermDepositRateType.Nominal, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1),
            TermDepositInterestFrequency.Monthly, isCompounding: true, autoRenewal: false);
        context.Set<TermDeposit>().Add(termDeposit);
        await context.SaveChangesAsync();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Accounts SET Institution = {institutionName}, InstitutionId = NULL WHERE Id = {termDeposit.Id}");

        return termDeposit.Id;
    }

    private async Task<Guid> SeedLegacyInvestmentFundAsync(string institutionName)
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrackTraceMoneyDbContext>();

        var fund = new InvestmentFund(
            "Test Fund", CurrencyCode.USD, Guid.NewGuid(), new DateOnly(2026, 1, 1),
            contributions: 1000m, openingBalance: 1000m);
        context.Set<InvestmentFund>().Add(fund);
        await context.SaveChangesAsync();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Accounts SET Institution = {institutionName}, InstitutionId = NULL WHERE Id = {fund.Id}");

        return fund.Id;
    }

    private async Task<Guid> SeedLegacyBankAccountAsync(string bankName)
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrackTraceMoneyDbContext>();

        var account = new BankAccount("Test Checking", CurrencyCode.USD, openingBalance: 0m);
        context.Set<BankAccount>().Add(account);
        await context.SaveChangesAsync();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Accounts SET BankName = {bankName}, InstitutionId = NULL WHERE Id = {account.Id}");

        return account.Id;
    }

    /// <summary>
    /// BankAccount was added to the backfill pool in a follow-up pass (it was missed in the original
    /// slice) -- this test specifically proves it pools/dedups against another entity type's legacy
    /// value, the same way the original four types already did against each other (see the test above).
    /// </summary>
    [Fact]
    public async Task BackfillInstitutionsAsync_PoolsBankAccountWithOtherEntityTypesSharingTheSameName()
    {
        var creditCardId = await SeedLegacyCreditCardAsync("Banco Agrícola");
        var bankAccountId = await SeedLegacyBankAccountAsync("Banco Agrícola");
        var otherBankAccountId = await SeedLegacyBankAccountAsync("Banco Industrial");

        using (var scope = _provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TrackTraceMoneyDbContext>();
            await FinancialInstitutionBackfillService.BackfillInstitutionsAsync(context);
        }

        using (var scope = _provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TrackTraceMoneyDbContext>();

            var institutions = await context.FinancialInstitutions.ToListAsync();
            // 2 distinct names: "Banco Agrícola" (shared by the card and one bank account) and
            // "Banco Industrial" (the other bank account) -- not 3, proving the shared name collapsed
            // onto one row across the two different entity types.
            Assert.Equal(2, institutions.Count);

            var bancoAgricola = Assert.Single(institutions, i => i.Name == "Banco Agrícola");
            var bancoIndustrial = Assert.Single(institutions, i => i.Name == "Banco Industrial");

            var card = await context.Set<CreditCard>().SingleAsync(c => c.Id == creditCardId);
            var bankAccount = await context.Set<BankAccount>().SingleAsync(b => b.Id == bankAccountId);
            var otherBankAccount = await context.Set<BankAccount>().SingleAsync(b => b.Id == otherBankAccountId);

            Assert.Equal(bancoAgricola.Id, card.InstitutionId);
            Assert.Equal(bancoAgricola.Id, bankAccount.InstitutionId);
            Assert.Equal(bancoIndustrial.Id, otherBankAccount.InstitutionId);
        }
    }

    [Fact]
    public async Task BackfillInstitutionsAsync_CreatesOneInstitutionPerDistinctExactNameAcrossAllFourEntityTypes()
    {
        // CreditCard and Loan share the exact same string -- must collapse onto ONE shared
        // FinancialInstitution (the spec's "pooled across all four entity types" requirement, not four
        // separate per-type pools).
        var creditCardId = await SeedLegacyCreditCardAsync("Banco Agrícola");
        var loanId = await SeedLegacyLoanAsync("Banco Agrícola");
        var termDepositId = await SeedLegacyTermDepositAsync("Banco Industrial");
        // Near-duplicate of "Banco Agrícola" (differs only by case, plus trailing whitespace that Trim()
        // removes) -- must NOT be fuzzy-merged with it. This is the test that actually proves the
        // "exact match only" decision was implemented, not just intended.
        var investmentFundId = await SeedLegacyInvestmentFundAsync("banco agrícola ");

        using (var scope = _provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TrackTraceMoneyDbContext>();
            await FinancialInstitutionBackfillService.BackfillInstitutionsAsync(context);
        }

        using (var scope = _provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TrackTraceMoneyDbContext>();

            var institutions = await context.FinancialInstitutions.ToListAsync();
            // 3 distinct exact strings: "Banco Agrícola" (shared by card+loan), "Banco Industrial", and
            // "banco agrícola" (trimmed) -- kept separate from "Banco Agrícola" despite differing only by case.
            Assert.Equal(3, institutions.Count);

            var bancoAgricola = Assert.Single(institutions, i => i.Name == "Banco Agrícola");
            var bancoIndustrial = Assert.Single(institutions, i => i.Name == "Banco Industrial");
            var bancoAgricolaLowercase = Assert.Single(institutions, i => i.Name == "banco agrícola");

            var card = await context.Set<CreditCard>().SingleAsync(c => c.Id == creditCardId);
            var loan = await context.Set<Loan>().SingleAsync(l => l.Id == loanId);
            var termDeposit = await context.Set<TermDeposit>().SingleAsync(t => t.Id == termDepositId);
            var investmentFund = await context.Set<InvestmentFund>().SingleAsync(f => f.Id == investmentFundId);

            Assert.Equal(bancoAgricola.Id, card.InstitutionId);
            Assert.Equal(bancoAgricola.Id, loan.InstitutionId);
            Assert.Equal(bancoIndustrial.Id, termDeposit.InstitutionId);
            Assert.Equal(bancoAgricolaLowercase.Id, investmentFund.InstitutionId);
        }
    }

    [Fact]
    public async Task BackfillInstitutionsAsync_SecondRun_IsANoOp()
    {
        var creditCardId = await SeedLegacyCreditCardAsync("Banco Agrícola");

        using (var scope = _provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TrackTraceMoneyDbContext>();
            await FinancialInstitutionBackfillService.BackfillInstitutionsAsync(context);
        }

        using (var scope = _provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TrackTraceMoneyDbContext>();
            await FinancialInstitutionBackfillService.BackfillInstitutionsAsync(context);
        }

        using (var scope = _provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TrackTraceMoneyDbContext>();

            var institutions = await context.FinancialInstitutions.ToListAsync();
            Assert.Single(institutions); // Still exactly one -- the second run created nothing new.

            var card = await context.Set<CreditCard>().SingleAsync(c => c.Id == creditCardId);
            Assert.Equal(institutions[0].Id, card.InstitutionId); // Still correctly wired, not cleared.
        }
    }

    [Fact]
    public async Task BackfillInstitutionsAsync_WithNoLegacyRows_IsANoOp()
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrackTraceMoneyDbContext>();

        var exception = await Record.ExceptionAsync(() => FinancialInstitutionBackfillService.BackfillInstitutionsAsync(context));

        Assert.Null(exception);
        Assert.Empty(await context.FinancialInstitutions.ToListAsync());
    }

    public void Dispose()
    {
        _provider.Dispose();
        SqliteConnection.ClearAllPools();

        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }
}
