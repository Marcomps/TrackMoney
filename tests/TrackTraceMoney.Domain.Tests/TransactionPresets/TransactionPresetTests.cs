using TrackTraceMoney.Domain.TransactionPresets;

namespace TrackTraceMoney.Domain.Tests.TransactionPresets;

/// <summary>
/// Covers <see cref="TransactionPreset"/>'s construction validation and mutators (Transaction Type
/// Customization slice spec §B.6/§B.7.5): the "at most one of DefaultAccountId/DefaultCreditAccountId"
/// guard, the "DefaultCreditAccountId only for Expense" guard, the "no category concept for Transfer"
/// guard, and Rename/SetIcon/UpdateDefaults/Deactivate/Reactivate.
/// </summary>
public sealed class TransactionPresetTests
{
    private static readonly Guid CategoryId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid CreditAccountId = Guid.NewGuid();

    [Fact]
    public void Constructor_BothAccountAndCreditAccountSet_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new TransactionPreset("Gasolina", null, TransactionPresetBaseType.Expense, null, AccountId, CreditAccountId));
    }

    [Fact]
    public void Constructor_CreditAccountSetWithIncomeBaseType_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new TransactionPreset("Salario", null, TransactionPresetBaseType.Income, null, null, CreditAccountId));
    }

    [Fact]
    public void Constructor_CreditAccountSetWithTransferBaseType_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new TransactionPreset("Ahorro", null, TransactionPresetBaseType.Transfer, null, null, CreditAccountId));
    }

    [Fact]
    public void Constructor_CreditAccountSetWithExpenseBaseType_Succeeds()
    {
        var preset = new TransactionPreset("Gasolina", null, TransactionPresetBaseType.Expense, CategoryId, null, CreditAccountId);

        Assert.Equal(CreditAccountId, preset.DefaultCreditAccountId);
        Assert.Null(preset.DefaultAccountId);
    }

    [Fact]
    public void Constructor_AccountSetWithExpenseBaseType_Succeeds()
    {
        var preset = new TransactionPreset("Gasolina", null, TransactionPresetBaseType.Expense, CategoryId, AccountId, null);

        Assert.Equal(AccountId, preset.DefaultAccountId);
        Assert.Null(preset.DefaultCreditAccountId);
    }

    [Fact]
    public void Constructor_NeitherAccountNorCreditAccountSet_Succeeds()
    {
        var preset = new TransactionPreset("Gasolina", null, TransactionPresetBaseType.Expense, CategoryId, null, null);

        Assert.Null(preset.DefaultAccountId);
        Assert.Null(preset.DefaultCreditAccountId);
    }

    [Fact]
    public void Constructor_CategorySetWithTransferBaseType_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new TransactionPreset("Ahorro", null, TransactionPresetBaseType.Transfer, CategoryId, null, null));
    }

    [Fact]
    public void Constructor_NoCategoryWithTransferBaseType_Succeeds()
    {
        var preset = new TransactionPreset("Ahorro", null, TransactionPresetBaseType.Transfer, null, AccountId, null);

        Assert.Null(preset.DefaultCategoryId);
    }

    [Fact]
    public void Constructor_EmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new TransactionPreset(" ", null, TransactionPresetBaseType.Expense, null, null, null));
    }

    [Fact]
    public void Constructor_DefaultsToActive()
    {
        var preset = new TransactionPreset("Gasolina", null, TransactionPresetBaseType.Expense, null, null, null);

        Assert.True(preset.IsActive);
    }

    [Fact]
    public void Rename_UpdatesName()
    {
        var preset = new TransactionPreset("Gasolina", null, TransactionPresetBaseType.Expense, null, null, null);

        preset.Rename("Combustible");

        Assert.Equal("Combustible", preset.Name);
    }

    [Fact]
    public void Rename_TrimsWhitespace()
    {
        var preset = new TransactionPreset("Gasolina", null, TransactionPresetBaseType.Expense, null, null, null);

        preset.Rename("  Combustible  ");

        Assert.Equal("Combustible", preset.Name);
    }

    [Fact]
    public void Rename_EmptyName_Throws()
    {
        var preset = new TransactionPreset("Gasolina", null, TransactionPresetBaseType.Expense, null, null, null);

        Assert.Throws<ArgumentException>(() => preset.Rename(" "));
    }

    [Fact]
    public void SetIcon_UpdatesIcon()
    {
        var preset = new TransactionPreset("Gasolina", null, TransactionPresetBaseType.Expense, null, null, null);

        preset.SetIcon("⛽");

        Assert.Equal("⛽", preset.Icon);
    }

    [Fact]
    public void SetIcon_Null_ClearsIcon()
    {
        var preset = new TransactionPreset("Gasolina", "⛽", TransactionPresetBaseType.Expense, null, null, null);

        preset.SetIcon(null);

        Assert.Null(preset.Icon);
    }

    [Fact]
    public void UpdateDefaults_UpdatesAllThreeFields()
    {
        var preset = new TransactionPreset("Gasolina", null, TransactionPresetBaseType.Expense, null, null, null);
        var newCategoryId = Guid.NewGuid();

        preset.UpdateDefaults(newCategoryId, AccountId, null);

        Assert.Equal(newCategoryId, preset.DefaultCategoryId);
        Assert.Equal(AccountId, preset.DefaultAccountId);
        Assert.Null(preset.DefaultCreditAccountId);
    }

    [Fact]
    public void UpdateDefaults_SwitchingFromAccountToCreditAccount_Succeeds()
    {
        var preset = new TransactionPreset("Gasolina", null, TransactionPresetBaseType.Expense, CategoryId, AccountId, null);

        preset.UpdateDefaults(CategoryId, null, CreditAccountId);

        Assert.Null(preset.DefaultAccountId);
        Assert.Equal(CreditAccountId, preset.DefaultCreditAccountId);
    }

    [Fact]
    public void UpdateDefaults_BothAccountAndCreditAccountSet_Throws()
    {
        var preset = new TransactionPreset("Gasolina", null, TransactionPresetBaseType.Expense, null, null, null);

        Assert.Throws<ArgumentException>(() => preset.UpdateDefaults(null, AccountId, CreditAccountId));
    }

    /// <summary>
    /// Unreachable through the planned UI (§B.7.4's picker only ever shows the merged Expense-only
    /// account+card list when BaseType == Expense) -- mirrors RecurringExpense's own private-constructor
    /// guard precedent of defending a structurally-unreachable-through-the-UI case anyway.
    /// </summary>
    [Fact]
    public void UpdateDefaults_CreditAccountSetOnIncomePreset_Throws()
    {
        var preset = new TransactionPreset("Salario", null, TransactionPresetBaseType.Income, null, null, null);

        Assert.Throws<ArgumentException>(() => preset.UpdateDefaults(null, null, CreditAccountId));
    }

    [Fact]
    public void UpdateDefaults_CategorySetOnTransferPreset_Throws()
    {
        var preset = new TransactionPreset("Ahorro", null, TransactionPresetBaseType.Transfer, null, null, null);

        Assert.Throws<ArgumentException>(() => preset.UpdateDefaults(CategoryId, null, null));
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var preset = new TransactionPreset("Gasolina", null, TransactionPresetBaseType.Expense, null, null, null);

        preset.Deactivate();

        Assert.False(preset.IsActive);
    }

    [Fact]
    public void Reactivate_SetsIsActiveTrue()
    {
        var preset = new TransactionPreset("Gasolina", null, TransactionPresetBaseType.Expense, null, null, null);
        preset.Deactivate();

        preset.Reactivate();

        Assert.True(preset.IsActive);
    }
}
