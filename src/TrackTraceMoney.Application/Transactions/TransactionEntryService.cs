using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Application.Reporting;
using TrackTraceMoney.Domain.Accounts;
using TrackTraceMoney.Domain.Budgets;
using TrackTraceMoney.Domain.CreditAccounts;
using TrackTraceMoney.Domain.Enums;
using TrackTraceMoney.Domain.MedicalExpenses;
using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.Application.Transactions;

public sealed class TransactionEntryService : ITransactionEntryService
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IFinancialAccountRepository _accountRepository;
    private readonly ICreditAccountRepository _creditAccountRepository;
    private readonly IBudgetRepository _budgetRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ISpendingCalculator _spendingCalculator;
    private readonly ILocalNotifier _localNotifier;
    private readonly IMedicalExpenseDetailRepository _medicalExpenseDetailRepository;

    public TransactionEntryService(
        ITransactionRepository transactionRepository,
        IFinancialAccountRepository accountRepository,
        ICreditAccountRepository creditAccountRepository,
        IBudgetRepository budgetRepository,
        ICategoryRepository categoryRepository,
        ISpendingCalculator spendingCalculator,
        ILocalNotifier localNotifier,
        IMedicalExpenseDetailRepository medicalExpenseDetailRepository)
    {
        _transactionRepository = transactionRepository;
        _accountRepository = accountRepository;
        _creditAccountRepository = creditAccountRepository;
        _budgetRepository = budgetRepository;
        _categoryRepository = categoryRepository;
        _spendingCalculator = spendingCalculator;
        _localNotifier = localNotifier;
        _medicalExpenseDetailRepository = medicalExpenseDetailRepository;
    }

    public async Task RecordExpenseAsync(
        DateOnly date,
        decimal amount,
        Guid accountId,
        Guid categoryId,
        Guid? payerPersonId,
        Guid? beneficiaryPersonId,
        string? description,
        string? notes,
        CancellationToken ct = default)
    {
        var account = await _accountRepository.GetByIdAsync(accountId, ct)
            ?? throw new InvalidOperationException($"Account '{accountId}' was not found.");

        // README §22/§30: a term deposit is locked until maturity — unlike Transfer's source side
        // (deliberately still allowed, see RecordTransferAsync's doc comment/TransferDestinationAccounts
        // in AddTransactionViewModel), there is no legitimate "spend directly from a term deposit" use
        // case, and debiting it here would silently corrupt its balance with no maturity/overdraft check.
        if (account is TermDeposit)
            throw new InvalidOperationException("A term deposit cannot be used to fund this transaction.");

        // README §34/§37: figure out, before saving, whether this expense is the one that pushes the
        // category's monthly budget over its limit — only that crossing transition should notify, not
        // every subsequent expense once already over. Computed arithmetically from spend-before-this
        // expense rather than re-querying after save, so there's no risk of double-counting this
        // expense against itself.
        var (budget, spentBefore) = await GetBudgetCrossingContextAsync(categoryId, date, account.Currency, ct);

        var expense = new Expense(date, amount, accountId, categoryId, beneficiaryPersonId, payerPersonId, description, notes);

        account.Debit(amount);

        await _transactionRepository.AddAsync(expense, ct);
        await _transactionRepository.SaveChangesAsync(ct);

        await NotifyIfBudgetCrossedAsync(budget, spentBefore, amount, categoryId, ct);
    }

    public async Task RecordIncomeAsync(
        DateOnly date,
        decimal amount,
        Guid destinationAccountId,
        Guid categoryId,
        Guid? personId,
        string? description,
        string? notes,
        CancellationToken ct = default)
    {
        var destinationAccount = await _accountRepository.GetByIdAsync(destinationAccountId, ct)
            ?? throw new InvalidOperationException($"Account '{destinationAccountId}' was not found.");

        var income = new Income(date, amount, destinationAccountId, categoryId, personId, description, notes);

        destinationAccount.Credit(amount);

        await _transactionRepository.AddAsync(income, ct);
        await _transactionRepository.SaveChangesAsync(ct);
    }

    public async Task RecordTransferAsync(
        DateOnly date,
        decimal amount,
        Guid sourceAccountId,
        Guid destinationAccountId,
        string? description,
        string? notes,
        CancellationToken ct = default)
    {
        var sourceAccount = await _accountRepository.GetByIdAsync(sourceAccountId, ct)
            ?? throw new InvalidOperationException($"Account '{sourceAccountId}' was not found.");

        var destinationAccount = await _accountRepository.GetByIdAsync(destinationAccountId, ct)
            ?? throw new InvalidOperationException($"Account '{destinationAccountId}' was not found.");

        // README §6/§10: currency is explicit per account and never assumed global. Without an
        // exchange-rate/conversion feature (not in scope for MVP), applying the same numeric amount
        // to accounts in different currencies would silently corrupt both balances.
        if (sourceAccount.Currency != destinationAccount.Currency)
            throw new InvalidOperationException(
                $"Cannot transfer between accounts with different currencies ({sourceAccount.Currency} -> {destinationAccount.Currency}) without currency conversion support.");

        var transfer = new Transfer(date, amount, sourceAccountId, destinationAccountId, description, notes);

        sourceAccount.Debit(amount);
        destinationAccount.Credit(amount);

        await _transactionRepository.AddAsync(transfer, ct);
        await _transactionRepository.SaveChangesAsync(ct);
    }

    public async Task RecordCreditCardPurchaseAsync(
        DateOnly date,
        decimal amount,
        Guid creditAccountId,
        Guid categoryId,
        Guid? payerPersonId,
        Guid? beneficiaryPersonId,
        string? description,
        string? notes,
        CancellationToken ct = default)
    {
        var creditAccount = await _creditAccountRepository.GetByIdAsync(creditAccountId, ct)
            ?? throw new InvalidOperationException($"Credit account '{creditAccountId}' was not found.");

        if (creditAccount is not CreditCard)
            throw new InvalidOperationException($"Credit account '{creditAccountId}' is not a credit card.");

        // Same budget-crossing check as RecordExpenseAsync — see its comment above. Kept duplicated
        // here rather than factored into a second service, since the crossing logic must stay in sync
        // for every spend-counting transaction kind (README §34/§37; CLAUDE.md's #1 risk).
        var (budget, spentBefore) = await GetBudgetCrossingContextAsync(categoryId, date, creditAccount.Currency, ct);

        var purchase = new CreditCardPurchase(date, amount, creditAccountId, categoryId, beneficiaryPersonId, payerPersonId, description, notes);

        creditAccount.RegisterCharge(amount);

        await _transactionRepository.AddAsync(purchase, ct);
        await _transactionRepository.SaveChangesAsync(ct);

        await NotifyIfBudgetCrossedAsync(budget, spentBefore, amount, categoryId, ct);
    }

    public async Task RecordMedicalExpenseAsync(
        DateOnly date,
        decimal amount,
        Guid accountId,
        Guid categoryId,
        Guid? payerPersonId,
        Guid? beneficiaryPersonId,
        string? description,
        string? notes,
        MedicalInsuranceInput medicalInfo,
        CancellationToken ct = default)
    {
        var account = await _accountRepository.GetByIdAsync(accountId, ct)
            ?? throw new InvalidOperationException($"Account '{accountId}' was not found.");

        // Same term-deposit guard as RecordExpenseAsync — see its comment above. This is the medical
        // sub-path of the same Expense UI flow (AddTransactionViewModel routes here instead of
        // RecordExpenseAsync when "medical expense" is checked), so it carries the identical risk.
        if (account is TermDeposit)
            throw new InvalidOperationException("A term deposit cannot be used to fund this transaction.");

        // Same budget-crossing check as RecordExpenseAsync — see its comment above. A medical expense
        // still counts as spend exactly like any other, so this must fire unconditionally, not be
        // special-cased away.
        var (budget, spentBefore) = await GetBudgetCrossingContextAsync(categoryId, date, account.Currency, ct);

        var expense = new Expense(date, amount, accountId, categoryId, beneficiaryPersonId, payerPersonId, description, notes);

        account.Debit(amount);

        await _transactionRepository.AddAsync(expense, ct);

        var detail = BuildMedicalDetail(expense.Id, amount, medicalInfo);
        await _medicalExpenseDetailRepository.AddAsync(detail, ct);

        await _transactionRepository.SaveChangesAsync(ct);

        await NotifyIfBudgetCrossedAsync(budget, spentBefore, amount, categoryId, ct);
    }

    public async Task RecordMedicalCreditCardPurchaseAsync(
        DateOnly date,
        decimal amount,
        Guid creditAccountId,
        Guid categoryId,
        Guid? payerPersonId,
        Guid? beneficiaryPersonId,
        string? description,
        string? notes,
        MedicalInsuranceInput medicalInfo,
        CancellationToken ct = default)
    {
        var creditAccount = await _creditAccountRepository.GetByIdAsync(creditAccountId, ct)
            ?? throw new InvalidOperationException($"Credit account '{creditAccountId}' was not found.");

        if (creditAccount is not CreditCard)
            throw new InvalidOperationException($"Credit account '{creditAccountId}' is not a credit card.");

        // Same budget-crossing check as RecordCreditCardPurchaseAsync — see its comment above.
        var (budget, spentBefore) = await GetBudgetCrossingContextAsync(categoryId, date, creditAccount.Currency, ct);

        var purchase = new CreditCardPurchase(date, amount, creditAccountId, categoryId, beneficiaryPersonId, payerPersonId, description, notes);

        creditAccount.RegisterCharge(amount);

        await _transactionRepository.AddAsync(purchase, ct);

        var detail = BuildMedicalDetail(purchase.Id, amount, medicalInfo);
        await _medicalExpenseDetailRepository.AddAsync(detail, ct);

        await _transactionRepository.SaveChangesAsync(ct);

        await NotifyIfBudgetCrossedAsync(budget, spentBefore, amount, categoryId, ct);
    }

    /// <summary>
    /// Fetches the category's budget for the transaction's month (if any) and the amount already spent
    /// in that category/currency before this transaction, so the caller can construct/persist its
    /// transaction in between this call and <see cref="NotifyIfBudgetCrossedAsync"/> without re-querying
    /// spend after save (which would risk double-counting the transaction being saved against itself).
    /// Shared by every spend-counting Record*Async method (README §34/§37; CLAUDE.md's #1 risk) — kept
    /// as a private helper rather than a separate service, per this class's existing design note on
    /// keeping the crossing logic colocated (see the note this replaced, previously duplicated verbatim
    /// in each caller).
    /// </summary>
    private async Task<(Budget? Budget, decimal SpentBefore)> GetBudgetCrossingContextAsync(
        Guid categoryId, DateOnly date, CurrencyCode currency, CancellationToken ct)
    {
        var budget = await _budgetRepository.GetForCategoryAndMonthAsync(categoryId, date.Year, date.Month, currency, ct);
        if (budget is null)
            return (null, 0m);

        var startOfMonth = new DateOnly(date.Year, date.Month, 1);
        var categoryTransactions = await _transactionRepository.GetByDateRangeAndCategoryAsync(startOfMonth, date, categoryId, ct);
        var accounts = await _accountRepository.GetAllAsync(ct);
        var creditAccounts = await _creditAccountRepository.GetAllAsync(ct);
        var accountCurrencies = AccountCurrencyMapBuilder.Build(accounts, creditAccounts);
        var summary = _spendingCalculator.Calculate(categoryTransactions, accountCurrencies);
        var spentBefore = summary.GetSpentForCategory(categoryId, budget.Currency);

        return (budget, spentBefore);
    }

    /// <summary>
    /// Second half of the budget-crossing check started by <see cref="GetBudgetCrossingContextAsync"/> —
    /// called after the transaction has been persisted, notifying only on the crossing transition (not
    /// every subsequent over-budget transaction).
    /// </summary>
    private async Task NotifyIfBudgetCrossedAsync(Budget? budget, decimal spentBefore, decimal amount, Guid categoryId, CancellationToken ct)
    {
        if (budget is null)
            return;

        var wasOverBudget = spentBefore > budget.Amount;
        var newSpent = spentBefore + amount;
        var isOverBudget = newSpent > budget.Amount;

        if (!wasOverBudget && isOverBudget)
        {
            var category = await _categoryRepository.GetByIdAsync(categoryId, ct);
            if (category is not null)
                await _localNotifier.NotifyBudgetExceededAsync(category, budget.Amount, newSpent - budget.Amount, ct);
        }
    }

    /// <summary>
    /// Shared by <see cref="RecordMedicalExpenseAsync"/>/<see cref="RecordMedicalCreditCardPurchaseAsync"/>.
    /// <paramref name="amount"/> is always what the account/card actually moved (net of insurance that
    /// paid the provider directly) — <see cref="MedicalExpenseDetail.GrossAmount"/> reconstructs the
    /// pre-insurance total only for display/record-keeping, it never corrects the transaction's own Amount.
    /// </summary>
    private static MedicalExpenseDetail BuildMedicalDetail(Guid transactionId, decimal amount, MedicalInsuranceInput medicalInfo)
    {
        decimal? grossAmount = null;
        var status = MedicalReimbursementStatus.None;
        if (medicalInfo.InsuranceCoveredAmount is > 0m)
        {
            grossAmount = medicalInfo.InsurancePaidProviderDirectly
                ? amount + medicalInfo.InsuranceCoveredAmount.Value
                : amount;
            status = medicalInfo.InsurancePaidProviderDirectly
                ? MedicalReimbursementStatus.PaidDirectly
                : MedicalReimbursementStatus.Pending;
        }

        return new MedicalExpenseDetail(transactionId, medicalInfo.InsuranceProvider, grossAmount, medicalInfo.InsuranceCoveredAmount, status);
    }

    public async Task RecordCreditCardPaymentAsync(
        DateOnly date,
        decimal amount,
        Guid sourceAccountId,
        Guid creditAccountId,
        string? description,
        string? notes,
        CancellationToken ct = default)
    {
        var sourceAccount = await _accountRepository.GetByIdAsync(sourceAccountId, ct)
            ?? throw new InvalidOperationException($"Account '{sourceAccountId}' was not found.");

        // Same term-deposit guard as RecordExpenseAsync — see its comment above.
        if (sourceAccount is TermDeposit)
            throw new InvalidOperationException("A term deposit cannot be used to fund this transaction.");

        var creditAccount = await _creditAccountRepository.GetByIdAsync(creditAccountId, ct)
            ?? throw new InvalidOperationException($"Credit account '{creditAccountId}' was not found.");

        if (creditAccount is not CreditCard)
            throw new InvalidOperationException($"Credit account '{creditAccountId}' is not a credit card.");

        // README §6/§10: same cross-currency rejection as RecordTransferAsync — no conversion feature
        // in scope, so a numeric amount can't be safely applied to accounts in different currencies.
        if (sourceAccount.Currency != creditAccount.Currency)
            throw new InvalidOperationException(
                $"Cannot pay a card with an account in a different currency ({sourceAccount.Currency} -> {creditAccount.Currency}) without currency conversion support.");

        var payment = new CreditCardPayment(date, amount, sourceAccountId, creditAccountId, description, notes);

        // Credit side first: RegisterPayment's overpayment guard is the stricter/newer invariant — if it
        // throws, the source account must not have been mutated yet.
        creditAccount.RegisterPayment(amount);
        sourceAccount.Debit(amount);

        await _transactionRepository.AddAsync(payment, ct);
        await _transactionRepository.SaveChangesAsync(ct);
    }

    public async Task RecordLoanPaymentAsync(
        DateOnly date,
        decimal amount,
        Guid sourceAccountId,
        Guid loanAccountId,
        DateOnly nextPaymentDate,
        decimal requiredPayment,
        string? description,
        string? notes,
        CancellationToken ct = default)
    {
        var sourceAccount = await _accountRepository.GetByIdAsync(sourceAccountId, ct)
            ?? throw new InvalidOperationException($"Account '{sourceAccountId}' was not found.");

        // Same term-deposit guard as RecordExpenseAsync — see its comment above.
        if (sourceAccount is TermDeposit)
            throw new InvalidOperationException("A term deposit cannot be used to fund this transaction.");

        var creditAccount = await _creditAccountRepository.GetByIdAsync(loanAccountId, ct)
            ?? throw new InvalidOperationException($"Credit account '{loanAccountId}' was not found.");

        if (creditAccount is not Loan loan)
            throw new InvalidOperationException($"Credit account '{loanAccountId}' is not a loan.");

        if (sourceAccount.Currency != loan.Currency)
            throw new InvalidOperationException(
                $"Cannot pay a loan with an account in a different currency ({sourceAccount.Currency} -> {loan.Currency}) without currency conversion support.");

        var payment = new LoanPayment(date, amount, sourceAccountId, loanAccountId, description, notes);

        // Credit side first: RegisterPayment's overpayment guard is the stricter/newer invariant — if it
        // throws, the source account must not have been mutated yet. Same ordering as CreditCardPayment.
        loan.RegisterPayment(amount);
        loan.AdvanceSchedule(nextPaymentDate, requiredPayment);
        sourceAccount.Debit(amount);

        await _transactionRepository.AddAsync(payment, ct);
        await _transactionRepository.SaveChangesAsync(ct);
    }

    public async Task RecordInvestmentContributionAsync(
        DateOnly date,
        decimal amount,
        Guid sourceAccountId,
        Guid investmentFundId,
        string? description,
        string? notes,
        CancellationToken ct = default)
    {
        var sourceAccount = await _accountRepository.GetByIdAsync(sourceAccountId, ct)
            ?? throw new InvalidOperationException($"Account '{sourceAccountId}' was not found.");

        // Same term-deposit guard as RecordExpenseAsync — see its comment above. The destination side
        // is already restricted to InvestmentFund below and needs no additional guard.
        if (sourceAccount is TermDeposit)
            throw new InvalidOperationException("A term deposit cannot be used to fund this transaction.");

        var fundAccount = await _accountRepository.GetByIdAsync(investmentFundId, ct)
            ?? throw new InvalidOperationException($"Account '{investmentFundId}' was not found.");

        if (fundAccount is not InvestmentFund fund)
            throw new InvalidOperationException($"Account '{investmentFundId}' is not an investment fund.");

        // README §6/§10: same cross-currency rejection as RecordTransferAsync — no conversion feature
        // in scope, so a numeric amount can't be safely applied to accounts in different currencies.
        if (sourceAccount.Currency != fund.Currency)
            throw new InvalidOperationException(
                $"Cannot contribute to a fund with an account in a different currency ({sourceAccount.Currency} -> {fund.Currency}) without currency conversion support.");

        var contribution = new InvestmentContribution(date, amount, sourceAccountId, investmentFundId, description, notes);

        sourceAccount.Debit(amount);
        fund.RecordContribution(amount);

        await _transactionRepository.AddAsync(contribution, ct);
        await _transactionRepository.SaveChangesAsync(ct);
    }

    public async Task RecordInvestmentWithdrawalAsync(
        DateOnly date,
        decimal amount,
        Guid investmentFundId,
        Guid destinationAccountId,
        string? description,
        string? notes,
        CancellationToken ct = default)
    {
        var fundAccount = await _accountRepository.GetByIdAsync(investmentFundId, ct)
            ?? throw new InvalidOperationException($"Account '{investmentFundId}' was not found.");

        if (fundAccount is not InvestmentFund fund)
            throw new InvalidOperationException($"Account '{investmentFundId}' is not an investment fund.");

        var destinationAccount = await _accountRepository.GetByIdAsync(destinationAccountId, ct)
            ?? throw new InvalidOperationException($"Account '{destinationAccountId}' was not found.");

        if (fund.Currency != destinationAccount.Currency)
            throw new InvalidOperationException(
                $"Cannot withdraw from a fund into an account in a different currency ({fund.Currency} -> {destinationAccount.Currency}) without currency conversion support.");

        var withdrawal = new InvestmentWithdrawal(date, amount, investmentFundId, destinationAccountId, description, notes);

        // No overdraft guard here — InvestmentFund.RecordWithdrawal deliberately mirrors Debit's lax
        // convention (asset-side accounts in this codebase never get liability-side guards like
        // CreditAccount.RegisterPayment's). Do not add one.
        fund.RecordWithdrawal(amount);
        destinationAccount.Credit(amount);

        await _transactionRepository.AddAsync(withdrawal, ct);
        await _transactionRepository.SaveChangesAsync(ct);
    }

    public async Task RecordInterestIncomeAsync(
        DateOnly date,
        decimal amount,
        Guid termDepositId,
        string? description,
        string? notes,
        CancellationToken ct = default)
    {
        var account = await _accountRepository.GetByIdAsync(termDepositId, ct)
            ?? throw new InvalidOperationException($"Account '{termDepositId}' was not found.");

        if (account is not TermDeposit termDeposit)
            throw new InvalidOperationException($"Account '{termDepositId}' is not a term deposit.");

        var interestIncome = new InterestIncome(date, amount, termDepositId, description, notes);

        termDeposit.RecordInterestReceived(amount);

        await _transactionRepository.AddAsync(interestIncome, ct);
        await _transactionRepository.SaveChangesAsync(ct);
    }

    public async Task RecordMedicalReimbursementAsync(
        DateOnly date,
        decimal actualAmountReceived,
        Guid linkedTransactionId,
        Guid destinationAccountId,
        string? description,
        string? notes,
        CancellationToken ct = default)
    {
        var linkedTransaction = await _transactionRepository.GetByIdAsync(linkedTransactionId, ct)
            ?? throw new InvalidOperationException($"Transaction '{linkedTransactionId}' was not found.");

        var detail = await _medicalExpenseDetailRepository.GetForTransactionAsync(linkedTransactionId, ct)
            ?? throw new InvalidOperationException(
                $"Transaction '{linkedTransactionId}' has no medical expense detail; reimbursements are only supported for medical expenses in this version.");

        if (detail.Status != MedicalReimbursementStatus.Pending)
            throw new InvalidOperationException(
                $"Transaction '{linkedTransactionId}' is not pending reimbursement (current status: {detail.Status}).");

        var destinationAccount = await _accountRepository.GetByIdAsync(destinationAccountId, ct)
            ?? throw new InvalidOperationException($"Account '{destinationAccountId}' was not found.");

        // README §6/§10: same cross-currency rejection as RecordTransferAsync — a reimbursement must
        // land in an account denominated in the original expense's currency, or it silently corrupts
        // that account's balance the same way a cross-currency transfer would.
        var originalCurrency = await ResolveOriginalExpenseCurrencyAsync(linkedTransaction, ct);
        if (originalCurrency != destinationAccount.Currency)
            throw new InvalidOperationException(
                $"Cannot reimburse into an account in a different currency ({originalCurrency} -> {destinationAccount.Currency}) without currency conversion support.");

        var reimbursement = new Reimbursement(date, actualAmountReceived, destinationAccountId, linkedTransactionId, description, notes);

        // Order matters: MarkReimbursed can still throw (actualAmountReceived > GrossAmount) — run it
        // BEFORE crediting the account so a thrown exception never leaves the account's in-memory
        // Balance mutated ahead of an aborted save.
        detail.MarkReimbursed(actualAmountReceived);
        destinationAccount.Credit(actualAmountReceived);

        await _transactionRepository.AddAsync(reimbursement, ct);
        await _transactionRepository.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Resolves the currency of the account/card the original medical expense actually moved money
    /// against — <see cref="RecordMedicalReimbursementAsync"/>'s only two supported linked-transaction
    /// kinds (an <see cref="Expense"/> debiting a <see cref="Accounts.FinancialAccount"/>, or a
    /// <see cref="CreditCardPurchase"/> charging a <see cref="CreditAccount"/>) resolve their spend
    /// account through different repositories/fields, mirroring how <see cref="AccountCurrencyMapBuilder"/>
    /// already merges both hierarchies elsewhere in this codebase.
    /// </summary>
    private async Task<CurrencyCode> ResolveOriginalExpenseCurrencyAsync(Transaction transaction, CancellationToken ct)
    {
        switch (transaction)
        {
            case CreditCardPurchase purchase:
                var creditAccount = await _creditAccountRepository.GetByIdAsync(purchase.CreditAccountId, ct)
                    ?? throw new InvalidOperationException($"Credit account '{purchase.CreditAccountId}' was not found.");
                return creditAccount.Currency;
            case Expense expense:
                var account = await _accountRepository.GetByIdAsync(expense.AccountId, ct)
                    ?? throw new InvalidOperationException($"Account '{expense.AccountId}' was not found.");
                return account.Currency;
            default:
                throw new InvalidOperationException(
                    $"Transaction '{transaction.Id}' is not a supported reimbursable expense type.");
        }
    }

    public async Task RejectMedicalReimbursementAsync(Guid linkedTransactionId, CancellationToken ct = default)
    {
        var detail = await _medicalExpenseDetailRepository.GetForTransactionAsync(linkedTransactionId, ct)
            ?? throw new InvalidOperationException(
                $"Transaction '{linkedTransactionId}' has no medical expense detail; reimbursements are only supported for medical expenses in this version.");

        detail.MarkRejected(); // throws InvalidOperationException itself if Status != Pending

        await _medicalExpenseDetailRepository.SaveChangesAsync(ct);
    }
}
