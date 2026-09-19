using TrackTraceMoney.Domain.Accounts;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.Domain.Tests.Accounts;

public sealed class BankAccountTests
{
    [Fact]
    public void Constructor_WithoutInstitutionId_InstitutionIdIsNull()
    {
        var account = new BankAccount("Checking", CurrencyCode.USD, openingBalance: 0m);

        Assert.Null(account.InstitutionId);
    }

    [Fact]
    public void Constructor_WithInstitutionId_SetsInstitutionId()
    {
        var institutionId = Guid.NewGuid();

        var account = new BankAccount("Checking", CurrencyCode.USD, openingBalance: 0m, institutionId: institutionId);

        Assert.Equal(institutionId, account.InstitutionId);
    }

    [Fact]
    public void Constructor_WithEmptyInstitutionId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new BankAccount("Checking", CurrencyCode.USD, openingBalance: 0m, institutionId: Guid.Empty));
    }

    [Fact]
    public void SetInstitutionId_UpdatesInstitutionId()
    {
        var account = new BankAccount("Checking", CurrencyCode.USD, openingBalance: 0m);
        var newInstitutionId = Guid.NewGuid();

        account.SetInstitutionId(newInstitutionId);

        Assert.Equal(newInstitutionId, account.InstitutionId);
    }

    [Fact]
    public void SetInstitutionId_WithEmptyGuid_Throws()
    {
        var account = new BankAccount("Checking", CurrencyCode.USD, openingBalance: 0m);

        Assert.Throws<ArgumentException>(() => account.SetInstitutionId(Guid.Empty));
    }

    [Fact]
    public void UpdateInstitution_WithId_SetsInstitutionId()
    {
        var account = new BankAccount("Checking", CurrencyCode.USD, openingBalance: 0m);
        var institutionId = Guid.NewGuid();

        account.UpdateInstitution(institutionId);

        Assert.Equal(institutionId, account.InstitutionId);
    }

    [Fact]
    public void UpdateInstitution_WithNull_ClearsInstitutionId()
    {
        var institutionId = Guid.NewGuid();
        var account = new BankAccount("Checking", CurrencyCode.USD, openingBalance: 0m, institutionId: institutionId);

        account.UpdateInstitution(null);

        Assert.Null(account.InstitutionId);
    }

    [Fact]
    public void UpdateInstitution_WithEmptyGuid_Throws()
    {
        var account = new BankAccount("Checking", CurrencyCode.USD, openingBalance: 0m);

        Assert.Throws<ArgumentException>(() => account.UpdateInstitution(Guid.Empty));
    }

    [Fact]
    public void UpdateAccountNumber_WithValue_SetsAccountNumberLast4()
    {
        var account = new BankAccount("Checking", CurrencyCode.USD, openingBalance: 0m);

        account.UpdateAccountNumber("4321");

        Assert.Equal("4321", account.AccountNumberLast4);
    }

    [Fact]
    public void UpdateAccountNumber_WithNull_ClearsAccountNumberLast4()
    {
        var account = new BankAccount("Checking", CurrencyCode.USD, openingBalance: 0m, accountNumberLast4: "1234");

        account.UpdateAccountNumber(null);

        Assert.Null(account.AccountNumberLast4);
    }
}
