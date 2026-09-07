using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Converters;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Application.Transactions;

namespace TrackTraceMoney.App.ViewModels;

public sealed partial class AddTransactionViewModel : ObservableObject
{
    private readonly ITransactionEntryService _transactionEntryService;
    private readonly IFinancialAccountRepository _accountRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IPersonRepository _personRepository;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsExpense))]
    [NotifyPropertyChangedFor(nameof(IsIncome))]
    [NotifyPropertyChangedFor(nameof(IsTransfer))]
    private TransactionType selectedType = TransactionType.Expense;

    [ObservableProperty]
    private DateTime selectedDate = DateTime.Today;

    [ObservableProperty]
    private string amountText = string.Empty;

    [ObservableProperty]
    private NamedOption? selectedAccount;

    [ObservableProperty]
    private NamedOption? selectedDestinationAccount;

    [ObservableProperty]
    private NamedOption? selectedSourceAccount;

    [ObservableProperty]
    private NamedOption? selectedCategory;

    [ObservableProperty]
    private NamedOption? selectedPayer;

    [ObservableProperty]
    private NamedOption? selectedBeneficiary;

    [ObservableProperty]
    private NamedOption? selectedPerson;

    [ObservableProperty]
    private string? description;

    [ObservableProperty]
    private string? notes;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private bool isBusy;

    public IReadOnlyList<TransactionType> AvailableTypes { get; } = Enum.GetValues<TransactionType>();

    public ObservableCollection<NamedOption> Accounts { get; } = [];

    public ObservableCollection<NamedOption> Categories { get; } = [];

    public ObservableCollection<NamedOption> People { get; } = [];

    public bool IsExpense => SelectedType == TransactionType.Expense;

    public bool IsIncome => SelectedType == TransactionType.Income;

    public bool IsTransfer => SelectedType == TransactionType.Transfer;

    public AddTransactionViewModel(
        ITransactionEntryService transactionEntryService,
        IFinancialAccountRepository accountRepository,
        ICategoryRepository categoryRepository,
        IPersonRepository personRepository)
    {
        _transactionEntryService = transactionEntryService;
        _accountRepository = accountRepository;
        _categoryRepository = categoryRepository;
        _personRepository = personRepository;
    }

    [RelayCommand]
    private async Task LoadOptionsAsync()
    {
        var accounts = await _accountRepository.GetActiveAsync();
        var categories = await _categoryRepository.GetAllAsync();
        var people = await _personRepository.GetAllAsync();

        Accounts.Clear();
        foreach (var account in accounts)
            Accounts.Add(new NamedOption(account.Id, account.Name, account.Currency));

        Categories.Clear();
        foreach (var category in categories)
            Categories.Add(new NamedOption(category.Id, SystemCategoryKeyToLabelConverter.GetDisplayName(category)));

        People.Clear();
        People.Add(new NamedOption(Guid.Empty, AppResources.AddTransaction_NoneOption));
        foreach (var person in people)
            People.Add(new NamedOption(person.Id, person.Name));
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (!decimal.TryParse(AmountText, NumberStyles.Number, CultureInfo.CurrentCulture, out var amount) || amount <= 0)
        {
            ErrorMessage = AppResources.AddTransaction_ValidationAmountInvalid;
            return;
        }

        var date = DateOnly.FromDateTime(SelectedDate);

        IsBusy = true;
        try
        {
            switch (SelectedType)
            {
                case TransactionType.Expense:
                    if (SelectedAccount is null)
                    {
                        ErrorMessage = AppResources.AddTransaction_ValidationAccountRequired;
                        return;
                    }

                    if (SelectedCategory is null)
                    {
                        ErrorMessage = AppResources.AddTransaction_ValidationCategoryRequired;
                        return;
                    }

                    await _transactionEntryService.RecordExpenseAsync(
                        date,
                        amount,
                        SelectedAccount.Id,
                        SelectedCategory.Id,
                        AsNullableId(SelectedPayer),
                        AsNullableId(SelectedBeneficiary),
                        Description,
                        Notes);
                    break;

                case TransactionType.Income:
                    if (SelectedDestinationAccount is null)
                    {
                        ErrorMessage = AppResources.AddTransaction_ValidationAccountRequired;
                        return;
                    }

                    if (SelectedCategory is null)
                    {
                        ErrorMessage = AppResources.AddTransaction_ValidationCategoryRequired;
                        return;
                    }

                    await _transactionEntryService.RecordIncomeAsync(
                        date,
                        amount,
                        SelectedDestinationAccount.Id,
                        SelectedCategory.Id,
                        AsNullableId(SelectedPerson),
                        Description,
                        Notes);
                    break;

                case TransactionType.Transfer:
                    if (SelectedSourceAccount is null || SelectedDestinationAccount is null)
                    {
                        ErrorMessage = AppResources.AddTransaction_ValidationAccountRequired;
                        return;
                    }

                    if (SelectedSourceAccount.Id == SelectedDestinationAccount.Id)
                    {
                        ErrorMessage = AppResources.AddTransaction_ValidationSameAccount;
                        return;
                    }

                    if (SelectedSourceAccount.Currency != SelectedDestinationAccount.Currency)
                    {
                        ErrorMessage = AppResources.AddTransaction_ValidationCurrencyMismatch;
                        return;
                    }

                    await _transactionEntryService.RecordTransferAsync(
                        date,
                        amount,
                        SelectedSourceAccount.Id,
                        SelectedDestinationAccount.Id,
                        Description,
                        Notes);
                    break;
            }

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

    private static Guid? AsNullableId(NamedOption? option) =>
        option is null || option.Id == Guid.Empty ? null : option.Id;
}
