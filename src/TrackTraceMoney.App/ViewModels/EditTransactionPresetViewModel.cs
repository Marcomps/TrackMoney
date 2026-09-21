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
/// Rename/icon/defaults editing for a <see cref="TransactionPreset"/> (Transaction Type Customization
/// slice spec §B.4/§B.7.4), mirroring <see cref="EditCategoryViewModel"/>'s "own dedicated page, not a
/// shared Add/Edit page" precedent. <see cref="TransactionPreset.BaseType"/> itself is never editable
/// here (the entity exposes no mutator for it -- only <c>Rename</c>/<c>SetIcon</c>/<c>UpdateDefaults</c>),
/// shown as a read-only label instead, same "show why it's disabled, don't just silently disable it"
/// idiom as <c>EditCategoryViewModel.IsSystemNameLockedMessageVisible</c>.
/// </summary>
[QueryProperty(nameof(PresetIdText), "presetId")]
public sealed partial class EditTransactionPresetViewModel : ObservableObject
{
    private readonly ITransactionPresetRepository _transactionPresetRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IFinancialAccountRepository _accountRepository;
    private readonly ICreditAccountRepository _creditAccountRepository;

    [ObservableProperty]
    private Guid presetId;

    /// <summary>The actual <c>[QueryProperty]</c> target -- see <c>EditCategoryViewModel.CategoryIdText</c>'s
    /// doc comment for why this is bound as a string and parsed defensively rather than as a non-nullable
    /// <see cref="Guid"/> directly.</summary>
    [ObservableProperty]
    private string? presetIdText;

    partial void OnPresetIdTextChanged(string? value)
    {
        if (Guid.TryParse(value, out var parsed))
            PresetId = parsed;
    }

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string selectedIcon = CategoryIconPalette.NoIconValue;

    [ObservableProperty]
    private TransactionPresetBaseType baseType;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsExpenseBaseType))]
    [NotifyPropertyChangedFor(nameof(IsCategoryVisible))]
    [NotifyPropertyChangedFor(nameof(IsPlainAccountBaseType))]
    private bool loaded;

    [ObservableProperty]
    private NamedOption? selectedCategory;

    [ObservableProperty]
    private NamedOption? selectedAccount;

    [ObservableProperty]
    private NamedOption? selectedPlainAccount;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private bool isBusy;

    public IReadOnlyList<string> AvailableIcons { get; } = CategoryIconPalette.Icons;

    public bool IsExpenseBaseType => BaseType == TransactionPresetBaseType.Expense;

    public bool IsPlainAccountBaseType => !IsExpenseBaseType;

    public bool IsCategoryVisible => BaseType != TransactionPresetBaseType.Transfer;

    public ObservableCollection<NamedOption> Categories { get; } = [];

    public ObservableCollection<NamedOption> ExpenseAccounts { get; } = [];

    public ObservableCollection<NamedOption> PlainAccounts { get; } = [];

    public EditTransactionPresetViewModel(
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

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (PresetId == Guid.Empty)
            return;

        var categories = await _categoryRepository.GetAllAsync();
        var accounts = await _accountRepository.GetActiveAsync();
        var creditAccounts = await _creditAccountRepository.GetActiveAsync();

        Categories.Clear();
        foreach (var category in categories)
            Categories.Add(new NamedOption(category.Id, SystemCategoryKeyToLabelConverter.GetDisplayName(category)));

        ExpenseAccounts.Clear();
        foreach (var account in accounts.Where(a => a is not TermDeposit and not InvestmentFund))
            ExpenseAccounts.Add(new NamedOption(account.Id, account.Name, account.Currency, IsCreditAccount: false));
        foreach (var creditCard in creditAccounts.OfType<CreditCard>())
            ExpenseAccounts.Add(new NamedOption(creditCard.Id, $"💳 {creditCard.Name}", creditCard.Currency, IsCreditAccount: true));

        PlainAccounts.Clear();
        foreach (var account in accounts.Where(a => a is not InvestmentFund))
            PlainAccounts.Add(new NamedOption(account.Id, account.Name, account.Currency));

        var preset = await _transactionPresetRepository.GetByIdAsync(PresetId);
        if (preset is null)
        {
            ErrorMessage = AppResources.EditTransactionPreset_NotFound;
            return;
        }

        Name = preset.Name;
        SelectedIcon = preset.Icon ?? CategoryIconPalette.NoIconValue;
        BaseType = preset.BaseType;

        SelectedCategory = preset.DefaultCategoryId is { } categoryId
            ? Categories.FirstOrDefault(o => o.Id == categoryId)
            : null;

        if (IsExpenseBaseType)
        {
            var targetId = preset.DefaultAccountId ?? preset.DefaultCreditAccountId;
            SelectedAccount = targetId is null ? null : ExpenseAccounts.FirstOrDefault(o => o.Id == targetId.Value);
        }
        else
        {
            SelectedPlainAccount = preset.DefaultAccountId is { } accountId
                ? PlainAccounts.FirstOrDefault(o => o.Id == accountId)
                : null;
        }

        Loaded = true;
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
            var preset = await _transactionPresetRepository.GetByIdAsync(PresetId);
            if (preset is null)
            {
                ErrorMessage = AppResources.EditTransactionPreset_NotFound;
                return;
            }

            preset.Rename(Name);
            preset.SetIcon(AsNullableIcon(SelectedIcon));
            preset.UpdateDefaults(categoryId, accountId, creditAccountId);

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
