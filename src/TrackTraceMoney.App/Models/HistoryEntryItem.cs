using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Domain.MedicalExpenses;
using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.App.Models;

/// <summary>
/// A single row in the History screen (README §39). Carries the same display fields as
/// <see cref="TransactionListItem"/> plus the raw filter keys (accounts/category/people involved)
/// needed for client-side filtering — this app filters in-memory over the full local transaction
/// set rather than pushing filters down to SQLite, since offline-first MVP volume doesn't need it.
/// <see cref="MedicalStatusBadge"/> (README §25/§26/§27/§29) is only ever non-null for
/// Pending/Reimbursed/Rejected — None/PaidDirectly and transactions with no
/// <see cref="MedicalExpenseDetail"/> row at all never show a badge.
/// </summary>
public sealed record HistoryEntryItem(
    Guid Id,
    DateOnly Date,
    decimal Amount,
    TransactionType Type,
    string? Description,
    string AccountLabel,
    IReadOnlyList<Guid> AccountIds,
    Guid? CategoryId,
    IReadOnlyList<Guid> PersonIds,
    string? MedicalStatusBadge = null)
{
    public static HistoryEntryItem FromDomain(
        Transaction transaction,
        IReadOnlyDictionary<Guid, string> accountNames,
        IReadOnlyDictionary<Guid, string> categoryNames,
        MedicalReimbursementStatus? medicalStatus = null)
    {
        var accountLabel = TransactionLabelFormatter.BuildLabel(transaction, accountNames, categoryNames);
        var medicalStatusBadge = GetMedicalStatusBadge(medicalStatus);

        var item = transaction switch
        {
            Expense expense => new HistoryEntryItem(
                expense.Id,
                expense.Date,
                expense.Amount,
                TransactionType.Expense,
                expense.Description,
                accountLabel,
                [expense.AccountId],
                expense.CategoryId,
                BuildPersonIds(expense.PayerPersonId, expense.BeneficiaryPersonId)),
            Income income => new HistoryEntryItem(
                income.Id,
                income.Date,
                income.Amount,
                TransactionType.Income,
                income.Description,
                accountLabel,
                [income.DestinationAccountId],
                income.CategoryId,
                BuildPersonIds(income.PersonId)),
            Transfer transfer => new HistoryEntryItem(
                transfer.Id,
                transfer.Date,
                transfer.Amount,
                TransactionType.Transfer,
                transfer.Description,
                accountLabel,
                [transfer.SourceAccountId, transfer.DestinationAccountId],
                null,
                []),
            CreditCardPurchase purchase => new HistoryEntryItem(
                purchase.Id,
                purchase.Date,
                purchase.Amount,
                TransactionType.CreditCardPurchase,
                purchase.Description,
                accountLabel,
                [purchase.CreditAccountId],
                purchase.CategoryId,
                BuildPersonIds(purchase.PayerPersonId, purchase.BeneficiaryPersonId)),
            CreditCardPayment payment => new HistoryEntryItem(
                payment.Id,
                payment.Date,
                payment.Amount,
                TransactionType.CreditCardPayment,
                payment.Description,
                accountLabel,
                [payment.SourceAccountId, payment.CreditAccountId],
                null,
                []),
            LoanPayment payment => new HistoryEntryItem(
                payment.Id,
                payment.Date,
                payment.Amount,
                TransactionType.LoanPayment,
                payment.Description,
                accountLabel,
                [payment.SourceAccountId, payment.CreditAccountId],
                null,
                []),
            InvestmentContribution contribution => new HistoryEntryItem(
                contribution.Id,
                contribution.Date,
                contribution.Amount,
                TransactionType.InvestmentContribution,
                contribution.Description,
                accountLabel,
                [contribution.SourceAccountId, contribution.DestinationAccountId],
                null,
                []),
            InvestmentWithdrawal withdrawal => new HistoryEntryItem(
                withdrawal.Id,
                withdrawal.Date,
                withdrawal.Amount,
                TransactionType.InvestmentWithdrawal,
                withdrawal.Description,
                accountLabel,
                [withdrawal.SourceAccountId, withdrawal.DestinationAccountId],
                null,
                []),
            InterestIncome interestIncome => new HistoryEntryItem(
                interestIncome.Id,
                interestIncome.Date,
                interestIncome.Amount,
                TransactionType.InterestIncome,
                interestIncome.Description,
                accountLabel,
                [interestIncome.DestinationAccountId],
                null,
                []),
            Reimbursement reimbursement => new HistoryEntryItem(
                reimbursement.Id,
                reimbursement.Date,
                reimbursement.Amount,
                TransactionType.Reimbursement,
                reimbursement.Description,
                accountLabel,
                [reimbursement.DestinationAccountId],
                null,
                []),
            _ => throw new NotSupportedException($"Unknown transaction type '{transaction.GetType().Name}'.")
        };

        return item with { MedicalStatusBadge = medicalStatusBadge };
    }

    private static IReadOnlyList<Guid> BuildPersonIds(params Guid?[] personIds) =>
        personIds.Where(id => id.HasValue).Select(id => id!.Value).ToList();

    /// <summary>
    /// Only Pending/Reimbursed/Rejected ever get a badge — <see cref="MedicalReimbursementStatus.None"/>/
    /// <see cref="MedicalReimbursementStatus.PaidDirectly"/> and "no detail row at all" (a null
    /// <paramref name="status"/>) both resolve to no badge (README §25/§26/§27/§29).
    /// </summary>
    private static string? GetMedicalStatusBadge(MedicalReimbursementStatus? status) => status switch
    {
        MedicalReimbursementStatus.Pending => AppResources.History_MedicalStatusPending,
        MedicalReimbursementStatus.Reimbursed => AppResources.History_MedicalStatusReimbursed,
        MedicalReimbursementStatus.Rejected => AppResources.History_MedicalStatusRejected,
        _ => null
    };
}
