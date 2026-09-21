using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Converters;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.Accounts;
using TrackTraceMoney.Domain.CreditAccounts;
using TrackTraceMoney.Domain.TransactionPresets;

namespace TrackTraceMoney.App.ViewModels;

/// <summary>
/// Add screen for a <see cref="TransactionPreset"/> (Transaction Type Customization slice spec §B.4/§B.7.4).
/// The account-target picker mirrors <c>AddRecurringExpenseViewModel</c>'s already-shipped "one merged
/// Picker, not a toggle" pattern for <see cref="TransactionPresetBaseType.Expense"/>-based presets (both
/// <c>FinancialAccount</c>s and credit cards together via <see cref="ExpenseAccounts"/>); Income/Transfer
/// presets get a plain <c>FinancialAccount</c>-only picker (<see cref="PlainAccounts"/>, no cards) since
/// neither can ever legally target a credit card (Domain-enforced invariant, not just a UI convention).
/// </summary>
public sealed partial class AddTransactionPresetViewModel : ObservableObject
{
    private readonly ITransactionPresetRepository _transactionPresetRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IFinancialAccountRepository _accountRepository;
    private readonly ICreditAccountRepository _creditAccountRepository;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string selectedIcon = CategoryIconPalette.NoIconValue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsExpenseBaseType))]
    [NotifyPropertyChangedFor(nameof(IsCategoryVisible))]
    [NotifyPropertyChangedFor(nameof(IsPlainAccountBaseType))]
    private TransactionPresetBaseType selectedBaseType = TransactionPresetBaseType.Expense;

    [ObservableProperty]
    private NamedOption? selectedCategory;

    /// <summary>Only consulted when <see cref="SelectedBaseType"/> is
    /// <see cref="TransactionPresetBaseType.Expense"/> -- resolved against <see cref="ExpenseAccounts"/>.</summary>
    [ObservableProperty]
    private NamedOption? selectedAccount;

    /// <summary>Only consulted when <see cref="SelectedBaseType"/> is Income/Transfer -- resolved against
    /// <see cref="PlainAccounts"/>.</summary>
    [ObservableProperty]
    private NamedOption? selectedPlainAccount;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private bool isBusy;

    public IReadOnlyList<TransactionPresetBaseType> AvailableBaseTypes { get; } = Enum.GetValues<TransactionPresetBaseType>();

    public IReadOnlyList<string> AvailableIcons { get; } = CategoryIconPalette.Icons;

    public bool IsExpenseBaseType => SelectedBaseType == TransactionPresetBaseType.Expense;

    public bool IsPlainAccountBaseType => !IsExpenseBaseType;

    /// <summary>Transfer has no category concept anywhere in this app (Domain-enforced by
    /// <see cref="TransactionPreset"/>'s own constructor guard) -- hidden, not just left to fail on Save.</summary>
    public bool IsCategoryVisible => SelectedBaseType != TransactionPresetBaseType.Transfer;

    public ObservableCollection<NamedOption> Categories { get; } = [];

    /// <summary>Merged FinancialAccount + credit card picker, same shape as
    /// <c>AddTransactionViewModel.PaymentAccounts</c>/<c>AddRecurringExpenseViewModel.Accounts</c>.</summary>
    public ObservableCollection<NamedOption> ExpenseAccounts { get; } = [];

    /// <summary>FinancialAccount-only picker (no cards), same shape as <c>AddTransactionViewModel.Accounts</c>.</summary>
    public ObservableCollection<NamedOption> PlainAccounts { get; } = [];

    public AddTransactionPresetViewModel(
        ITransactionPresetRepository transactionPresetRepository,
        ICategoryRepository categoryRepository,
        IFinancialAccountRepository accountRepository,
        ICreditAccountRepository creditAccountRepository)
    {
        _transactionPresetRepository = transactionPresetRepository;
        _categoryRepository = categoryRepository;
        _accountRepository = accountRepository;
        _creditAccountRepository = creditAccountRepository;
    }

    /// <summary>Clears every default-field selection whenever the base type changes -- a stale selection
    /// from one base type (e.g. a category picked while Expense was selected) has no guaranteed meaning
    /// once the type switches (mirrors <c>AddTransactionViewModel.OnSelectedTypeChanged</c>'s identical
    /// stale-cross-picker-selection concern).</summary>
    partial void OnSelectedBaseTypeChanged(TransactionPresetBaseType value)
    {
        SelectedCategory = null;
        SelectedAccount = null;
        SelectedPlainAccount = null;
    }

    [RelayCommand]
    private async Task LoadOptionsAsync()
    {
        var categories = await _categoryRepository.GetAllAsync();
        var accounts = await _accountRepository.GetActiveAsync();
        var creditAccounts = await _creditAccountRepository.GetActiveAsync();

        Categories.Clear();
        foreach (var category in categories)
            Categories.Add(new NamedOption(category.Id, SystemCategoryKeyToLabelConverter.GetDisplayName(category)));

        // Same TermDeposit/InvestmentFund exclusions as AddTransactionViewModel.PaymentAccounts/
        // AddRecurringExpenseViewModel.Accounts -- no legitimate "spend directly from a term deposit"
        // or "bypass InvestmentFund.RecordContribution/Withdrawal" use case.
        ExpenseAccounts.Clear();
        foreach (var account in accounts.Where(a => a is not TermDeposit and not InvestmentFund))
            ExpenseAccounts.Add(new NamedOption(account.Id, account.Name, account.Currency, IsCreditAccount: false));
        foreach (var creditCard in creditAccounts.OfType<CreditCard>())
            ExpenseAccounts.Add(new NamedOption(creditCard.Id, $"💳 {creditCard.Name}", creditCard.Currency, IsCreditAccount: true));

        PlainAccounts.Clear();
        foreach (var account in accounts.Where(a => a is not InvestmentFund))
            PlainAccounts.Add(new NamedOption(account.Id, account.Name, account.Currency));
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = AppResources.AddTransactionPreset_ValidationNameRequired;
            return;
        }

        Guid? categoryId = IsCategoryVisible ? SelectedCategory?.Id : null;
        Guid? accountId = null;
        Guid? creditAccountId = null;

        if (IsExpenseBaseType)
        {
            if (SelectedAccount is not null)
            {
                if (SelectedAccount.IsCreditAccount)
                    creditAccountId = SelectedAccount.Id;
                else
                    accountId = SelectedAccount.Id;
            }
        }
        else
        {
            accountId = SelectedPlainAccount?.Id;
        }

        IsBusy = true;
        try
        {
            var preset = new TransactionPreset(Name, AsNullableIcon(SelectedIcon), SelectedBaseType, categoryId, accountId, creditAccountId);

            await _transactionPresetRepository.AddAsync(preset);
            await _transactionPresetRepository.SaveChangesAsync();
            await Shell.Current.GoToAsync("..");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private static async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }

    private static string? AsNullableIcon(string icon) =>
        icon == CategoryIconPalette.NoIconValue ? null : icon;
}
