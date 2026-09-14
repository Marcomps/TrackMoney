using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Converters;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Application.Transactions;
using TrackTraceMoney.Domain.Accounts;
using TrackTraceMoney.Domain.CreditAccounts;
using TrackTraceMoney.Domain.MedicalExpenses;
using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.App.ViewModels;

[QueryProperty(nameof(LinkedTransactionId), "linkedTransactionId")]
public sealed partial class AddTransactionViewModel : ObservableObject
{
    private readonly ITransactionEntryService _transactionEntryService;
    private readonly IFinancialAccountRepository _accountRepository;
    private readonly ICreditAccountRepository _creditAccountRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IPersonRepository _personRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IMedicalExpenseDetailRepository _medicalExpenseDetailRepository;

    /// <summary>
    /// Raw <c>Loan</c> entities backing <see cref="LoanOptions"/>, kept alongside it (not folded into
    /// <see cref="NamedOption"/>) since <c>OnSelectedLoanPaymentTargetChanged</c> needs
    /// <c>NextPaymentDate</c>/<c>MonthlyInstallment</c> to prefill the schedule fields, and those are
    /// loan-specific — extending the shared <see cref="NamedOption"/> record for one screen's prefill
    /// need would leak loan-only fields into every other picker consumer.
    /// </summary>
    private IReadOnlyList<Loan> _loans = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsExpense))]
    [NotifyPropertyChangedFor(nameof(IsIncome))]
    [NotifyPropertyChangedFor(nameof(IsTransfer))]
    [NotifyPropertyChangedFor(nameof(IsCreditCardPayment))]
    [NotifyPropertyChangedFor(nameof(IsLoanPayment))]
    [NotifyPropertyChangedFor(nameof(IsInvestmentContribution))]
    [NotifyPropertyChangedFor(nameof(IsInvestmentWithdrawal))]
    [NotifyPropertyChangedFor(nameof(IsReimbursement))]
    private TransactionType selectedType = TransactionType.Expense;

    [ObservableProperty]
    private DateTime selectedDate = DateTime.Today;

    [ObservableProperty]
    private string amountText = string.Empty;

    [ObservableProperty]
    private NamedOption? selectedAccount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIncomeCategoryAndPersonVisible))]
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
    private bool isMedicalExpense;

    [ObservableProperty]
    private string? medicalInsuranceProvider;

    [ObservableProperty]
    private bool hasInsuranceCoverage;

    [ObservableProperty]
    private string medicalInsuranceCoveredAmountText = string.Empty;

    [ObservableProperty]
    private bool insurancePaidProviderDirectly;

    [ObservableProperty]
    private NamedOption? selectedPerson;

    [ObservableProperty]
    private NamedOption? selectedCardPaymentSource;

    [ObservableProperty]
    private NamedOption? selectedCardPaymentTarget;

    [ObservableProperty]
    private NamedOption? selectedLoanPaymentSource;

    [ObservableProperty]
    private NamedOption? selectedLoanPaymentTarget;

    [ObservableProperty]
    private DateTime loanNextPaymentDate = DateTime.Today;

    [ObservableProperty]
    private string requiredPaymentText = string.Empty;

    [ObservableProperty]
    private NamedOption? selectedInvestmentContributionSource;

    [ObservableProperty]
    private NamedOption? selectedInvestmentContributionFund;

    [ObservableProperty]
    private NamedOption? selectedInvestmentWithdrawalFund;

    [ObservableProperty]
    private NamedOption? selectedInvestmentWithdrawalDestination;

    [ObservableProperty]
    private PendingReimbursableExpenseOption? selectedPendingReimbursableExpense;

    [ObservableProperty]
    private NamedOption? selectedReimbursementDestinationAccount;

    /// <summary>
    /// Set via <c>AddTransactionPage</c>'s <c>[QueryProperty]</c> plumbing when navigating from
    /// <c>MedicalExpenseDetailPage</c>'s "Record reimbursement" button (README §27/§28, Phase 3 slice
    /// 7) — pre-selects <see cref="SelectedType"/> and <see cref="SelectedPendingReimbursableExpense"/>
    /// once <see cref="PendingReimbursableExpenses"/> has loaded. Bound as a plain string, not
    /// <c>Guid?</c>, mirroring <c>RecordCreditCardStatementViewModel.StatementId</c>'s documented reason:
    /// MAUI Shell query parameters always arrive as strings and this codebase has no nullable-Guid
    /// <c>[QueryProperty]</c> precedent to rely on — parsed defensively, absent/unparseable simply means
    /// "no pre-selection", the same degraded-but-safe behavior as navigating here directly.
    /// </summary>
    [ObservableProperty]
    private string? linkedTransactionId;

    [ObservableProperty]
    private string? description;

    [ObservableProperty]
    private string? notes;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private bool isBusy;

    // CreditCardPurchase is deliberately excluded: a card purchase is entered through the Expense
    // block below (README §11's "Payment method" field), not as its own top-level Type — this Picker
    // must keep mirroring README §45's quick-action menu, which has no separate "Card purchase" item.
    // InterestIncome is likewise excluded: it's auto-detected from the Income block's destination
    // account being a term deposit (see IsIncomeCategoryAndPersonVisible/SaveAsync), not a user-chosen
    // top-level Type — README §45 has no separate "Interest income" quick action either.
    public IReadOnlyList<TransactionType> AvailableTypes { get; } =
        Enum.GetValues<TransactionType>()
            .Where(t => t != TransactionType.CreditCardPurchase && t != TransactionType.InterestIncome)
            .ToList();

    public ObservableCollection<NamedOption> Accounts { get; } = [];

    /// <summary>
    /// Backs ONLY the Transfer block's destination picker — same base set as <see cref="Accounts"/>
    /// (both hierarchies already exclude <c>InvestmentFund</c>) but additionally excludes
    /// <c>TermDeposit</c>: unlike Transfer's source side (kept selectable via <see cref="Accounts"/>,
    /// needed for moving a matured deposit's proceeds out — no "close term deposit" flow exists yet),
    /// there's no legitimate "fund an open term deposit via transfer" use case (README §22), and doing
    /// so would bypass <c>TermDeposit.RecordInterestReceived</c> tracking, the same desync risk as the
    /// InvestmentFund bug this collection's sibling already guards against. Income's destination picker
    /// keeps using <see cref="Accounts"/> unchanged — a term deposit IS a valid Income destination,
    /// since that's how interest gets routed (see <c>SaveAsync</c>'s Income branch).
    /// </summary>
    public ObservableCollection<NamedOption> TransferDestinationAccounts { get; } = [];

    /// <summary>
    /// Backs the CreditCardPayment/LoanPayment/InvestmentContribution blocks' source-account pickers —
    /// same base set as <see cref="Accounts"/> (excludes <c>InvestmentFund</c>) but additionally excludes
    /// <c>TermDeposit</c>, mirroring <see cref="TransferDestinationAccounts"/>'s rationale: a term deposit
    /// is locked until maturity and there is no legitimate "pay a card/loan/fund a contribution directly
    /// from a term deposit" use case (README §22/§30). Unlike Transfer's source side (still <see cref="Accounts"/>,
    /// deliberately kept — see <see cref="TransferDestinationAccounts"/>'s doc comment), none of these
    /// three flows have the "move a matured deposit's proceeds out" justification, so both sides here are
    /// restricted. <c>TransactionEntryService</c> enforces the same restriction server-side regardless of
    /// what this picker offers (defense in depth).
    /// </summary>
    public ObservableCollection<NamedOption> NonTermDepositAccounts { get; } = [];

    /// <summary>
    /// Backs ONLY the Expense block's Account picker — includes both <c>FinancialAccount</c>s and
    /// credit cards (README §11's "Payment method" field), unlike <see cref="Accounts"/> which stays
    /// FinancialAccount-only and still backs Income's destination picker and Transfer's source/
    /// destination pickers. Cards must never be selectable there: <c>Transfer</c>/<c>Income</c> only
    /// work with <c>FinancialAccount.Credit</c>/<c>Debit</c>, and <c>CreditAccount</c> has no such
    /// methods (hard constraint). Also excludes <c>TermDeposit</c> on the <c>FinancialAccount</c> side —
    /// same rationale as <see cref="NonTermDepositAccounts"/>: there is no legitimate "spend directly
    /// from a term deposit" use case, whether the expense is medical or plain.
    /// </summary>
    public ObservableCollection<NamedOption> PaymentAccounts { get; } = [];

    /// <summary>
    /// Backs ONLY the CreditCardPayment block's Card picker — credit cards only, no <c>💳</c> prefix
    /// (the screen's own "Card" field label already conveys that), unlike
    /// <see cref="PaymentAccounts"/> which mixes both hierarchies with a prefix for the Expense block.
    /// </summary>
    public ObservableCollection<NamedOption> CreditCardOptions { get; } = [];

    /// <summary>
    /// Backs ONLY the LoanPayment block's Loan picker — loans only, no prefix (the screen's own "Loan"
    /// field label already conveys that), mirroring <see cref="CreditCardOptions"/>'s pattern.
    /// </summary>
    public ObservableCollection<NamedOption> LoanOptions { get; } = [];

    /// <summary>
    /// Backs ONLY the InvestmentContribution/InvestmentWithdrawal blocks' fund picker — investment
    /// funds only, mirroring <see cref="LoanOptions"/>'s pattern.
    /// </summary>
    public ObservableCollection<NamedOption> InvestmentFundOptions { get; } = [];

    /// <summary>
    /// Backs ONLY the Reimbursement block's expense picker — every transaction whose linked
    /// <see cref="MedicalExpenseDetail"/> is currently <see cref="MedicalReimbursementStatus.Pending"/>
    /// (README §27/§28, Phase 3 slice 7). Built from <c>IMedicalExpenseDetailRepository.GetPendingAsync</c>
    /// (a real <c>WHERE</c>-filtered query for just the Pending rows) plus a single batched
    /// <c>ITransactionRepository.GetByIdsAsync</c> lookup for the transactions they link to — not a
    /// full-table <c>GetAllAsync</c> fetch of either table followed by client-side filtering, and not
    /// one lookup per detail either.
    /// </summary>
    public ObservableCollection<PendingReimbursableExpenseOption> PendingReimbursableExpenses { get; } = [];

    public ObservableCollection<NamedOption> Categories { get; } = [];

    public ObservableCollection<NamedOption> People { get; } = [];

    public bool IsExpense => SelectedType == TransactionType.Expense;

    public bool IsIncome => SelectedType == TransactionType.Income;

    public bool IsTransfer => SelectedType == TransactionType.Transfer;

    public bool IsCreditCardPayment => SelectedType == TransactionType.CreditCardPayment;

    public bool IsLoanPayment => SelectedType == TransactionType.LoanPayment;

    public bool IsInvestmentContribution => SelectedType == TransactionType.InvestmentContribution;

    public bool IsInvestmentWithdrawal => SelectedType == TransactionType.InvestmentWithdrawal;

    public bool IsReimbursement => SelectedType == TransactionType.Reimbursement;

    /// <summary>
    /// Hides the Income block's Category/Person pickers when the selected destination account is a
    /// term deposit — an <see cref="Domain.Transactions.InterestIncome"/> transaction has no
    /// category/person, its meaning is unambiguous from its type alone (same precedent as
    /// Transfer/InvestmentContribution).
    /// </summary>
    public bool IsIncomeCategoryAndPersonVisible => SelectedDestinationAccount is not { IsTermDeposit: true };

    public AddTransactionViewModel(
        ITransactionEntryService transactionEntryService,
        IFinancialAccountRepository accountRepository,
        ICreditAccountRepository creditAccountRepository,
        ICategoryRepository categoryRepository,
        IPersonRepository personRepository,
        ITransactionRepository transactionRepository,
        IMedicalExpenseDetailRepository medicalExpenseDetailRepository)
    {
        _transactionEntryService = transactionEntryService;
        _accountRepository = accountRepository;
        _creditAccountRepository = creditAccountRepository;
        _categoryRepository = categoryRepository;
        _personRepository = personRepository;
        _transactionRepository = transactionRepository;
        _medicalExpenseDetailRepository = medicalExpenseDetailRepository;
    }

    [RelayCommand]
    private async Task LoadOptionsAsync()
    {
        var accounts = await _accountRepository.GetActiveAsync();
        var creditAccounts = await _creditAccountRepository.GetActiveAsync();
        var categories = await _categoryRepository.GetAllAsync();
        var people = await _personRepository.GetAllAsync();

        // InvestmentFund is deliberately excluded here — this collection backs Transfer's source/
        // destination pickers and Income's destination picker, and moving money into/out of a fund
        // through a plain Transfer/Income would bypass InvestmentFund.RecordContribution/
        // RecordWithdrawal, silently corrupting Contributions/Withdrawals (and therefore Gain/
        // ReturnPercentage) forever. Fund movements must go through the dedicated Investment
        // contribution/withdrawal blocks below instead.
        Accounts.Clear();
        foreach (var account in accounts.Where(a => a is not InvestmentFund))
            Accounts.Add(new NamedOption(account.Id, account.Name, account.Currency, IsTermDeposit: account is TermDeposit));

        // TermDeposit is additionally excluded here (on top of InvestmentFund) — see this collection's
        // doc comment for why Transfer's destination side differs from its source side and from
        // Income's destination side.
        TransferDestinationAccounts.Clear();
        foreach (var account in accounts.Where(a => a is not InvestmentFund and not TermDeposit))
            TransferDestinationAccounts.Add(new NamedOption(account.Id, account.Name, account.Currency));

        // Same TermDeposit exclusion as TransferDestinationAccounts, applied to the CreditCardPayment/
        // LoanPayment/InvestmentContribution blocks' source pickers — see this collection's doc comment.
        NonTermDepositAccounts.Clear();
        foreach (var account in accounts.Where(a => a is not InvestmentFund and not TermDeposit))
            NonTermDepositAccounts.Add(new NamedOption(account.Id, account.Name, account.Currency));

        InvestmentFundOptions.Clear();
        foreach (var fund in accounts.OfType<InvestmentFund>())
            InvestmentFundOptions.Add(new NamedOption(fund.Id, fund.Name, fund.Currency));

        // README §14's own example uses a "💳 " prefix for cards (e.g. "💳 BAC Card"); the Picker
        // renders via NamedOption.ToString(), so no ItemDisplayBinding is needed for this prefix.
        // TermDeposit is excluded from the FinancialAccount side — see PaymentAccounts's doc comment.
        PaymentAccounts.Clear();
        foreach (var account in accounts.Where(a => a is not TermDeposit))
            PaymentAccounts.Add(new NamedOption(account.Id, account.Name, account.Currency, IsCreditAccount: false));
        foreach (var creditCard in creditAccounts.OfType<CreditCard>())
            PaymentAccounts.Add(new NamedOption(creditCard.Id, $"💳 {creditCard.Name}", creditCard.Currency, IsCreditAccount: true));

        CreditCardOptions.Clear();
        foreach (var creditCard in creditAccounts.OfType<CreditCard>())
            CreditCardOptions.Add(new NamedOption(creditCard.Id, creditCard.Name, creditCard.Currency, IsCreditAccount: true));

        _loans = creditAccounts.OfType<Loan>().ToList();
        LoanOptions.Clear();
        foreach (var loan in _loans)
            LoanOptions.Add(new NamedOption(loan.Id, loan.Name, loan.Currency, IsCreditAccount: true));

        Categories.Clear();
        foreach (var category in categories)
            Categories.Add(new NamedOption(category.Id, SystemCategoryKeyToLabelConverter.GetDisplayName(category)));

        People.Clear();
        People.Add(new NamedOption(Guid.Empty, AppResources.AddTransaction_NoneOption));
        foreach (var person in people)
            People.Add(new NamedOption(person.Id, person.Name));

        // A filtered query for just the Pending details, plus a single batched lookup for the (small)
        // set of transactions they link to — instead of fetching every row in both the Transaction and
        // MedicalExpenseDetail tables just to find the handful that are Pending. See
        // PendingReimbursableExpenses's doc comment.
        var categoryNames = categories.ToDictionary(c => c.Id, SystemCategoryKeyToLabelConverter.GetDisplayName);
        var pendingDetails = await _medicalExpenseDetailRepository.GetPendingAsync();
        var linkedTransactions = await _transactionRepository.GetByIdsAsync(pendingDetails.Select(d => d.TransactionId));
        var transactionsById = linkedTransactions.ToDictionary(t => t.Id);

        PendingReimbursableExpenses.Clear();
        foreach (var detail in pendingDetails)
        {
            if (!transactionsById.TryGetValue(detail.TransactionId, out var transaction))
                continue;

            PendingReimbursableExpenses.Add(new PendingReimbursableExpenseOption(
                transaction.Id,
                BuildPendingExpenseLabel(transaction, categoryNames)));
        }

        // Pre-select the linked expense (and switch to the Reimbursement type) when navigated here from
        // MedicalExpenseDetailPage's "Record reimbursement" button — see LinkedTransactionId's doc comment.
        if (Guid.TryParse(LinkedTransactionId, out var linkedTransactionId))
        {
            var preselected = PendingReimbursableExpenses.FirstOrDefault(o => o.TransactionId == linkedTransactionId);
            if (preselected is not null)
            {
                SelectedType = TransactionType.Reimbursement;
                SelectedPendingReimbursableExpense = preselected;
            }
        }
    }

    private static string BuildPendingExpenseLabel(Transaction transaction, IReadOnlyDictionary<Guid, string> categoryNames)
    {
        var categoryLabel = transaction.SpendCategoryId is { } categoryId && categoryNames.TryGetValue(categoryId, out var name)
            ? name
            : string.Empty;
        var descriptor = !string.IsNullOrWhiteSpace(transaction.Description) ? transaction.Description : categoryLabel;

        return $"{transaction.Date.ToString("d", CultureInfo.CurrentCulture)} · {descriptor} · {transaction.Amount.ToString("N2", CultureInfo.CurrentCulture)}";
    }

    partial void OnSelectedLoanPaymentTargetChanged(NamedOption? value)
    {
        var loan = _loans.FirstOrDefault(l => l.Id == value?.Id);
        if (loan is null)
            return;

        // UI-suggested defaults only — the user can still edit both before saving; never enforced.
        LoanNextPaymentDate = loan.NextPaymentDate.AddMonths(1).ToDateTime(TimeOnly.MinValue);
        RequiredPaymentText = loan.MonthlyInstallment.ToString(CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// Clears <see cref="SelectedDestinationAccount"/> whenever the transaction type changes — it is the
    /// only selection shared across two pickers backed by different collections (Income's <see cref="Accounts"/>,
    /// which includes <c>TermDeposit</c>, vs. Transfer's <see cref="TransferDestinationAccounts"/>, which
    /// deliberately excludes it — see that collection's doc comment). Without this reset, selecting a
    /// term deposit under Income and then switching to Transfer without touching the destination picker
    /// again would carry the stale, now-invalid selection straight into <c>SaveAsync</c>'s Transfer case,
    /// bypassing the very protection <see cref="TransferDestinationAccounts"/> exists for. Every other
    /// selection property here is scoped to a single transaction type's own block/collection and has no
    /// equivalent cross-contamination risk, so it is left untouched.
    /// </summary>
    partial void OnSelectedTypeChanged(TransactionType value) => SelectedDestinationAccount = null;

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

                    if (IsMedicalExpense)
                    {
                        decimal? medicalInsuranceCoveredAmount = null;
                        if (HasInsuranceCoverage)
                        {
                            if (!decimal.TryParse(MedicalInsuranceCoveredAmountText, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsedCoveredAmount) || parsedCoveredAmount <= 0)
                            {
                                ErrorMessage = AppResources.AddTransaction_ValidationMedicalCoveredInvalid;
                                return;
                            }

                            medicalInsuranceCoveredAmount = parsedCoveredAmount;
                        }

                        var medicalInfo = new MedicalInsuranceInput(
                            MedicalInsuranceProvider,
                            medicalInsuranceCoveredAmount,
                            InsurancePaidProviderDirectly);

                        if (SelectedAccount.IsCreditAccount)
                        {
                            await _transactionEntryService.RecordMedicalCreditCardPurchaseAsync(
                                date,
                                amount,
                                SelectedAccount.Id,
                                SelectedCategory.Id,
                                AsNullableId(SelectedPayer),
                                AsNullableId(SelectedBeneficiary),
                                Description,
                                Notes,
                                medicalInfo);
                        }
                        else
                        {
                            await _transactionEntryService.RecordMedicalExpenseAsync(
                                date,
                                amount,
                                SelectedAccount.Id,
                                SelectedCategory.Id,
                                AsNullableId(SelectedPayer),
                                AsNullableId(SelectedBeneficiary),
                                Description,
                                Notes,
                                medicalInfo);
                        }
                    }
                    else if (SelectedAccount.IsCreditAccount)
                    {
                        await _transactionEntryService.RecordCreditCardPurchaseAsync(
                            date,
                            amount,
                            SelectedAccount.Id,
                            SelectedCategory.Id,
                            AsNullableId(SelectedPayer),
                            AsNullableId(SelectedBeneficiary),
                            Description,
                            Notes);
                    }
                    else
                    {
                        await _transactionEntryService.RecordExpenseAsync(
                            date,
                            amount,
                            SelectedAccount.Id,
                            SelectedCategory.Id,
                            AsNullableId(SelectedPayer),
                            AsNullableId(SelectedBeneficiary),
                            Description,
                            Notes);
                    }
                    break;

                case TransactionType.Income:
                    if (SelectedDestinationAccount is null)
                    {
                        ErrorMessage = AppResources.AddTransaction_ValidationAccountRequired;
                        return;
                    }

                    if (SelectedDestinationAccount.IsTermDeposit)
                    {
                        // A term deposit's Income destination is interest, not categorizable income —
                        // no Category/Person on InterestIncome (same precedent as Transfer/
                        // InvestmentContribution's typed, unambiguous meaning).
                        await _transactionEntryService.RecordInterestIncomeAsync(
                            date,
                            amount,
                            SelectedDestinationAccount.Id,
                            Description,
                            Notes);
                        break;
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

                    // Defense-in-depth: OnSelectedTypeChanged resets SelectedDestinationAccount on every
                    // type switch, so this should be unreachable in practice — but RecordTransferAsync
                    // itself has no term-deposit guard (its source side deliberately allows one, see
                    // TransferDestinationAccounts's doc comment), so this client-side check is the only
                    // thing standing between a future regression and a silently corrupted deposit balance.
                    if (SelectedDestinationAccount.IsTermDeposit)
                    {
                        ErrorMessage = AppResources.AddTransaction_ValidationTermDepositDestinationInvalid;
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

                case TransactionType.CreditCardPayment:
                    if (SelectedCardPaymentSource is null || SelectedCardPaymentTarget is null)
                    {
                        ErrorMessage = AppResources.AddTransaction_ValidationAccountRequired;
                        return;
                    }

                    if (SelectedCardPaymentSource.Currency != SelectedCardPaymentTarget.Currency)
                    {
                        ErrorMessage = AppResources.AddTransaction_ValidationCurrencyMismatch;
                        return;
                    }

                    await _transactionEntryService.RecordCreditCardPaymentAsync(
                        date,
                        amount,
                        SelectedCardPaymentSource.Id,
                        SelectedCardPaymentTarget.Id,
                        Description,
                        Notes);
                    break;

                case TransactionType.LoanPayment:
                    if (SelectedLoanPaymentSource is null || SelectedLoanPaymentTarget is null)
                    {
                        ErrorMessage = AppResources.AddTransaction_ValidationAccountRequired;
                        return;
                    }

                    if (SelectedLoanPaymentSource.Currency != SelectedLoanPaymentTarget.Currency)
                    {
                        ErrorMessage = AppResources.AddTransaction_ValidationCurrencyMismatch;
                        return;
                    }

                    if (!decimal.TryParse(RequiredPaymentText, NumberStyles.Number, CultureInfo.CurrentCulture, out var requiredPayment) || requiredPayment <= 0)
                    {
                        ErrorMessage = AppResources.AddTransaction_ValidationRequiredPaymentInvalid;
                        return;
                    }

                    await _transactionEntryService.RecordLoanPaymentAsync(
                        date,
                        amount,
                        SelectedLoanPaymentSource.Id,
                        SelectedLoanPaymentTarget.Id,
                        DateOnly.FromDateTime(LoanNextPaymentDate),
                        requiredPayment,
                        Description,
                        Notes);
                    break;

                case TransactionType.InvestmentContribution:
                    if (SelectedInvestmentContributionSource is null || SelectedInvestmentContributionFund is null)
                    {
                        ErrorMessage = AppResources.AddTransaction_ValidationAccountRequired;
                        return;
                    }

                    if (SelectedInvestmentContributionSource.Currency != SelectedInvestmentContributionFund.Currency)
                    {
                        ErrorMessage = AppResources.AddTransaction_ValidationCurrencyMismatch;
                        return;
                    }

                    await _transactionEntryService.RecordInvestmentContributionAsync(
                        date,
                        amount,
                        SelectedInvestmentContributionSource.Id,
                        SelectedInvestmentContributionFund.Id,
                        Description,
                        Notes);
                    break;

                case TransactionType.InvestmentWithdrawal:
                    if (SelectedInvestmentWithdrawalFund is null || SelectedInvestmentWithdrawalDestination is null)
                    {
                        ErrorMessage = AppResources.AddTransaction_ValidationAccountRequired;
                        return;
                    }

                    if (SelectedInvestmentWithdrawalFund.Currency != SelectedInvestmentWithdrawalDestination.Currency)
                    {
                        ErrorMessage = AppResources.AddTransaction_ValidationCurrencyMismatch;
                        return;
                    }

                    await _transactionEntryService.RecordInvestmentWithdrawalAsync(
                        date,
                        amount,
                        SelectedInvestmentWithdrawalFund.Id,
                        SelectedInvestmentWithdrawalDestination.Id,
                        Description,
                        Notes);
                    break;

                case TransactionType.Reimbursement:
                    if (SelectedPendingReimbursableExpense is null || SelectedReimbursementDestinationAccount is null)
                    {
                        ErrorMessage = AppResources.AddTransaction_ValidationAccountRequired;
                        return;
                    }

                    await _transactionEntryService.RecordMedicalReimbursementAsync(
                        date,
                        amount,
                        SelectedPendingReimbursableExpense.TransactionId,
                        SelectedReimbursementDestinationAccount.Id,
                        Description,
                        Notes);
                    break;
            }

            await Shell.Current.GoToAsync("..");
        }
        catch (InvalidOperationException)
        {
            // A service-layer invariant rejected the save (e.g. CreditAccount.RegisterPayment's
            // overpayment guard — nothing client-side compares the entered amount against the target
            // card/loan's current AmountOwed before calling the service). Surface a generic, localized
            // message rather than the raw domain exception text, mirroring how RecurringExpensesListViewModel
            // and SettingsViewModel handle a failed service call elsewhere in this app.
            ErrorMessage = AppResources.AddTransaction_ValidationServiceError;
        }
        catch (ArgumentOutOfRangeException)
        {
            // MedicalExpenseDetail.MarkReimbursed throws this when the entered reimbursement amount
            // exceeds the original expense's GrossAmount — a genuinely user-correctable input error
            // (unlike the generic service-error case above), so it gets its own, more specific message.
            ErrorMessage = AppResources.AddTransaction_ValidationReimbursementExceedsOriginal;
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
